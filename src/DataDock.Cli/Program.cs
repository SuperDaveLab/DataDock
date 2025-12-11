using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataDock.Services.DataSources;
using DataDock.Core.Dialects;
using DataDock.Core.Interfaces;
using DataDock.Core.Models;
using DataDock.Core.Services;
using DataDock.Services;
using Microsoft.Data.SqlClient;

namespace DataDock.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var appConfig = AppConfigLoader.Load();

            if (args.Length > 0 && args[0].Equals("schemagen", StringComparison.OrdinalIgnoreCase))
            {
                return RunSchemaGen(args.Skip(1).ToArray(), appConfig);
            }

            // default: import
            return RunImport(args, appConfig);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static int RunImport(string[] args, AppConfig appConfig)
    {
        try
        {
            var options = ParseArgs(args);
            if (options == null)
            {
                PrintUsage();
                return 1;
            }

            var profile = LoadOrCreateProfile(options, appConfig);
            EnsureTargetFieldsFromInput(profile, options);
            var connectionSettings = ConnectionSettingsResolver.Resolve(options, profile, appConfig);

            if (string.IsNullOrWhiteSpace(profile.TableSchema))
            {
                profile.TableSchema = connectionSettings.Schema;
            }

            if (options.EnsureTable)
            {
                if (string.IsNullOrWhiteSpace(connectionSettings.ConnectionString))
                {
                    Console.Error.WriteLine("No connection string found. Provide --connection-string, set profile.tableConnectionString, or configure datadock.config.json.");
                    return 1;
                }

                EnsureTableExists(profile, connectionSettings);
            }

            Console.WriteLine($"Profile: {(string.IsNullOrWhiteSpace(options.ProfilePath) ? "<none>" : options.ProfilePath)}");
            Console.WriteLine($"Input:   {options.InputPath}");
            Console.WriteLine($"Output:  {options.OutputPath}");
            Console.WriteLine();

            // 2. Create appropriate reader based on file extension
            IDataSourceReader dataReader = CreateDataReader(options.InputPath);
            using (dataReader)
            {
                var headers = dataReader.GetHeaders();

                var sourceColumns = headers
                    .Select((h, idx) => new SourceColumn
                    {
                        HeaderName = h,
                        Index = idx
                    })
                    .ToList();

                // 3. Generate mappings
                var mapper = new DefaultColumnMapper();
                var mappings = mapper.GenerateMappings(profile, sourceColumns);

                // 4. Print mapping summary (as before)
                Console.WriteLine("Column mappings:");
                Console.WriteLine("----------------");

                foreach (var m in mappings)
                {
                    var src = m.SourceColumn != null
                        ? $"{m.SourceColumn.HeaderName} (index {m.SourceColumn.Index})"
                        : "<UNMAPPED>";

                    var autoText = m.IsAutoMapped ? "auto" : "manual/none";

                    Console.WriteLine(
                        $"{m.TargetField.Name,-15} -> {src,-30} [{autoText}]");
                }

                Console.WriteLine();
                Console.WriteLine("Source columns seen in file:");
                foreach (var c in sourceColumns)
                {
                    Console.WriteLine($"  [{c.Index}] {c.HeaderName}");
                }

                Console.WriteLine();
                Console.WriteLine("Processing rows...");

                // 5. Process data rows
                var rowResults = new List<ImportRowResult>();
                var dataRowIndex = 0;

                while (dataReader.Read())
                {
                    dataRowIndex++;
                    var rowResult = new ImportRowResult
                    {
                        RowNumber = dataRowIndex
                    };

                    foreach (var mapping in mappings)
                    {
                        var field = mapping.TargetField;

                        // If there is no source column mapped
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

                            // Required check for empty
                            if (field.IsRequired && profile.StrictRequiredFields)
                            {
                                if (value == null ||
                                    (value is string str && string.IsNullOrWhiteSpace(str)))
                                {
                                    rowResult.Errors.Add(
                                        $"Required field '{field.Name}' is empty.");
                                }
                            }
                        }
                    }

                    rowResults.Add(rowResult);
                }


                // 6. Split valid and invalid rows
                var validRows = rowResults.Where(r => r.IsValid).ToList();
                var invalidRows = rowResults.Where(r => !r.IsValid).ToList();

                Console.WriteLine();
                Console.WriteLine($"Total rows:     {rowResults.Count}");
                Console.WriteLine($"Valid rows:     {validRows.Count}");
                Console.WriteLine($"Rows with errors: {invalidRows.Count}");

                if (invalidRows.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Sample errors (up to first 5 rows with issues):");

                    foreach (var r in invalidRows.Take(5))
                    {
                        Console.WriteLine($"  Row {r.RowNumber}:");
                        foreach (var err in r.Errors)
                        {
                            Console.WriteLine($"    - {err}");
                        }
                    }
                }

                // 7. Write valid rows to JSON
                Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath!) ?? ".");

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                // Just serialize the Values dictionary for each row
                var payload = validRows.Select(r => r.Values);

                File.WriteAllText(options.OutputPath!, JsonSerializer.Serialize(payload, jsonOptions));

                Console.WriteLine();
                Console.WriteLine($"Wrote {validRows.Count} valid rows to: {options.OutputPath}");

                // 8. Optionally write to SQL Server
                if (options.WriteToDatabase)
                {
                    if (string.IsNullOrWhiteSpace(connectionSettings.ConnectionString))
                    {
                        Console.Error.WriteLine("No connection string found. Provide --connection-string, set profile.tableConnectionString, or configure datadock.config.json.");
                        return 1;
                    }

                    var resolvedKeyFields = ResolveKeyFields(options, profile);

                    if (options.WriteMode == WriteMode.Upsert && resolvedKeyFields.Count == 0)
                    {
                        Console.Error.WriteLine("Upsert mode requires at least one key field (via --key-fields or profile keyFields).");
                        return 1;
                    }

                    Console.WriteLine();
                    Console.WriteLine($"Writing {validRows.Count} rows to SQL Server using mode {options.WriteMode}...");

                    var writer = new SqlServerDataWriter(profile, options.WriteMode, resolvedKeyFields, connectionSettings.Schema);
                    writer.WriteRows(connectionSettings.ConnectionString!, validRows);

                    Console.WriteLine("Database write completed.");
                }
            } // end using dataReader

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static IDataSourceReader CreateDataReader(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".csv" => new CsvDataSourceReader(filePath),
            ".xlsx" => new ExcelDataSourceReader(filePath),
            _ => throw new NotSupportedException($"File type '{extension}' is not supported. Supported types: .csv, .xlsx")
        };
    }

    private static int RunSchemaGen(string[] args, AppConfig appConfig)
{
    if (ParseSchemaGenArgs(args) is not SchemaGenOptions options)
    {
        PrintSchemaGenUsage();
        return 1;
    }

    var profile = string.IsNullOrWhiteSpace(options.ProfilePath)
        ? new ImportProfile()
        : LoadProfile(options.ProfilePath!);

    ApplySchemaGenDefaults(profile, options, appConfig);
    var schemaSource = options.SchemaSourcePath ?? options.InputPath;

    if (!profile.TargetFields.Any())
    {
        if (string.IsNullOrWhiteSpace(schemaSource))
        {
            Console.Error.WriteLine("Unable to infer schema. Provide --input or ensure the profile defines targetFields.");
            return 1;
        }

        var inferred = InferTargetFieldsFromFile(schemaSource);
        if (inferred.Count == 0)
        {
            Console.Error.WriteLine($"Schema inference found no columns in '{schemaSource}'.");
            return 1;
        }

        foreach (var field in inferred)
        {
            profile.TargetFields.Add(field);
        }

        Console.WriteLine($"Inferred {profile.TargetFields.Count} fields from {schemaSource}.");
    }

    var schema = TableSchemaBuilder.FromProfile(profile);

    ISqlDialect sqlDialect = options.Dialect switch
    {
        "sqlserver" => new SqlServerDialect(),
        // "postgres"  => new PostgresDialect(),
        // "mysql"     => new MySqlDialect(),
        _            => new SqlServerDialect()
    };

    var sql = sqlDialect.GenerateCreateTable(schema);

    if (!string.IsNullOrWhiteSpace(options.OutputPath))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
        File.WriteAllText(options.OutputPath!, sql);
        Console.WriteLine($"Wrote CREATE TABLE script to: {options.OutputPath}");
    }
    else
    {
        Console.WriteLine(sql);
    }

    return 0;
}

    private static void EnsureTableExists(ImportProfile profile, ConnectionSettings connectionSettings)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (connectionSettings == null)
            throw new ArgumentNullException(nameof(connectionSettings));
        if (string.IsNullOrWhiteSpace(connectionSettings.ConnectionString))
            throw new InvalidOperationException("A connection string is required to create the table.");
        if (string.IsNullOrWhiteSpace(profile.TableName))
            throw new InvalidOperationException("Profile.TableName must be provided before ensuring the table exists.");

        var schemaName = profile.TableSchema;
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            schemaName = connectionSettings.Schema;
            profile.TableSchema = schemaName;
        }

        using var connection = new SqlConnection(connectionSettings.ConnectionString);
        connection.Open();

        const string existsSql = @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;";

        using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = existsSql;
            existsCommand.Parameters.AddWithValue("@schema", schemaName);
            existsCommand.Parameters.AddWithValue("@table", profile.TableName);

            var exists = Convert.ToInt32(existsCommand.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
            if (exists)
            {
                Console.WriteLine($"Table [{schemaName}].[{profile.TableName}] already exists.");
                return;
            }
        }

        var tableSchema = TableSchemaBuilder.FromProfile(profile);
        tableSchema.SchemaName = schemaName;

        var dialect = new SqlServerDialect();
        var ddl = dialect.GenerateCreateTable(tableSchema);

        using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = ddl;
            createCommand.ExecuteNonQuery();
        }

        Console.WriteLine($"Created table [{schemaName}].[{profile.TableName}].");
    }

  private static ImportProfile LoadProfile(string path)
  {
    if (!File.Exists(path))
        throw new FileNotFoundException($"Profile not found: {path}");

    var json = File.ReadAllText(path);
    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    options.Converters.Add(new JsonStringEnumConverter());

    var profile = JsonSerializer.Deserialize<ImportProfile>(json, options);

    if (profile == null)
        throw new InvalidOperationException("Unable to deserialize import profile.");

    return profile;
}

    private static List<string> ResolveKeyFields(CliOptions options, ImportProfile profile)
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
private static CliOptions? ParseArgs(string[] args)
{
    string? profile = null;
    string? input = null;
    string? output = null;
    string? connectionString = null;
    string? dbSchema = null;
    string? tableName = null;
    ColumnNameStyle? columnStyle = null;
    var keyFields = new List<string>();
    var writeMode = WriteMode.Insert;
    var writeDb = false;
    var ensureTable = false;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--profile" when i + 1 < args.Length:
                profile = args[++i];
                break;
            case "--input" when i + 1 < args.Length:
                input = args[++i];
                break;
            case "--output" when i + 1 < args.Length:
                output = args[++i];
                break;
            case "--write-mode" when i + 1 < args.Length:
                var writeModeArg = args[++i];
                if (!WriteModeParser.TryParse(writeModeArg, out writeMode))
                {
                    Console.Error.WriteLine($"Unknown write mode '{writeModeArg}'. Supported values: insert, truncate-insert, upsert.");
                    return null;
                }
                break;
            case "--key-fields" when i + 1 < args.Length:
                var keysArg = args[++i];
                keyFields.AddRange(
                    keysArg
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                break;
            case "--write-db":
                writeDb = true;
                break;
            case "--ensure-table":
                ensureTable = true;
                break;
            case "--connection-string" when i + 1 < args.Length:
                connectionString = args[++i];
                break;
            case "--db-schema" when i + 1 < args.Length:
                dbSchema = args[++i];
                break;
            case "--table" when i + 1 < args.Length:
                tableName = args[++i];
                break;
            case "--column-style" when i + 1 < args.Length:
                var styleArg = args[++i];
                if (!TryParseColumnNameStyle(styleArg, out var style))
                {
                    Console.Error.WriteLine($"Unknown column style '{styleArg}'. Supported values: asis, camelcase, pascalcase, snakecase, titlewithspaces.");
                    return null;
                }
                columnStyle = style;
                break;
            default:
                Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                return null;
        }
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        return null;
    }

    if (string.IsNullOrWhiteSpace(output))
    {
        output = input + ".out.json";
    }

    return new CliOptions
    {
        ProfilePath = profile,
        InputPath = input,
        OutputPath = output,
        ConnectionString = connectionString,
        WriteMode = writeMode,
        WriteToDatabase = writeDb,
    EnsureTable = ensureTable,
        KeyFields = keyFields,
        DatabaseSchema = dbSchema,
        TableName = tableName,
        ColumnNameStyleOverride = columnStyle
    };
}

    private static SchemaGenOptions? ParseSchemaGenArgs(string[] args)
    {
        string? profile = null;
        string? input = null;
        string? output = null;
        string dialect = "sqlserver";
        string? tableName = null;
        ColumnNameStyle? columnStyle = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--profile" when i + 1 < args.Length:
                    profile = args[++i];
                    break;
                case "--input" when i + 1 < args.Length:
                    input = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--dialect" when i + 1 < args.Length:
                    dialect = args[++i].ToLowerInvariant();
                    break;
                case "--table" when i + 1 < args.Length:
                    tableName = args[++i];
                    break;
                case "--column-style" when i + 1 < args.Length:
                    var styleArg = args[++i];
                    if (!TryParseColumnNameStyle(styleArg, out var style))
                    {
                        Console.Error.WriteLine($"Unknown column style '{styleArg}'. Supported values: asis, camelcase, pascalcase, snakecase, titlewithspaces.");
                        return null;
                    }
                    columnStyle = style;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(profile) && string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return new SchemaGenOptions
        {
            ProfilePath = profile,
            InputPath = input,
            OutputPath = output,
            Dialect = dialect,
            TableName = tableName,
            ColumnStyleOverride = columnStyle,
            SchemaSourcePath = input
        };
    }

    private static ImportProfile LoadOrCreateProfile(CliOptions options, AppConfig appConfig)
    {
        var hasProfile = !string.IsNullOrWhiteSpace(options.ProfilePath);
        var profile = hasProfile
            ? LoadProfile(options.ProfilePath!)
            : new ImportProfile();

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
            profile.ColumnNameStyle = appConfig.Defaults.ColumnNameStyle;
        }

        if (string.IsNullOrWhiteSpace(profile.TableSchema) && !string.IsNullOrWhiteSpace(options.DatabaseSchema))
        {
            profile.TableSchema = options.DatabaseSchema;
        }

        return profile;
    }

    private static void EnsureTargetFieldsFromInput(ImportProfile profile, CliOptions options)
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

        Console.WriteLine($"Inferred {profile.TargetFields.Count} target fields from {Path.GetFileName(options.InputPath)}");
        Console.WriteLine();
    }


    private static void ApplySchemaGenDefaults(ImportProfile profile, SchemaGenOptions options, AppConfig appConfig)
    {
        var hasProfile = !string.IsNullOrWhiteSpace(options.ProfilePath);

        if (!string.IsNullOrWhiteSpace(options.TableName))
        {
            profile.TableName = options.TableName;
        }
        else if (string.IsNullOrWhiteSpace(profile.TableName) && !string.IsNullOrWhiteSpace(options.InputPath))
        {
            profile.TableName = DeriveTableNameFromInput(options.InputPath);
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = profile.TableName ?? options.TableName ?? DeriveTableNameFromInput(options.InputPath) ?? "DataDock Schema";
        }

        if (options.ColumnStyleOverride.HasValue)
        {
            profile.ColumnNameStyle = options.ColumnStyleOverride.Value;
        }
        else if (!hasProfile)
        {
            profile.ColumnNameStyle = appConfig.Defaults.ColumnNameStyle;
        }
    }

    private static IReadOnlyList<TargetField> InferTargetFieldsFromFile(string filePath, int sampleRowLimit = 1000)
    {
        IDataSourceReader reader = CreateDataReader(filePath);
        using (reader)
        {
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
    }

    private static string? DeriveTableNameFromInput(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        return Path.GetFileNameWithoutExtension(inputPath);
    }

    private static bool TryParseColumnNameStyle(string value, out ColumnNameStyle style)
    {
        return Enum.TryParse(value, true, out style);
    }

    private static void PrintSchemaGenUsage()
    {
        Console.WriteLine("Usage: datadock schemagen [--profile profile.json] [--input data.csv|data.xlsx] --table <TableName> [--column-style style] [--output file.sql] [--dialect sqlserver]");
        Console.WriteLine("Notes:");
        Console.WriteLine("  • If no profile is provided, --input is required so the schema can be inferred.");
        Console.WriteLine("  • Column name style defaults to datadock.config.json unless overridden with --column-style.");
        Console.WriteLine();
    }

    private sealed class SchemaGenOptions
    {
        public string? ProfilePath { get; init; }
        public string? InputPath { get; init; }
        public string? OutputPath { get; init; }
        public string Dialect { get; init; } = "sqlserver";
        public string? TableName { get; init; }
        public ColumnNameStyle? ColumnStyleOverride { get; init; }
        public string? SchemaSourcePath { get; init; }
    }

    private static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  datadock [--profile <profile.json>] --input <data.csv|data.xlsx> [--output <output.json>] [--table <TableName>] [--column-style style]");
    Console.WriteLine("  datadock schemagen [--profile <profile.json>] [--input <data.csv|data.xlsx>] --table <TableName> [--column-style style] [--output <file.sql>] [--dialect sqlserver|postgres|mysql]");
    Console.WriteLine("Options:");
    Console.WriteLine("  --write-mode insert|truncate-insert|upsert");
    Console.WriteLine("  --key-fields Field1,Field2,...");
    Console.WriteLine("  --write-db");
    Console.WriteLine("  --ensure-table (creates table if missing before writing)");
    Console.WriteLine("  --connection-string <sql-connection-string>");
    Console.WriteLine("  --db-schema <schema>");
    Console.WriteLine("  --table <tableName> (defaults to input file name when omitted)");
    Console.WriteLine("  --column-style asis|camelcase|pascalcase|snakecase|kebabcase|titlewithspaces");
    Console.WriteLine();
    Console.WriteLine("Global config:");
    Console.WriteLine("  datadock.config.json (cwd or parent directories), ~/.datadock/config.json, /etc/datadock/config.json");
    Console.WriteLine("  Connection string priority: --connection-string > profile.tableConnectionString > config database.defaultConnectionString");
    Console.WriteLine("  Schema priority: --db-schema > profile.tableSchema > config database.defaultSchema > dbo");
    Console.WriteLine("  Column style priority: --column-style > profile.ColumnNameStyle > config.defaults.columnNameStyle");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  datadock --input samples/tickets.xlsx --output out/tickets.json --table Tickets");
    Console.WriteLine("  datadock --profile profiles/tickets.json --input samples/tickets.csv --output out/tickets.json --write-db");
    Console.WriteLine("  datadock schemagen --input samples/tickets.xlsx --table Tickets --output out/tickets.sql");
}
}
