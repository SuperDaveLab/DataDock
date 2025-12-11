using System;
using DataDock.Core.Interfaces;
using DataDock.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataDock.Core.Services;

public class SqlServerSchemaInspector : IDatabaseSchemaInspector
{
    public DbTableInfo GetTableSchema(string connectionString, string schemaName, string tableName)
    {
        var result = new DbTableInfo
        {
            Schema = schemaName,
            Name = tableName
        };

        using var conn = new SqlConnection(connectionString);
        conn.Open();

        const string sql = @"
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_SCHEMA = @schema
  AND c.TABLE_NAME = @table
ORDER BY c.ORDINAL_POSITION;
";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var col = new DbColumnInfo
            {
                Name = reader.GetString(reader.GetOrdinal("COLUMN_NAME")),
                DataType = reader.GetString(reader.GetOrdinal("DATA_TYPE")),
                MaxLength = reader.IsDBNull(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH")),
                NumericPrecision = reader.IsDBNull(reader.GetOrdinal("NUMERIC_PRECISION"))
                    ? null
                    : Convert.ToInt32(reader["NUMERIC_PRECISION"]),
                NumericScale = reader.IsDBNull(reader.GetOrdinal("NUMERIC_SCALE"))
                    ? null
                    : Convert.ToInt32(reader["NUMERIC_SCALE"]),
                IsNullable = string.Equals(
                    reader.GetString(reader.GetOrdinal("IS_NULLABLE")),
                    "YES",
                    StringComparison.OrdinalIgnoreCase)
            };

            result.Columns.Add(col);
        }

        return result;
    }
}
