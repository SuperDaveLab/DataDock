namespace DataDock.Core.Interfaces;

public interface IDataWriter
{
    void InsertRows(
        string connectionString,
        string schemaName,
        string tableName,
        List<Dictionary<string, object?>> rows);
}
