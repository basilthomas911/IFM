namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedCoreIsolationOptions
{
    public FeedCoreIsolationMode Mode { get; init; } =
        FeedCoreIsolationMode.ExcludeFromProcessWorkers;
    public bool RequireCoreIsolation { get; init; }
}
