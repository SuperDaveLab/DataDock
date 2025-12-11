using System;
using System.Collections.Generic;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public class TargetFieldFactory
{
    private static readonly HashSet<string> StringTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "char", "nchar", "varchar", "nvarchar", "text", "ntext", "xml", "uniqueidentifier"
    };

    private static readonly HashSet<string> IntTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "bigint", "smallint", "tinyint"
    };

    private static readonly HashSet<string> DecimalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "decimal", "numeric", "money", "smallmoney", "float", "real"
    };

    private static readonly HashSet<string> BoolTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bit"
    };

    private static readonly HashSet<string> DateTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "datetime", "datetime2", "smalldatetime", "datetimeoffset", "time"
    };

    public void PopulateFromDbSchema(ImportProfile profile, DbTableInfo table)
    {
        if (profile.TargetFields.Count > 0)
            return; // already defined in profile

        foreach (var column in table.Columns)
        {
            var field = new TargetField
            {
                Name = column.Name,
                DbColumnName = column.Name,
                FieldType = MapFieldType(column.DataType),
                MaxLength = column.MaxLength,
                IsRequired = !column.IsNullable
            };

            profile.TargetFields.Add(field);
        }
    }

    private static FieldType MapFieldType(string? dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return FieldType.String;

        if (StringTypes.Contains(dataType))
            return FieldType.String;

        if (IntTypes.Contains(dataType))
            return FieldType.Int;

        if (DecimalTypes.Contains(dataType))
            return FieldType.Decimal;

        if (BoolTypes.Contains(dataType))
            return FieldType.Bool;

        if (DateTypes.Contains(dataType))
            return FieldType.DateTime;

        return FieldType.String;
    }
}
