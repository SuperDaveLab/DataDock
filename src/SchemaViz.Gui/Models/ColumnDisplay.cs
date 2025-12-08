namespace SchemaViz.Gui.Models;

public sealed class ColumnDisplay
{
    public ColumnDisplay(string name, string dataType, string lengthDisplay, bool isNullable)
    {
        Name = name;
        DataType = dataType;
        LengthDisplay = lengthDisplay;
        IsNullable = isNullable;
    }

    public string Name { get; }
    public string DataType { get; }
    public string LengthDisplay { get; }
    public bool IsNullable { get; }

    public string NullableDisplay => IsNullable ? "Yes" : "No";
}
