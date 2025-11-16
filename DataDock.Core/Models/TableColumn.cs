namespace DataDock.Core.Models;

public class TableColumn
{
    public string Name { get; set; } = string.Empty;   // DB column name
    public FieldType FieldType { get; set; }
    public int? MaxLength { get; set; }
    public bool IsRequired { get; set; }
}
