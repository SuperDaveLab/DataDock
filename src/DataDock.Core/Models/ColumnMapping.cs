namespace DataDock.Core.Models;

public class ColumnMapping
{
    public TargetField TargetField { get; set; } = default!;
    public SourceColumn? SourceColumn { get; set; } // null if unmapped
    public bool IsAutoMapped { get; set; }
}
