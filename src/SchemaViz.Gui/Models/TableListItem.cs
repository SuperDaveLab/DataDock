namespace SchemaViz.Gui.Models;

public sealed class TableListItem
{
    public TableListItem(string schema, string name, long? rowCount)
    {
        Schema = schema;
        Name = name;
        RowCount = rowCount;
    }

    public string Schema { get; }
    public string Name { get; }
    public long? RowCount { get; }

    public string DisplayName => $"[{Schema}].[{Name}]";
    public string RowCountDisplay => RowCount.HasValue ? $"{RowCount:N0} rows" : "Row count unavailable";
}
