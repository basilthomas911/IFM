namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record SyntheticFeedOptions
{
    /// <summary>Converts native performance-counter event times to UTC for end-to-end pricing qualification.</summary>
    public bool UseUtcTimestamps { get; init; }
    public int RecordCount { get; init; } = 10_000;
    public int RecordsPerSecond { get; init; }
    public ulong StartSequence { get; init; } = 1;
}
