namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedProcessorResidencyOptions
{
    public bool EnableTracking { get; init; }
    public int ForcedMigrationIntervalRecords { get; init; }
}
