using DataDock.Core.Models;
using DataDock.Core.Services;

namespace DataDock.Tests;

public class FileSchemaInferenceServiceTests
{
    [Fact]
    public void InferTargetFields_UsesBucketizerForStringColumns()
    {
        var headers = new[] { "TicketId", "Quantity" };
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { new string('A', 180), "42" },
            new[] { new string('B', 200), "7" },
            new[] { new string('C', 10), "0" }
        };

        var fields = FileSchemaInferenceService.InferTargetFields(headers, rows);

        var ticketField = Assert.Single(fields, f => f.Name == "TicketId");
        Assert.Equal(FieldType.String, ticketField.FieldType);
        Assert.Equal(255, ticketField.MaxLength);
        Assert.True(ticketField.IsRequired);

        var quantityField = Assert.Single(fields, f => f.Name == "Quantity");
        Assert.Equal(FieldType.Int, quantityField.FieldType);
        Assert.Null(quantityField.MaxLength);
        Assert.True(quantityField.IsRequired);
    }
}
