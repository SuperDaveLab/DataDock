using DataDock.Core.Models;

namespace DataDock.Gui.Models;

public sealed class ColumnStyleOption
{
    public ColumnStyleOption(ColumnNameStyle style, string label, string example)
    {
        Style = style;
        Label = label;
        Example = example;
    }

    public ColumnNameStyle Style { get; }
    public string Label { get; }
    public string Example { get; }
}
