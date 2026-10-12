namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedMemoryOptions
{
    public bool LockRingMemory { get; init; } = true;
    public bool RequireLockedMemory { get; init; }
    public bool RequireBasePagePolicy { get; init; }
}
