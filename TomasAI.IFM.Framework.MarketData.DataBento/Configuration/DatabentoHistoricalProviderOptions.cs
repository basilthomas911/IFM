namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>
/// Controls the native Databento historical provider without retaining credentials.
/// </summary>
public sealed record DatabentoHistoricalProviderOptions
{
    /// <summary>
    /// Gets whether calls use the deterministic offline native provider.
    /// </summary>
    public bool UseSyntheticProvider { get; init; }

    /// <summary>
    /// Gets the bounded timeout supplied to native historical operations.
    /// </summary>
    public uint TimeoutMilliseconds { get; init; } = 30_000;

    /// <summary>
    /// Gets the maximum number of records copied across the ABI per read.
    /// </summary>
    public int MaximumBatchRecords { get; init; } = 4_096;

    internal void Validate()
    {
        if (TimeoutMilliseconds is 0 or uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeoutMilliseconds));
        }
        if (MaximumBatchRecords is < 1 or > 65_536)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumBatchRecords));
        }
    }
}
