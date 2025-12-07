namespace DataDock.Gui.Models;

public sealed class SampleRowPreview
{
    public SampleRowPreview(int rowNumber, string displayText)
    {
        RowNumber = rowNumber;
        DisplayText = displayText;
    }

    public int RowNumber { get; }
    public string DisplayText { get; }
}
