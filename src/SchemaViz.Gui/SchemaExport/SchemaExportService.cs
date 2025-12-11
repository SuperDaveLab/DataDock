using System;
using System.Collections.Generic;
using System.Text.Json;
using SchemaViz.Gui.ViewModels.Diagram;

namespace SchemaViz.Core.SchemaExport;

public sealed class SchemaExportService
{
    public SchemaExport CreateExport(
        string? databaseName,
        string? schemaFilter,
        IReadOnlyCollection<TableNodeViewModel> tables,
        IReadOnlyCollection<RelationshipViewModel> relationships)
    {
        var export = new SchemaExport
        {
            DatabaseName = databaseName,
            SchemaFilter = schemaFilter,
            GeneratedAtUtc = DateTime.UtcNow
        };

        foreach (var table in tables)
        {
            var tableDto = new SchemaExportTable
            {
                Schema = table.Schema,
                Name = table.Name,
                X = table.X,
                Y = table.Y,
                Width = table.Width,
                Height = table.Height
            };

            foreach (var column in table.Columns)
            {
                tableDto.Columns.Add(new SchemaExportColumn
                {
                    Name = column.Name,
                    DataType = column.DataType,
                    IsPrimaryKey = column.IsPrimaryKey,
                    IsForeignKey = column.IsForeignKey
                });
            }

            export.Tables.Add(tableDto);
        }

        foreach (var relationship in relationships)
        {
            var relationshipDto = new SchemaExportRelationship
            {
                ForeignKeyName = relationship.ForeignKeyName ?? string.Empty,
                FromSchema = relationship.From.Schema,
                FromTable = relationship.From.Name,
                ToSchema = relationship.To.Schema,
                ToTable = relationship.To.Name
            };

            foreach (var link in relationship.ColumnLinks)
            {
                relationshipDto.ColumnLinks.Add(new SchemaExportColumnLink
                {
                    FromColumn = link.FromColumn,
                    ToColumn = link.ToColumn
                });
            }

            export.Relationships.Add(relationshipDto);
        }

        return export;
    }

    public string ToJson(SchemaExport export)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(export, options);
    }
}
