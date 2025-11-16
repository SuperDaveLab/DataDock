namespace DataDock.Core.Models;

public class ImportRowResult
{
    public int RowNumber { get; set; } // 1-based data row (excluding header)

    // TargetField.Name -> typed value
    public Dictionary<string, object?> Values { get; } = new();

    // Human-readable error messages for this row
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
