namespace SchemaViz.Gui.ViewModels.Diagram;

public sealed class RelationshipViewModel : ViewModelBase
{
    public RelationshipViewModel(TableNodeViewModel from, TableNodeViewModel to, string? label = null)
    {
        From = from;
        To = to;
        Label = label;
    }

    public TableNodeViewModel From { get; }
    public TableNodeViewModel To { get; }
    public string? Label { get; }
}
