using System;
using System.Collections.Generic;

namespace SchemaViz.Core.SchemaExport;

public sealed class SchemaExport
{
    public string? DatabaseName { get; set; }
    public string? SchemaFilter { get; set; }
    public DateTime GeneratedAtUtc { get; set; }

    public List<SchemaExportTable> Tables { get; set; } = new();
    public List<SchemaExportRelationship> Relationships { get; set; } = new();
}

public sealed class SchemaExportTable
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public List<SchemaExportColumn> Columns { get; set; } = new();
}

public sealed class SchemaExportColumn
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
}

public sealed class SchemaExportRelationship
{
    public string ForeignKeyName { get; set; } = string.Empty;
    public string FromSchema { get; set; } = string.Empty;
    public string FromTable { get; set; } = string.Empty;
    public string ToSchema { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;

    public List<SchemaExportColumnLink> ColumnLinks { get; set; } = new();
}

public sealed class SchemaExportColumnLink
{
    public string FromColumn { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
}
