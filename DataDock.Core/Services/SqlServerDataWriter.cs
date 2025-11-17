using System;
using System.Collections.Generic;
using System.Linq;
using DataDock.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataDock.Core.Services;

public class SqlServerDataWriter
{
    private readonly ImportProfile _profile;
    private readonly WriteMode _writeMode;
    private readonly IReadOnlyList<ColumnBinding> _columns;
    private readonly IReadOnlyList<ColumnBinding> _keyColumns;
    private readonly IReadOnlyList<ColumnBinding> _nonKeyColumns;
    private readonly string _qualifiedTableName;
    private readonly string _insertSql;
    private readonly string? _updateSql;
    private readonly string _truncateSql;

    public SqlServerDataWriter(
        ImportProfile profile,
        WriteMode writeMode,
        IEnumerable<string> keyFieldNames,
        string? schemaOverride = null)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _writeMode = writeMode;

        var (schemaName, tableName) = ResolveSchemaAndTable(profile, schemaOverride);
        _qualifiedTableName = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
        _truncateSql = $"TRUNCATE TABLE {_qualifiedTableName};";

        var comparer = StringComparer.OrdinalIgnoreCase;
        var requestedKeys = new HashSet<string>(keyFieldNames ?? Array.Empty<string>(), comparer);

        _columns = profile.TargetFields
            .Select((field, index) =>
            {
                var columnName = ColumnNameGenerator.ToColumnName(field.Name, profile.ColumnNameStyle);
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    throw new InvalidOperationException($"Unable to derive column name for target field '{field.Name}'.");
                }

                return new ColumnBinding(
                    field,
                    columnName,
                    InsertParameterName: $"@p{index}",
                    UpdateParameterName: $"@set{index}",
                    WhereParameterName: $"@w{index}");
            })
            .ToList();

        if (_columns.Count == 0)
        {
            throw new InvalidOperationException("Import profile does not define any target fields to write.");
        }

        _keyColumns = _columns
            .Where(c => requestedKeys.Contains(c.Field.Name))
            .ToList();

        if (_writeMode == WriteMode.Upsert && _keyColumns.Count == 0)
        {
            throw new InvalidOperationException("Upsert mode requires at least one key field.");
        }

        var knownKeyNames = new HashSet<string>(_keyColumns.Select(c => c.Field.Name), comparer);
        var missingKeys = requestedKeys.Except(knownKeyNames, comparer).ToList();
        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException($"Key field(s) not found in profile: {string.Join(", ", missingKeys)}");
        }

        _nonKeyColumns = _columns
            .Where(c => !knownKeyNames.Contains(c.Field.Name))
            .ToList();

        _insertSql = BuildInsertSql();
        _updateSql = _writeMode == WriteMode.Upsert
            ? BuildUpdateSql()
            : null;
    }

    public void WriteRows(string connectionString, IEnumerable<ImportRowResult> rows)
    {
        if (connectionString == null)
            throw new ArgumentNullException(nameof(connectionString));
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        if (_writeMode == WriteMode.TruncateInsert)
        {
            ExecuteNonQuery(connection, _truncateSql);
        }

        foreach (var row in rows)
        {
            if (row == null)
            {
                continue;
            }

            WriteRow(connection, row);
        }
    }

    private void WriteRow(SqlConnection connection, ImportRowResult row)
    {
        switch (_writeMode)
        {
            case WriteMode.Insert:
            case WriteMode.TruncateInsert:
                ExecuteInsert(connection, row);
                break;
            case WriteMode.Upsert:
                if (!TryExecuteUpdate(connection, row))
                {
                    ExecuteInsert(connection, row);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ExecuteInsert(SqlConnection connection, ImportRowResult row)
    {
        using var command = connection.CreateCommand();
        command.CommandText = _insertSql;

        foreach (var column in _columns)
        {
            var value = GetValue(row, column.Field.Name);
            command.Parameters.AddWithValue(column.InsertParameterName, value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    private bool TryExecuteUpdate(SqlConnection connection, ImportRowResult row)
    {
        if (_updateSql == null)
        {
            return false;
        }

        if (_nonKeyColumns.Count == 0)
        {
            return false;
        }

        using var command = connection.CreateCommand();
        command.CommandText = _updateSql;

        foreach (var column in _nonKeyColumns)
        {
            var value = GetValue(row, column.Field.Name);
            command.Parameters.AddWithValue(column.UpdateParameterName!, value ?? DBNull.Value);
        }

        foreach (var column in _keyColumns)
        {
            var value = GetValue(row, column.Field.Name);
            command.Parameters.AddWithValue(column.WhereParameterName!, value ?? DBNull.Value);
        }

        var affected = command.ExecuteNonQuery();
        return affected > 0;
    }

    private void ExecuteNonQuery(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? GetValue(ImportRowResult row, string fieldName)
    {
        if (row.Values.TryGetValue(fieldName, out var value))
        {
            return value;
        }

        return null;
    }

    private string BuildInsertSql()
    {
        var columnList = string.Join(", ", _columns.Select(c => QuoteIdentifier(c.ColumnName)));
        var valueList = string.Join(", ", _columns.Select(c => c.InsertParameterName));
        return $"INSERT INTO {_qualifiedTableName} ({columnList}) VALUES ({valueList});";
    }

    private string? BuildUpdateSql()
    {
        if (_nonKeyColumns.Count == 0 || _keyColumns.Count == 0)
        {
            return null;
        }

        var setList = string.Join(", ", _nonKeyColumns.Select(c => $"{QuoteIdentifier(c.ColumnName)} = {c.UpdateParameterName}"));
        var whereList = string.Join(" AND ", _keyColumns.Select(c => $"{QuoteIdentifier(c.ColumnName)} = {c.WhereParameterName}"));
        return $"UPDATE {_qualifiedTableName} SET {setList} WHERE {whereList};";
    }

    private static (string Schema, string Table) ResolveSchemaAndTable(ImportProfile profile, string? schemaOverride)
    {
        if (string.IsNullOrWhiteSpace(profile.TableName))
        {
            throw new InvalidOperationException("ImportProfile.TableName must be provided to write to SQL Server.");
        }

        var trimmed = profile.TableName.Trim();
        string? embeddedSchema = null;
        var tableName = trimmed;

        var parts = trimmed.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            embeddedSchema = parts[0];
            tableName = parts[1];
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException("ImportProfile.TableName must include the table name.");
        }

        string? schema = schemaOverride;
        if (string.IsNullOrWhiteSpace(schema))
        {
            schema = profile.TableSchema;
        }

        if (string.IsNullOrWhiteSpace(schema))
        {
            schema = embeddedSchema;
        }

        if (string.IsNullOrWhiteSpace(schema))
        {
            schema = "dbo";
        }

        return (schema, tableName);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be empty.", nameof(identifier));
        }

        var escaped = identifier.Replace("]", "]]", StringComparison.Ordinal);
        return $"[{escaped}]";
    }

    private sealed record ColumnBinding(
        TargetField Field,
        string ColumnName,
        string InsertParameterName,
        string UpdateParameterName,
        string WhereParameterName);
}
