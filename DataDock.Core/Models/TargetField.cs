namespace DataDock.Core.Models;

public class TargetField
{
    public string Name { get; set; } = string.Empty;   // e.g. "JobNumber"
    public FieldType FieldType { get; set; }           // e.g. String
    public bool IsRequired { get; set; }
    public int? MaxLength { get; set; }                // for string fields
}
