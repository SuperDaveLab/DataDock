using System.Collections.Generic;

namespace SchemaViz.Gui.Models;

public sealed class ForeignKeyInfo
{
    private readonly List<ColumnLink> _columnLinks = new();

    public ForeignKeyInfo(
        string constraintName,
        string fromSchema,
        string fromTable,
        string toSchema,
        string toTable,
        IEnumerable<ColumnLink>? columnLinks = null)
    {
        ConstraintName = constraintName;
        FromSchema = fromSchema;
        FromTable = fromTable;
        ToSchema = toSchema;
        ToTable = toTable;

        if (columnLinks is not null)
        {
            _columnLinks.AddRange(columnLinks);
        }
    }

    public string ConstraintName { get; }
    public string FromSchema { get; }
    public string FromTable { get; }
    public string ToSchema { get; }
    public string ToTable { get; }
    public IReadOnlyList<ColumnLink> ColumnLinks => _columnLinks;

    public void AddColumnLink(ColumnLink link)
    {
        _columnLinks.Add(link);
    }
}
