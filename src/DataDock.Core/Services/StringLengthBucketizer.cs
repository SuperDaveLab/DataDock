using System;

namespace DataDock.Core.Services;

public static class StringLengthBucketizer
{
    private static readonly int[] Buckets = { 50, 100, 255, 500, 1000, 2000, 3000, 4000 };

    /// <summary>
    /// Maps the observed maximum string length to the nearest configured bucket that is
    /// greater than or equal to <paramref name="maxObservedLength"/>. For zero or negative
    /// inputs we return the smallest bucket as a safe default.
    /// </summary>
    public static int GetSuggestedLength(int maxObservedLength)
    {
        if (maxObservedLength <= 0)
        {
            return Buckets[0];
        }

        foreach (var bucket in Buckets)
        {
            if (maxObservedLength <= bucket)
            {
                return bucket;
            }
        }

        return Buckets[^1];
    }
}
