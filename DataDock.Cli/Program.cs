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

                        // String max length enforcement
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
private static CliOptions? ParseArgs(string[] args)
{
    string? profile = null;
    string? input = null;
    string? output = null;

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
        }
    }

    if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(input))
    {
        return null;
    }

    // Default output: <input>.out.json
    if (string.IsNullOrWhiteSpace(output))
    {
        output = input + ".out.json";
    }

    return new CliOptions
    {
        ProfilePath = profile,
        InputPath = input,
        OutputPath = output
    };
}

    private static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  datadock --profile <profile.json> --input <data.csv|data.xlsx> [--output <output.json>]");
    Console.WriteLine("  datadock schemagen --profile <profile.json> [--dialect sqlserver|postgres|mysql] [--output <file.sql>]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  datadock --profile profiles/tickets.json --input samples/tickets.csv --output out/tickets.json");
    Console.WriteLine("  datadock --profile profiles/tickets.json --input samples/tickets.xlsx --output out/tickets.json");
    Console.WriteLine("  datadock schemagen --profile profiles/tickets.json --dialect sqlserver --output out/tickets.sql");
}

  private class CliOptions
  {
    public string ProfilePath { get; set; } = string.Empty;
    public string InputPath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
  }
}
