using DataDock.Cli;
using DataDock.Core.Models;

namespace DataDock.Tests;

public class WriteModeParserTests
{
    public static IEnumerable<object[]> ValidCases => new List<object[]>
    {
        new object[] { "insert", WriteMode.Insert },
        new object[] { "INSERT", WriteMode.Insert },
        new object[] { "truncate-insert", WriteMode.TruncateInsert },
        new object[] { "TruncateInsert", WriteMode.TruncateInsert },
        new object[] { "truncate", WriteMode.TruncateInsert },
        new object[] { "upsert", WriteMode.Upsert }
    };

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void TryParse_ReturnsExpectedMode(string input, WriteMode expected)
    {
        var success = WriteModeParser.TryParse(input, out var mode);
        Assert.True(success);
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("truncate_insert")] // underscore not supported
    public void TryParse_InvalidValueReturnsFalse(string? input)
    {
        var success = WriteModeParser.TryParse(input, out var mode);
        Assert.False(success);
        Assert.Equal(WriteMode.Insert, mode); // default fallback
    }
}
