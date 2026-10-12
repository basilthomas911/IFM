namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedThreadPriorityOptions
{
    public FeedThreadPriority NativeProducer { get; init; } = FeedThreadPriority.AboveNormal;
    public FeedThreadPriority ManagedDrain { get; init; } = FeedThreadPriority.Highest;
    public bool RequireConfiguredPriority { get; init; }
}
