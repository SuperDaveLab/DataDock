using System.Text.Json;
using System.Text.Json.Serialization;
using DataDock.Cli.DataSources;
using DataDock.Core.Dialects;
using DataDock.Core.Interfaces;
using DataDock.Core.Models;
using DataDock.Core.Services;

namespace DataDock.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0].Equals("schemagen", StringComparison.OrdinalIgnoreCase))
            {
                return RunSchemaGen(args.Skip(1).ToArray());
            }

            // default: import
            return RunImport(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static int RunImport(string[] args)
    {
        try
        {
            var appConfig = AppConfigLoader.Load();
            var options = ParseArgs(args);
            if (options == null)
            {
                PrintUsage();
                return 1;
            }

            Console.WriteLine($"Profile: {options.ProfilePath}");
            Console.WriteLine($"Input:   {options.InputPath}");
            Console.WriteLine($"Output:  {options.OutputPath}");
            Console.WriteLine();

            // 1. Load profile
            var profile = LoadProfile(options.ProfilePath);
            var connectionSettings = ConnectionSettingsResolver.Resolve(options, profile, appConfig);

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

    private static int RunSchemaGen(string[] args)
{
    // Expect: --profile <file> [--dialect <sqlserver|postgres|mysql>] [--output <file>]

    string? profilePath = null;
    string dialect = "sqlserver";
    string? output = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--profile" when i + 1 < args.Length:
                profilePath = args[++i];
                break;

            case "--dialect" when i + 1 < args.Length:
                dialect = args[++i].ToLowerInvariant();
                break;

            case "--output" when i + 1 < args.Length:
                output = args[++i];
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(profilePath))
    {
        Console.WriteLine("Usage: datadock schemagen --profile <profile.json> [--dialect sqlserver|postgres|mysql] [--output <file.sql>]");
        return 1;
    }

    var profile = LoadProfile(profilePath);

    var schema = TableSchemaBuilder.FromProfile(profile);

    ISqlDialect sqlDialect = dialect switch
    {
        "sqlserver" => new SqlServerDialect(),
        // "postgres"  => new PostgresDialect(),
        // "mysql"     => new MySqlDialect(),
        _            => new SqlServerDialect()
    };

    var sql = sqlDialect.GenerateCreateTable(schema);

    if (!string.IsNullOrWhiteSpace(output))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, sql);
        Console.WriteLine($"Wrote CREATE TABLE script to: {output}");
    }
    else
    {
        Console.WriteLine(sql);
    }

    return 0;
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
    var keyFields = new List<string>();
    var writeMode = WriteMode.Insert;
    var writeDb = false;

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
            case "--connection-string" when i + 1 < args.Length:
                connectionString = args[++i];
                break;
            case "--db-schema" when i + 1 < args.Length:
                dbSchema = args[++i];
                break;
            default:
                Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                return null;
        }
    }

    if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(input))
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
        KeyFields = keyFields,
        DatabaseSchema = dbSchema
    };
}

    private static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  datadock --profile <profile.json> --input <data.csv|data.xlsx> [--output <output.json>]");
    Console.WriteLine("  datadock schemagen --profile <profile.json> [--dialect sqlserver|postgres|mysql] [--output <file.sql>]");
    Console.WriteLine("Options:");
    Console.WriteLine("  --write-mode insert|truncate-insert|upsert");
    Console.WriteLine("  --key-fields Field1,Field2,...");
    Console.WriteLine("  --write-db");
    Console.WriteLine("  --connection-string <sql-connection-string>");
    Console.WriteLine("  --db-schema <schema>");
    Console.WriteLine();
    Console.WriteLine("Global config:");
    Console.WriteLine("  datadock.config.json (cwd), ~/.datadock/config.json, /etc/datadock/config.json");
    Console.WriteLine("  Connection string priority: --connection-string > profile.tableConnectionString > config database.defaultConnectionString");
    Console.WriteLine("  Schema priority: --db-schema > profile.tableSchema > config database.defaultSchema > dbo");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  datadock --profile profiles/tickets.json --input samples/tickets.csv --output out/tickets.json");
    Console.WriteLine("  datadock --profile profiles/tickets.json --input samples/tickets.xlsx --output out/tickets.json");
    Console.WriteLine("  datadock schemagen --profile profiles/tickets.json --dialect sqlserver --output out/tickets.sql");
}
}
