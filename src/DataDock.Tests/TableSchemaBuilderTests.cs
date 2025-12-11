using DataDock.Core;
using DataDock.Core.Dialects;
using DataDock.Core.Models;
using DataDock.Core.Services;
using Xunit;

namespace DataDock.Tests;

public class TableSchemaBuilderTests
{
    [Fact]
    public void FromProfile_MapsFieldsToColumnsWithNamingStyle()
    {
        var profile = new ImportProfile
        {
            Name = "Tickets",
            TableName = "Tickets",
            TableSchema = "ingest",
            ColumnNameStyle = ColumnNameStyle.SnakeCase,
            TargetFields =
            {
                new TargetField { Name = "TicketId", FieldType = FieldType.String, IsRequired = true, MaxLength = 50 },
                new TargetField { Name = "OpenedDate", FieldType = FieldType.DateTime, IsRequired = true },
                new TargetField { Name = "Status", FieldType = FieldType.String, IsRequired = false, MaxLength = 20 }
            }
        };

        var schema = TableSchemaBuilder.FromProfile(profile);

        Assert.Equal("Tickets", schema.TableName);
    Assert.Equal("ingest", schema.SchemaName);
        Assert.Equal(3, schema.Columns.Count);

        var ticketCol = schema.Columns.Single(c => c.Name == "ticket_id");
        Assert.Equal(FieldType.String, ticketCol.FieldType);
        Assert.Equal(50, ticketCol.MaxLength);
        Assert.True(ticketCol.IsRequired);

        var openedCol = schema.Columns.Single(c => c.Name == "opened_date");
        Assert.Equal(FieldType.DateTime, openedCol.FieldType);
        Assert.True(openedCol.IsRequired);
    }

    [Fact]
    public void SqlServerDialect_GeneratesCreateTable()
    {
        var schema = new TableSchema
        {
            TableName = "Tickets",
            Columns =
            {
                new TableColumn
                {
                    Name = "ticket_id",
                    FieldType = FieldType.String,
                    MaxLength = 50,
                    IsRequired = true
                },
                new TableColumn
                {
                    Name = "opened_date",
                    FieldType = FieldType.DateTime,
                    IsRequired = true
                },
                new TableColumn
                {
                    Name = "status",
                    FieldType = FieldType.String,
                    MaxLength = 20,
                    IsRequired = false
                }
            }
        };

        var dialect = new SqlServerDialect();
        var sql = dialect.GenerateCreateTable(schema);

        Assert.Contains("CREATE TABLE [Tickets]", sql);
        Assert.Contains("[ticket_id] VARCHAR(50) NOT NULL", sql);
        Assert.Contains("[opened_date] DATETIME2 NOT NULL", sql);
        Assert.Contains("[status] VARCHAR(20) NULL", sql);
    }

    [Fact]
    public void SqlServerDialect_GeneratesSchemaQualifiedName()
    {
        var schema = new TableSchema
        {
            TableName = "Tickets",
            SchemaName = "ingest",
            Columns =
            {
                new TableColumn { Name = "ticket_id", FieldType = FieldType.String, MaxLength = 50, IsRequired = true }
            }
        };

        var dialect = new SqlServerDialect();
        var sql = dialect.GenerateCreateTable(schema);

        Assert.Contains("CREATE TABLE [ingest].[Tickets]", sql);
    }
}
