namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record SyntheticFeedOptions
{
    public int RecordCount { get; init; } = 10_000;
    public int RecordsPerSecond { get; init; }
    public ulong StartSequence { get; init; } = 1;
}
