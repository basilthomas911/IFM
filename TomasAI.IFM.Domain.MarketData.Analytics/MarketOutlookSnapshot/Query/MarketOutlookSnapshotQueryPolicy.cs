namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;

/// <summary>Controls which durable snapshot sources are eligible for startup display.</summary>
public sealed record MarketOutlookSnapshotQueryPolicy(bool RejectSyntheticSnapshots)
{
    public static MarketOutlookSnapshotQueryPolicy AllowAll { get; } = new(false);
}
