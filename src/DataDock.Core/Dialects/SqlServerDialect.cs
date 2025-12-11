using System;
using System.Text;
using DataDock.Core.Interfaces;
using DataDock.Core.Models;

namespace DataDock.Core.Dialects;

public class SqlServerDialect : ISqlDialect
{
    public string GenerateCreateTable(TableSchema schema)
    {
        var sb = new StringBuilder();
    var qualifiedName = BuildQualifiedName(schema.SchemaName, schema.TableName);
    sb.AppendLine($"CREATE TABLE {qualifiedName} (");

        for (int i = 0; i < schema.Columns.Count; i++)
        {
            var col = schema.Columns[i];
            var line = $"    [{col.Name}] {GetSqlType(col)}";

            if (col.IsRequired)
                line += " NOT NULL";
            else
                line += " NULL";

            if (i < schema.Columns.Count - 1)
                line += ",";

            sb.AppendLine(line);
        }

        sb.AppendLine(");");
        return sb.ToString();
    }

    private static string GetSqlType(TableColumn col)
    {
        return col.FieldType switch
        {
            FieldType.String   => col.MaxLength.HasValue
                ? $"VARCHAR({col.MaxLength.Value})"
                : "VARCHAR(255)",

            FieldType.Int      => "INT",
            FieldType.Decimal  => "DECIMAL(18, 2)",
            FieldType.Bool     => "BIT",
            FieldType.DateTime => "DATETIME2",
            _                  => "VARCHAR(255)"
        };
    }

        private static string BuildQualifiedName(string? schemaName, string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name is required.", nameof(tableName));

            var escapedTable = QuoteIdentifier(tableName);

            if (string.IsNullOrWhiteSpace(schemaName))
            {
                return escapedTable;
            }

            return $"{QuoteIdentifier(schemaName)}.{escapedTable}";
        }

        private static string QuoteIdentifier(string identifier)
        {
            var escaped = identifier.Replace("]", "]]", StringComparison.Ordinal);
            return $"[{escaped}]";
        }
}
