using DataDock.Core.Services;

namespace DataDock.Tests;

public class StringLengthBucketizerTests
{
    public static IEnumerable<object[]> BucketCases => new List<object[]>
    {
        new object[] { 1, 50 },
        new object[] { 50, 50 },
        new object[] { 51, 100 },
        new object[] { 180, 255 },
        new object[] { 256, 500 },
        new object[] { 600, 1000 },
        new object[] { 2100, 3000 },
        new object[] { 9999, 4000 },
        new object[] { 0, 50 },
        new object[] { -5, 50 }
    };

    [Theory]
    [MemberData(nameof(BucketCases))]
    public void MapsObservedLengthIntoExpectedBucket(int maxObserved, int expected)
    {
        var suggestion = StringLengthBucketizer.GetSuggestedLength(maxObserved);
        Assert.Equal(expected, suggestion);
    }
}
