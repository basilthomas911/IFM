using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;

/// <summary>Supplies immutable source provenance at the durable command boundary.</summary>
public sealed record MarketOutlookSnapshotPersistencePolicy(
    MarketOutlookSnapshotSource SnapshotSource)
{
    public static MarketOutlookSnapshotPersistencePolicy Legacy { get; } =
        new(MarketOutlookSnapshotSource.Unknown);
}
