namespace DataDock.Core.Models;

public class DbColumnInfo
{
    public string Name { get; set; } = string.Empty;        // DB column name
    public string DataType { get; set; } = string.Empty;    // e.g. "varchar", "int"
    public int? MaxLength { get; set; }                     // for string-ish types
    public int? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public bool IsNullable { get; set; }
}
