namespace DataDock.Core.Models;

public class SourceColumn
{
    public string HeaderName { get; set; } = string.Empty; // actual column header in file
    public int Index { get; set; }                         // zero-based column index
}
