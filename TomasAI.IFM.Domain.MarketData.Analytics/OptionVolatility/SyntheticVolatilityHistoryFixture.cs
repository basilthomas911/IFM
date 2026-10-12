using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

/// <summary>
/// Deterministic, explicitly synthetic, market-closed history used for offline qualification.
/// It contains the complete configured prior-session window plus a current observation, a futures
/// roll boundary, and an append-only correction learned after the original observation.
/// </summary>
public sealed record SyntheticVolatilityHistoryFixture(
    bool IsSynthetic,
    string Environment,
    string MarketState,
    DateTimeOffset VirtualNowUtc,
    VolatilitySeriesDefinition Definition,
    ImmutableArray<OptionIvObservation> AppendOnlyObservations,
    ImmutableArray<SyntheticVolatilityFixtureEvent> Events)
{
    public ImmutableArray<OptionIvObservation> AsKnownAt(DateTimeOffset knownAtUtc) =>
        AppendOnlyObservations.Where(x => x.AvailableAtUtc <= knownAtUtc)
            .GroupBy(x => (x.ExchangeValueDate, x.SamplingSlot))
            .Select(x => x.OrderByDescending(y => y.Revision).First())
            .OrderBy(x => x.ExchangeValueDate).ToImmutableArray();
}
