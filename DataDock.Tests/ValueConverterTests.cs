using DataDock.Core;
using DataDock.Core.Models;
using DataDock.Core.Services;
using Xunit;

namespace DataDock.Tests;

public class ValueConverterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Convert_EmptyString_ReturnsNull(string? raw)
    {
        var result = ValueConverter.Convert(FieldType.String, raw);

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("  42  ", 42)]
    public void Convert_Int_ParsesValid(string raw, int expected)
    {
        var result = ValueConverter.Convert(FieldType.Int, raw);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Convert_Int_Invalid_ReturnsError()
    {
        var result = ValueConverter.Convert(FieldType.Int, "abc");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData("yes", true)]
    [InlineData("No", false)]
    public void Convert_Bool_ParsesCommonFormats(string raw, bool expected)
    {
        var result = ValueConverter.Convert(FieldType.Bool, raw);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Convert_Bool_Invalid_ReturnsError()
    {
        var result = ValueConverter.Convert(FieldType.Bool, "maybe");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("2025-03-01")]
    [InlineData("03/01/2025")]
    public void Convert_DateTime_ParsesCommonFormats(string raw)
    {
        var result = ValueConverter.Convert(FieldType.DateTime, raw);

        Assert.True(result.Success);
        Assert.IsType<DateTime>(result.Value);
    }
}
