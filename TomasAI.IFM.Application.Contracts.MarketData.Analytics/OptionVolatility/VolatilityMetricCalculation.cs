using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>Pure calculator output, with independently qualified Rank and Percentile.</summary>
public sealed record VolatilityMetricCalculation(
    decimal? CurrentImpliedVolatility,
    VolatilityValueUnit ImpliedVolatilityUnit,
    decimal? IvRank,
    VolatilityMetricStatus RankStatus,
    decimal? IvPercentile,
    VolatilityMetricStatus PercentileStatus,
    VolatilityMetricUnit MetricUnit,
    decimal? RankLowImpliedVolatility,
    decimal? RankHighImpliedVolatility,
    int HistoricalBelowCurrentCount,
    int HistoricalTieCount,
    int ValidHistoricalObservationCount,
    int ExpectedHistoricalObservationCount,
    decimal CoverageRatio,
    DateOnly WindowStartValueDate,
    DateOnly WindowEndValueDate,
    ImmutableArray<string> SourceObservationIds);
