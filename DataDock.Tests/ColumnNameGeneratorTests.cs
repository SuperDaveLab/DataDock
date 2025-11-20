using DataDock.Core;
using DataDock.Core.Models;
using DataDock.Core.Services;
using Xunit;

namespace DataDock.Tests;

public class ColumnNameGeneratorTests
{
    [Theory]
    [InlineData("Ticket #", ColumnNameStyle.SnakeCase, "ticket_num")]
    [InlineData("PO #", ColumnNameStyle.SnakeCase, "po_num")]
    [InlineData("Revenue %", ColumnNameStyle.SnakeCase, "revenue_pct")]
    [InlineData("Job Number", ColumnNameStyle.SnakeCase, "job_number")]
    [InlineData("JOB_NUMBER", ColumnNameStyle.SnakeCase, "job_number")]
    [InlineData("Job-Number", ColumnNameStyle.SnakeCase, "job_number")]
    [InlineData("Ticket # (Open)", ColumnNameStyle.SnakeCase, "ticket_num_open")]
    public void ToColumnName_SnakeCase_NormalizesExpected(string input, ColumnNameStyle style, string expected)
    {
        var result = ColumnNameGenerator.ToColumnName(input, style);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Ticket #", ColumnNameStyle.CamelCase, "ticketNum")]
    [InlineData("PO #", ColumnNameStyle.CamelCase, "poNum")]
    [InlineData("Revenue %", ColumnNameStyle.CamelCase, "revenuePct")]
    [InlineData("Job Number", ColumnNameStyle.CamelCase, "jobNumber")]
    public void ToColumnName_CamelCase_NormalizesExpected(string input, ColumnNameStyle style, string expected)
    {
        var result = ColumnNameGenerator.ToColumnName(input, style);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Ticket #", ColumnNameStyle.PascalCase, "TicketNum")]
    [InlineData("PO #", ColumnNameStyle.PascalCase, "PoNum")]
    [InlineData("Revenue %", ColumnNameStyle.PascalCase, "RevenuePct")]
    [InlineData("Job Number", ColumnNameStyle.PascalCase, "JobNumber")]
    public void ToColumnName_PascalCase_NormalizesExpected(string input, ColumnNameStyle style, string expected)
    {
        var result = ColumnNameGenerator.ToColumnName(input, style);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToColumnName_AsIs_ReturnsInput()
    {
        const string input = "Weird Header (Raw) #1";
        var result = ColumnNameGenerator.ToColumnName(input, ColumnNameStyle.AsIs);
        Assert.Equal(input, result);
    }
}
