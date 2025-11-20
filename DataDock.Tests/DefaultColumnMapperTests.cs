using DataDock.Core;
using DataDock.Core.Models;
using DataDock.Core.Services;
using Xunit;

namespace DataDock.Tests;

public class DefaultColumnMapperTests
{
    [Fact]
    public void GenerateMappings_UsesExactHeaderMatch()
    {
        var profile = new ImportProfile
        {
            TargetFields =
            {
                new TargetField { Name = "TicketId", FieldType = FieldType.String, IsRequired = true }
            }
        };

        var sourceColumns = new[]
        {
            new SourceColumn { HeaderName = "TicketId", Index = 0 }
        };

        var mapper = new DefaultColumnMapper();
        var mappings = mapper.GenerateMappings(profile, sourceColumns);

        Assert.Single(mappings);
        Assert.Equal("TicketId", mappings[0].TargetField.Name);
        Assert.NotNull(mappings[0].SourceColumn);
        Assert.Equal("TicketId", mappings[0].SourceColumn!.HeaderName);
        Assert.True(mappings[0].IsAutoMapped);
    }

    [Fact]
    public void GenerateMappings_UsesAliasesWhenExactMissing()
    {
        var profile = new ImportProfile
        {
            TargetFields =
            {
                new TargetField { Name = "TicketId", FieldType = FieldType.String, IsRequired = true }
            },
            Aliases =
            {
                new ColumnAlias { TargetFieldName = "TicketId", Alias = "Ticket #" }
            }
        };

        var sourceColumns = new[]
        {
            new SourceColumn { HeaderName = "Ticket #", Index = 0 }
        };

        var mapper = new DefaultColumnMapper();
        var mappings = mapper.GenerateMappings(profile, sourceColumns);

        Assert.Single(mappings);
        Assert.Equal("TicketId", mappings[0].TargetField.Name);
        Assert.NotNull(mappings[0].SourceColumn);
        Assert.Equal("Ticket #", mappings[0].SourceColumn!.HeaderName);
        Assert.True(mappings[0].IsAutoMapped);
    }

    [Fact]
    public void GenerateMappings_AllowsUnmappedFields()
    {
        var profile = new ImportProfile
        {
            TargetFields =
            {
                new TargetField { Name = "TicketId", FieldType = FieldType.String, IsRequired = true },
                new TargetField { Name = "JobNumber", FieldType = FieldType.String, IsRequired = false }
            }
        };

        var sourceColumns = new[]
        {
            new SourceColumn { HeaderName = "TicketId", Index = 0 }
        };

        var mapper = new DefaultColumnMapper();
        var mappings = mapper.GenerateMappings(profile, sourceColumns);

        Assert.Equal(2, mappings.Count);

        var ticketMap = mappings.Single(m => m.TargetField.Name == "TicketId");
        var jobMap    = mappings.Single(m => m.TargetField.Name == "JobNumber");

        Assert.NotNull(ticketMap.SourceColumn);
        Assert.Null(jobMap.SourceColumn); // unmapped but present in mappings
    }
}
