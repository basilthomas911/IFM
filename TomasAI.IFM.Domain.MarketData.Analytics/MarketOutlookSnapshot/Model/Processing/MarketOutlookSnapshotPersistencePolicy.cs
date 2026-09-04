using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;

/// <summary>Supplies immutable source provenance at the durable command boundary.</summary>
public sealed record MarketOutlookSnapshotPersistencePolicy(
    MarketOutlookSnapshotSource SnapshotSource)
{
    /// <summary>
    /// Minimum cadence for replacing the latest restart-hydration row. Realtime UI publication
    /// is independent of this interval.
    /// </summary>
    public TimeSpan PersistenceInterval { get; init; } = TimeSpan.FromSeconds(1);

    public static MarketOutlookSnapshotPersistencePolicy Legacy { get; } =
        new(MarketOutlookSnapshotSource.Unknown);
}
