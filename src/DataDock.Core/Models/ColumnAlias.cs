namespace DataDock.Core.Models;

public class ColumnAlias
{
    public string TargetFieldName { get; set; } = string.Empty; // e.g. "JobNumber"
    public string Alias { get; set; } = string.Empty;           // e.g. "Job #", "Job Num"
}
