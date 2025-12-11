using DataDock.Core.Models;
using DataDock.Core.Services;

namespace DataDock.Tests;

public class SqlServerDataWriterTests
{
    [Fact]
    public void UpsertWithoutKeyFieldsThrows()
    {
        var profile = BuildProfile();

        Assert.Throws<InvalidOperationException>(() =>
            new SqlServerDataWriter(profile, WriteMode.Upsert, Array.Empty<string>()));
    }

    [Fact]
    public void InsertWithoutKeysIsAllowed()
    {
        var profile = BuildProfile();

        var writer = new SqlServerDataWriter(profile, WriteMode.Insert, Array.Empty<string>());

        Assert.NotNull(writer);
    }

    private static ImportProfile BuildProfile()
    {
        return new ImportProfile
        {
            TableName = "dbo.TestTable",
            TargetFields = new List<TargetField>
            {
                new() { Name = "TicketId", FieldType = FieldType.String, MaxLength = 50 },
                new() { Name = "Status", FieldType = FieldType.String, MaxLength = 20 }
            }
        };
    }
}
