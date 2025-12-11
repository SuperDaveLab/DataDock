using System;
using System.Collections.Generic;
using System.Linq;
using SchemaViz.Gui.Models;

namespace SchemaViz.Gui.ViewModels.Diagram;

public sealed class RelationshipViewModel : ViewModelBase
{
    public RelationshipViewModel(
        TableNodeViewModel from,
        TableNodeViewModel to,
        string foreignKeyName,
        IReadOnlyList<ColumnLink> columnLinks)
    {
        From = from;
        To = to;
        ForeignKeyName = foreignKeyName;
        ColumnLinks = columnLinks;
    }

    public TableNodeViewModel From { get; }
    public TableNodeViewModel To { get; }
    public string ForeignKeyName { get; }
    public IReadOnlyList<ColumnLink> ColumnLinks { get; }

    public string ChildToParentColumnSummary => BuildColumnSummary(link => $"{link.ToColumn} → {link.FromColumn}");

    private string BuildColumnSummary(Func<ColumnLink, string> formatter)
    {
        if (ColumnLinks.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", ColumnLinks.Select(formatter));
    }
}
