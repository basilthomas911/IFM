using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityValueUnit
{
    Unspecified = 0,
    AnnualDecimal = 1
}

public enum VolatilityMetricUnit
{
    Unspecified = 0,
    PercentagePoints0To100 = 1
}

public enum VolatilityObservationStatus
{
    Unspecified = 0,
    Qualified = 1,
    Partial = 2,
    Unavailable = 3,
    Invalid = 4,
    Incompatible = 5
}

public enum VolatilityMetricStatus
{
    Unspecified = 0,
    Qualified = 1,
    InsufficientHistory = 2,
    UndefinedRange = 3,
    InvalidCurrentObservation = 4
}

public enum VolatilityMoneynessConvention
{
    Unspecified = 0,
    AtTheMoneyForward = 1,
    AtTheMoneySpot = 2,
    DeltaBased = 3
}

public enum VolatilityOptionSideSelection
{
    Unspecified = 0,
    Calls = 1,
    Puts = 2,
    CallPutCombined = 3
}

public enum VolatilitySideCombinationMethod
{
    Unspecified = 0,
    None = 1,
    ArithmeticMean = 2
}

public enum VolatilityInterpolationMethod
{
    Unspecified = 0,
    None = 1,
    LinearVolatility = 2,
    LinearTotalVariance = 3
}

public enum VolatilityGapPolicy
{
    Unspecified = 0,
    PreserveExpectedSessionGap = 1
}

public enum VolatilityRankRangeConvention
{
    Unspecified = 0,
    CurrentAndPriorWindow = 1
}

public enum VolatilityPercentileTieConvention
{
    Unspecified = 0,
    StrictlyLessThanCurrent = 1
}

public enum VolatilityHistoricalWindowConvention
{
    Unspecified = 0,
    PriorExchangeSessions = 1
}

public enum VolatilityDependencyRequirement
{
    Unspecified = 0,
    Optional = 1,
    Required = 2
}

public enum VolatilityFreshnessStatus
{
    Unspecified = 0,
    Accepted = 1,
    Stale = 2,
    Unavailable = 3
}

[MessagePackObject]
public sealed record VolatilitySeriesIdentity(
    [property: Key(0)] string SeriesId,
    [property: Key(1)] string MethodologyVersion);

[MessagePackObject]
public sealed record VolatilityDataIdentity(
    [property: Key(0)] string Environment,
    [property: Key(1)] string Provider,
    [property: Key(2)] string Dataset);

[MessagePackObject]
public sealed record VolatilityTenorConvention(
    [property: Key(0)] int TargetCalendarDays,
    [property: Key(1)] VolatilityMoneynessConvention MoneynessConvention,
    [property: Key(2)] VolatilityOptionSideSelection OptionSideSelection,
    [property: Key(3)] ImmutableArray<string> EligibleProductFamilies,
    [property: Key(4)] VolatilitySideCombinationMethod SideCombinationMethod);

[MessagePackObject]
public sealed record VolatilityPricingConvention(
    [property: Key(0)] string ExerciseConvention,
    [property: Key(1)] string PremiumConvention,
    [property: Key(2)] string SettlementConvention,
    [property: Key(3)] ImmutableArray<string> ApplicablePricerVersions,
    [property: Key(4)] string NumericalPolicyVersion);

[MessagePackObject]
public sealed record VolatilityQualificationPolicy(
    [property: Key(0)] string QuoteAndIvMarkBasis,
    [property: Key(1)] string LiquidityAndQualityRulesVersion,
    [property: Key(2)] bool RequireExactUnderlyingMatch,
    [property: Key(3)] TimeSpan MaximumQuoteAge,
    [property: Key(4)] TimeSpan MaximumIvAge,
    [property: Key(5)] TimeSpan MaximumQuoteSkew);

[MessagePackObject]
public sealed record VolatilitySeriesConstructionPolicy(
    [property: Key(0)] string ExpirySelectionPolicyVersion,
    [property: Key(1)] VolatilityInterpolationMethod ConstantTenorInterpolation,
    [property: Key(2)] string FuturesRollPolicyVersion,
    [property: Key(3)] string ExchangeCalendarVersion,
    [property: Key(4)] string ExchangeTimeZoneId,
    [property: Key(5)] string SamplingAndCutoffPolicyVersion,
    [property: Key(6)] TimeSpan IntradayCoalescingInterval,
    [property: Key(7)] int MaximumIntradayCheckpointsPerValueDate);

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

[MessagePackObject]
public sealed record VolatilitySeriesGovernance(
    [property: Key(0)] DateTimeOffset EffectiveFromUtc,
    [property: Key(1)] DateTimeOffset? EffectiveUntilUtc,
    [property: Key(2)] string ApprovedConfigurationVersion,
    [property: Key(3)] string Owner,
    [property: Key(4)] string ApprovalEvidenceId,
    [property: Key(5)] ImmutableArray<string> CompatibleHistoricalMethodologyVersions);

/// <summary>Immutable, versioned definition of one comparable option-IV series.</summary>
[MessagePackObject]
public sealed record VolatilitySeriesDefinition(
    [property: Key(0)] ushort SchemaVersion,
    [property: Key(1)] VolatilitySeriesIdentity Identity,
    [property: Key(2)] string UnderlyingRoot,
    [property: Key(3)] string Venue,
    [property: Key(4)] string Currency,
    [property: Key(5)] VolatilityDataIdentity DataIdentity,
    [property: Key(6)] VolatilityTenorConvention Tenor,
    [property: Key(7)] VolatilityPricingConvention Pricing,
    [property: Key(8)] VolatilityQualificationPolicy Qualification,
    [property: Key(9)] VolatilitySeriesConstructionPolicy Construction,
    [property: Key(10)] VolatilityMetricPolicy Metrics,
    [property: Key(11)] VolatilitySeriesGovernance Governance)
{
    public const ushort CurrentSchemaVersion = 1;
}
