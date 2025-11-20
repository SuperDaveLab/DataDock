using System;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public static class TableSchemaBuilder
{
    public static TableSchema FromProfile(ImportProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.TableName))
            throw new InvalidOperationException("Profile.TableName is not set.");

        var schema = new TableSchema
        {
            TableName = profile.TableName,
            SchemaName = profile.TableSchema
        };

        foreach (var field in profile.TargetFields)
        {
            var colName = ColumnNameGenerator.ToColumnName(field.Name, profile.ColumnNameStyle);

            schema.Columns.Add(new TableColumn
            {
                Name = colName,
                FieldType = field.FieldType,
                MaxLength = field.MaxLength,
                IsRequired = field.IsRequired
            });
        }

        return schema;
    }
}
