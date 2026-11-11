namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Qualifies bounded instrument definition work before the persistence operation starts.</summary>
public static class InstrumentDefinitionRequestQualification
{
    /// <summary>Qualifies one complete partitioned write batch.</summary>
    public static void RequireBatch(Guid snapshot, long firstIndex, int count)
    {
        if (snapshot == Guid.Empty || firstIndex < 0 || count is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    /// <summary>Qualifies an exact ordinal and its derived storage bucket.</summary>
    public static void RequireOrdinal(int bucket, long index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (bucket is < 0 or >= 128 || bucket != index % 128)
            throw new ArgumentOutOfRangeException(nameof(bucket));
    }
}
