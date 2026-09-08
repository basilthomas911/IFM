using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Verified quotation convention; zero remains unqualified for legacy observations.</summary>
public enum TreasuryRateConvention { Unknown = 0, UsTreasuryCmtNominalSemiannual = 1 }

/// <summary>Reviewed source-series mapping. Evidence is required independently of numeric agreement.</summary>
[MessagePackObject]
public sealed record TreasuryRateConversionPolicy(
    [property: Key(0)] string Source,
    [property: Key(1)] string SourceSeriesId,
    [property: Key(2)] TreasuryRateConvention Convention,
    [property: Key(3)] string Version,
    [property: Key(4)] string EvidenceId);

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

/// <summary>Failure has no usable rate; callers must not substitute zero or a neighboring tenor.</summary>
public sealed record TreasuryContinuousRateResult(TreasuryContinuousRate? Value, string? Error)
{
    public bool Succeeded => Value is not null && Error is null;
    public static TreasuryContinuousRateResult Failed(string error) => new(null, error);
}
