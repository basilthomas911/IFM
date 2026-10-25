using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>One selected flat rate and immutable source provenance; this is not a bootstrapped zero curve.</summary>
[MessagePackObject]
public sealed record TreasuryContinuousRate(
    [property: Key(0)] TreasuryTenor Tenor,
    [property: Key(1)] decimal RatePercent,
    [property: Key(2)] double AnnualContinuousRate,
    [property: Key(3)] DateOnly ValueDate,
    [property: Key(4)] DateTimeOffset ObservedAtUtc,
    [property: Key(5)] string CurveDigest,
    [property: Key(6)] TreasuryRateConversionPolicy Conversion,
    [property: Key(7)] string ModelingPolicy);
