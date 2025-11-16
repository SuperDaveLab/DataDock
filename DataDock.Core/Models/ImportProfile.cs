namespace DataDock.Core.Models;

public class ImportProfile
{
    public string Name { get; set; } = string.Empty;

    public List<TargetField> TargetFields { get; set; } = new();
    public List<ColumnAlias> Aliases { get; set; } = new();

    public bool StrictRequiredFields { get; set; } = true;

    // NEW:
    public string? TableName { get; set; }          // e.g. "Tickets"
    public ColumnNameStyle ColumnNameStyle { get; set; } = ColumnNameStyle.SnakeCase;

    // Later we can add:
    // public string? DatabaseDialect { get; set; }  // "SqlServer", "Postgres", "MySql", etc.
}
