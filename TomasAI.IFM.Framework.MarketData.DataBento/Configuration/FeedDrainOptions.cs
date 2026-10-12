namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedDrainOptions
{
    public int NativeReadRecordCapacity { get; init; } = 512;
    public int MaxRecordsPerDrainPass { get; init; } = 8_192;
}
