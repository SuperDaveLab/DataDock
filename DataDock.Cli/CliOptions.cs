using DataDock.Core.Models;

namespace DataDock.Cli;

internal class CliOptions
{
    public string? ProfilePath { get; set; }
    public string InputPath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public bool WriteToDatabase { get; set; }
    public string? ConnectionString { get; set; }
    public WriteMode WriteMode { get; set; } = WriteMode.Insert;
    public List<string> KeyFields { get; set; } = new();
    public string? DatabaseSchema { get; set; }
    public string? TableName { get; set; }
    public ColumnNameStyle? ColumnNameStyleOverride { get; set; }
    public bool EnsureTable { get; set; }
}
