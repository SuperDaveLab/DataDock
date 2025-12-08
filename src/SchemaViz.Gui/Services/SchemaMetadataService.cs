using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataDock.Core.Models;
using DataDock.Core.Services;
using Microsoft.Data.SqlClient;
using SchemaViz.Gui.Models;

namespace SchemaViz.Gui.Services;

public sealed class SchemaMetadataService
{
    private readonly SqlServerSchemaInspector _inspector = new();

    public async Task TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TableListItem>> GetTablesAsync(
        string connectionString,
        string? schemaFilter,
        CancellationToken cancellationToken = default)
    {
        const string baseSql = @"
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    SUM(p.rows) AS TotalRows
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
LEFT JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
";

        var sql = string.IsNullOrWhiteSpace(schemaFilter)
            ? baseSql + "GROUP BY s.name, t.name ORDER BY s.name, t.name;"
            : baseSql + "AND s.name = @schema GROUP BY s.name, t.name ORDER BY s.name, t.name;";

        var tables = new List<TableListItem>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            command.Parameters.AddWithValue("@schema", schemaFilter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(reader.GetOrdinal("SchemaName"));
            var name = reader.GetString(reader.GetOrdinal("TableName"));
            long? rowCount = null;
            if (!reader.IsDBNull(reader.GetOrdinal("TotalRows")))
            {
                rowCount = reader.GetInt64(reader.GetOrdinal("TotalRows"));
            }

            tables.Add(new TableListItem(schema, name, rowCount));
        }

        return tables;
    }

    public async Task<DbTableInfo> GetTableSchemaAsync(
        string connectionString,
        string schema,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => _inspector.GetTableSchema(connectionString, schema, tableName), cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignKeyInfo>> GetForeignKeysAsync(
        string connectionString,
        string? schemaFilter,
        CancellationToken cancellationToken = default)
    {
        const string baseSql = @"
SELECT
    fk.name AS ConstraintName,
    sch_from.name AS FromSchema,
    t_from.name AS FromTable,
    sch_to.name AS ToSchema,
    t_to.name AS ToTable
FROM sys.foreign_keys fk
JOIN sys.tables t_from ON fk.parent_object_id = t_from.object_id
JOIN sys.schemas sch_from ON t_from.schema_id = sch_from.schema_id
JOIN sys.tables t_to ON fk.referenced_object_id = t_to.object_id
JOIN sys.schemas sch_to ON t_to.schema_id = sch_to.schema_id
WHERE fk.is_disabled = 0 AND fk.is_ms_shipped = 0
";

        var sql = string.IsNullOrWhiteSpace(schemaFilter)
            ? baseSql + "ORDER BY sch_from.name, t_from.name;"
            : baseSql + "AND sch_from.name = @schema ORDER BY sch_from.name, t_from.name;";

        var relations = new List<ForeignKeyInfo>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            command.Parameters.AddWithValue("@schema", schemaFilter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            relations.Add(new ForeignKeyInfo(
                reader.GetString(reader.GetOrdinal("ConstraintName")),
                reader.GetString(reader.GetOrdinal("FromSchema")),
                reader.GetString(reader.GetOrdinal("FromTable")),
                reader.GetString(reader.GetOrdinal("ToSchema")),
                reader.GetString(reader.GetOrdinal("ToTable"))));
        }

        return relations;
    }
}
