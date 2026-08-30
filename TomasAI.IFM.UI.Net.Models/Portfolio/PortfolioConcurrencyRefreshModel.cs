namespace TomasAI.IFM.UI.Net.Models.Portfolio;

/// <summary>UI-safe optimistic-concurrency result; stale edits refresh instead of being silently overwritten.</summary>
public sealed record PortfolioConcurrencyRefreshModel(long ExpectedRevision, long CurrentRevision)
{
    public bool RequiresRefresh => ExpectedRevision != CurrentRevision;
}
