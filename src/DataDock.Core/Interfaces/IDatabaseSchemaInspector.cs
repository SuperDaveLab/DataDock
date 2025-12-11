using DataDock.Core.Models;

namespace DataDock.Core.Interfaces;

public interface IDatabaseSchemaInspector
{
    DbTableInfo GetTableSchema(string connectionString, string schemaName, string tableName);
}
