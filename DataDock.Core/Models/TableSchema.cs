namespace DataDock.Core.Models;

public class TableSchema
{
    public string TableName { get; set; } = string.Empty;
    public List<TableColumn> Columns { get; set; } = new();
}