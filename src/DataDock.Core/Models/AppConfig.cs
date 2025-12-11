namespace DataDock.Core.Models;

public class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public DefaultsConfig Defaults { get; set; } = new();
}

public class DatabaseConfig
{
    public string? DefaultConnectionString { get; set; }
    public string DefaultSchema { get; set; } = "dbo";
}

public class DefaultsConfig
{
    public ColumnNameStyle ColumnNameStyle { get; set; } = ColumnNameStyle.SnakeCase;
    public string StringLengthStrategy { get; set; } = "MaxObservedRounded";
}
