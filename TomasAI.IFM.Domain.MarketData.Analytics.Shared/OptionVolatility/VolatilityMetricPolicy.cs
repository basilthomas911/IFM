using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>
/// Versioned calculation policy. Values are configuration inputs; this contract does not activate a default.
/// </summary>
[MessagePackObject]
public sealed record VolatilityMetricPolicy(
    [property: Key(0)] string PolicyVersion,
    [property: Key(1)] int HistoricalLookbackSessions,
    [property: Key(2)] int MinimumValidObservations,
    [property: Key(3)] decimal MinimumCoverageRatio,
    [property: Key(4)] VolatilityGapPolicy GapPolicy,
    [property: Key(5)] VolatilityHistoricalWindowConvention WindowConvention,
    [property: Key(6)] VolatilityRankRangeConvention RankRangeConvention,
    [property: Key(7)] VolatilityPercentileTieConvention PercentileTieConvention);
