using System.Text.Json;
using System.Text.Json.Serialization;
using DataDock.Core.Dialects;
using DataDock.Core.Models;
using DataDock.Core.Services;
using DataDock.Services.DataSources;
using Microsoft.Data.SqlClient;

namespace DataDock.Services;

public sealed class ImportWorkflowService
{
    private readonly AppConfig _appConfig;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ImportWorkflowService(AppConfig? appConfig = null)
    {
        _appConfig = appConfig ?? AppConfigLoader.Load();
    }

    public FilePreviewResult GeneratePreview(string inputPath, int sampleRowLimit = 200)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path is required", nameof(inputPath));
        }

        using var reader = CreateDataReader(inputPath);
        var headers = reader.GetHeaders();
        var samples = new List<string?[]>();
        var rowCount = 0;

        while (reader.Read() && rowCount < sampleRowLimit)
        {
            var row = new string?[headers.Length];
            for (var i = 0; i < headers.Length; i++)
            {
                row[i] = reader.GetField(i);
            }

            samples.Add(row);
            rowCount++;
        }

        var inferred = FileSchemaInferenceService.InferTargetFields(headers, samples, sampleRowLimit);
        return new FilePreviewResult(headers, samples, inferred);
    }

    public ImportExecutionResult RunImport(
        ImportOptions options,
        ImportProfile? profile = null,
        IProgress<ImportLogEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new ArgumentException("Input path is required", nameof(options));
        }

        var resolvedOutput = string.IsNullOrWhiteSpace(options.OutputPath)
            ? options.InputPath + ".out.json"
            : options.OutputPath!;

        profile ??= LoadOrCreateProfile(options);
        EnsureTargetFieldsFromInput(profile, options);
        var connectionSettings = ConnectionSettingsResolver.Resolve(options, profile, _appConfig);

        if (options.EnsureTable)
        {
            EnsureTableExists(profile, connectionSettings);
        }

        using var dataReader = CreateDataReader(options.InputPath);
        var headers = dataReader.GetHeaders();
        var sourceColumns = headers
            .Select((h, idx) => new SourceColumn
            {
                HeaderName = h,
                Index = idx
            })
            .ToList();

        var mapper = new DefaultColumnMapper();
        var mappings = mapper.GenerateMappings(profile, sourceColumns);

        var rowResults = new List<ImportRowResult>();
        var dataRowIndex = 0;

        while (dataReader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            dataRowIndex++;
            var rowResult = new ImportRowResult
            {
                RowNumber = dataRowIndex
            };

            foreach (var mapping in mappings)
            {
                var field = mapping.TargetField;

                if (mapping.SourceColumn == null)
                {
                    if (field.IsRequired && profile.StrictRequiredFields)
                    {
                        rowResult.Errors.Add(
                            $"Required field '{field.Name}' is not mapped to any source column.");
                    }

                    rowResult.Values[field.Name] = null;
                    continue;
                }

                string? rawValue;
                try
                {
                    rawValue = dataReader.GetField(mapping.SourceColumn.Index);
                }
                catch (Exception ex)
                {
                    rowResult.Errors.Add(
                        $"Error reading column '{mapping.SourceColumn.HeaderName}' (index {mapping.SourceColumn.Index}): {ex.Message}");
                    rowResult.Values[field.Name] = null;
                    continue;
                }

                var conversion = ValueConverter.Convert(field.FieldType, rawValue);

                if (!conversion.Success)
                {
                    rowResult.Errors.Add(
                        $"Field '{field.Name}': {conversion.ErrorMessage}");
                    rowResult.Values[field.Name] = null;
                }
                else
                {
                    var value = conversion.Value;

                    if (field.FieldType == FieldType.String &&
                        field.MaxLength.HasValue &&
                        value is string s &&
                        s.Length > field.MaxLength.Value)
                    {
                        rowResult.Errors.Add(
                            $"Field '{field.Name}': string length {s.Length} exceeds max {field.MaxLength.Value}.");
                        rowResult.Values[field.Name] = s.Substring(0, field.MaxLength.Value);
                    }
                    else
                    {
                        rowResult.Values[field.Name] = value;
                    }

                    if (field.IsRequired && profile.StrictRequiredFields)
                    {
                        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
                        {
                            rowResult.Errors.Add(
                                $"Required field '{field.Name}' is empty.");
                        }
                    }
                }
            }

            rowResults.Add(rowResult);
            if (dataRowIndex % 500 == 0)
            {
                progress?.Report(new ImportLogEvent("info", $"Processed {dataRowIndex} rows"));
            }
        }

        var validRows = rowResults.Where(r => r.IsValid).ToList();
        var invalidRows = rowResults.Where(r => !r.IsValid).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutput) ?? ".");
        var payload = validRows.Select(r => r.Values);
        File.WriteAllText(resolvedOutput, JsonSerializer.Serialize(payload, _jsonOptions));

        progress?.Report(new ImportLogEvent("info", $"Wrote {validRows.Count} rows to {resolvedOutput}"));

        var wroteToDatabase = false;
        if (options.WriteToDatabase)
        {
            if (string.IsNullOrWhiteSpace(connectionSettings.ConnectionString))
            {
                throw new InvalidOperationException("No connection string available for database write.");
            }

            var resolvedKeyFields = ResolveKeyFields(options, profile);
            if (options.WriteMode == WriteMode.Upsert && resolvedKeyFields.Count == 0)
            {
                throw new InvalidOperationException("Upsert mode requires at least one key field.");
            }

            progress?.Report(new ImportLogEvent("info", $"Writing {validRows.Count} rows to SQL Server using mode {options.WriteMode}..."));
            var writer = new SqlServerDataWriter(profile, options.WriteMode, resolvedKeyFields, connectionSettings.Schema);
            writer.WriteRows(connectionSettings.ConnectionString!, validRows);
            wroteToDatabase = true;
            progress?.Report(new ImportLogEvent("info", "Database write completed."));
        }

        return new ImportExecutionResult(dataRowIndex, validRows.Count, invalidRows.Count, resolvedOutput, wroteToDatabase, invalidRows.Take(5).ToList());
    }

    public ImportProfile LoadOrCreateProfile(ImportOptions options)
    {
        var hasProfile = !string.IsNullOrWhiteSpace(options.ProfilePath);
        var profile = hasProfile ? LoadProfile(options.ProfilePath!) : new ImportProfile();

        if (!string.IsNullOrWhiteSpace(options.TableName))
        {
            profile.TableName = options.TableName;
        }
        else if (!hasProfile && string.IsNullOrWhiteSpace(profile.TableName))
        {
            profile.TableName = DeriveTableNameFromInput(options.InputPath);
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = profile.TableName ?? DeriveTableNameFromInput(options.InputPath) ?? "DataDock Import";
        }

        if (options.ColumnNameStyleOverride.HasValue)
        {
            profile.ColumnNameStyle = options.ColumnNameStyleOverride.Value;
        }
        else if (!hasProfile)
        {
            profile.ColumnNameStyle = _appConfig.Defaults.ColumnNameStyle;
        }

        if (string.IsNullOrWhiteSpace(profile.TableSchema) && !string.IsNullOrWhiteSpace(options.DatabaseSchema))
        {
            profile.TableSchema = options.DatabaseSchema;
        }

        return profile;
    }

    private static IDataSourceReader CreateDataReader(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".csv" => new CsvDataSourceReader(filePath),
            ".xlsx" => new ExcelDataSourceReader(filePath),
            ".xls" => new ExcelDataSourceReader(filePath),
            _ => throw new NotSupportedException($"File type '{extension}' is not supported. Supported types: .csv, .xlsx, .xls")
        };
    }

    private void EnsureTargetFieldsFromInput(ImportProfile profile, ImportOptions options)
    {
        if (profile.TargetFields.Any())
        {
            return;
        }

        var inferred = InferTargetFieldsFromFile(options.InputPath);
        if (inferred.Count == 0)
        {
            throw new InvalidOperationException($"Unable to infer schema from '{options.InputPath}'. Provide a profile or ensure the file has headers.");
        }

        foreach (var field in inferred)
        {
            profile.TargetFields.Add(field);
        }
    }

    private IReadOnlyList<TargetField> InferTargetFieldsFromFile(string filePath, int sampleRowLimit = 1000)
    {
        using var reader = CreateDataReader(filePath);
        var headers = reader.GetHeaders();
        var samples = new List<IReadOnlyList<string?>>();
        var rowCount = 0;

        while (reader.Read() && rowCount < sampleRowLimit)
        {
            var row = new string?[headers.Length];
            for (var i = 0; i < headers.Length; i++)
            {
                row[i] = reader.GetField(i);
            }

            samples.Add(row);
            rowCount++;
        }

        return FileSchemaInferenceService.InferTargetFields(headers, samples, sampleRowLimit);
    }

    private static string? DeriveTableNameFromInput(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        return Path.GetFileNameWithoutExtension(inputPath);
    }

    private static ImportProfile LoadProfile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Profile not found: {path}");
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        var profile = JsonSerializer.Deserialize<ImportProfile>(json, options);
        if (profile == null)
        {
            throw new InvalidOperationException("Unable to deserialize import profile.");
        }

        return profile;
    }

    private static List<string> ResolveKeyFields(ImportOptions options, ImportProfile profile)
    {
        var source = options.KeyFields.Count > 0
            ? options.KeyFields
            : profile.KeyFields;

        return source
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void EnsureTableExists(ImportProfile profile, ConnectionSettings connectionSettings)
    {
        if (string.IsNullOrWhiteSpace(connectionSettings.ConnectionString))
        {
            throw new InvalidOperationException("A connection string is required to create the table.");
        }

        if (string.IsNullOrWhiteSpace(profile.TableName))
        {
            throw new InvalidOperationException("Profile.TableName must be provided before ensuring the table exists.");
        }

        var schemaName = profile.TableSchema;
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            schemaName = connectionSettings.Schema;
            profile.TableSchema = schemaName;
        }

        using var connection = new SqlConnection(connectionSettings.ConnectionString);
        connection.Open();

        const string existsSql = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;";

        using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = existsSql;
            existsCommand.Parameters.AddWithValue("@schema", schemaName);
            existsCommand.Parameters.AddWithValue("@table", profile.TableName);

            var exists = Convert.ToInt32(existsCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
            if (exists)
            {
                return;
            }
        }

        var tableSchema = TableSchemaBuilder.FromProfile(profile);
        tableSchema.SchemaName = schemaName;

        var dialect = new SqlServerDialect();
        var ddl = dialect.GenerateCreateTable(tableSchema);

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = ddl;
        createCommand.ExecuteNonQuery();
    }
}

public sealed record ImportExecutionResult(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    string OutputPath,
    bool WroteToDatabase,
    IReadOnlyList<ImportRowResult> SampleErrors);

public sealed record ImportLogEvent(string Level, string Message);

public sealed record FilePreviewResult(
    IReadOnlyList<string> Headers,
    IReadOnlyList<string?[]> SampleRows,
    IReadOnlyList<TargetField> InferredFields);
