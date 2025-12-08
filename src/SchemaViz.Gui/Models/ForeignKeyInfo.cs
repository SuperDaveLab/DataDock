namespace SchemaViz.Gui.Models;

public sealed class ForeignKeyInfo
{
    public ForeignKeyInfo(string constraintName, string fromSchema, string fromTable, string toSchema, string toTable)
    {
        ConstraintName = constraintName;
        FromSchema = fromSchema;
        FromTable = fromTable;
        ToSchema = toSchema;
        ToTable = toTable;
    }

    public string ConstraintName { get; }
    public string FromSchema { get; }
    public string FromTable { get; }
    public string ToSchema { get; }
    public string ToTable { get; }
}
