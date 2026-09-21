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

/// <summary>Immutable sealed metric payload suitable for later persistence/publication work.</summary>
[MessagePackObject]
public sealed record OptionIvMetricSnapshot(
    [property: Key(0)] ushort SchemaVersion,
    [property: Key(1)] string SnapshotId,
    [property: Key(2)] string SnapshotDigest,
    [property: Key(3)] VolatilitySeriesIdentity Series,
    [property: Key(4)] string MetricPolicyVersion,
    [property: Key(5)] DateOnly ExchangeValueDate,
    [property: Key(6)] string SamplingSlot,
    [property: Key(7)] decimal? CurrentImpliedVolatility,
    [property: Key(8)] VolatilityValueUnit ImpliedVolatilityUnit,
    [property: Key(9)] decimal? IvRank,
    [property: Key(10)] VolatilityMetricStatus RankStatus,
    [property: Key(11)] decimal? IvPercentile,
    [property: Key(12)] VolatilityMetricStatus PercentileStatus,
    [property: Key(13)] VolatilityMetricUnit MetricUnit,
    [property: Key(14)] decimal? RankLowImpliedVolatility,
    [property: Key(15)] decimal? RankHighImpliedVolatility,
    [property: Key(16)] int HistoricalBelowCurrentCount,
    [property: Key(17)] int HistoricalTieCount,
    [property: Key(18)] int ValidHistoricalObservationCount,
    [property: Key(19)] int ExpectedHistoricalObservationCount,
    [property: Key(20)] decimal CoverageRatio,
    [property: Key(21)] DateOnly WindowStartValueDate,
    [property: Key(22)] DateOnly WindowEndValueDate,
    [property: Key(23)] ImmutableArray<string> SourceObservationIds,
    [property: Key(24)] string SourceObservationDigest,
    [property: Key(25)] string CalculationVersion,
    [property: Key(26)] DateTimeOffset ObservedAtUtc,
    [property: Key(27)] DateTimeOffset CalculatedAtUtc,
    [property: Key(28)] DateTimeOffset RecordedAtUtc,
    [property: Key(29)] DateTimeOffset AvailableAtUtc)
{
    public const ushort CurrentSchemaVersion = 1;
}
