using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>Verified quotation convention; zero remains unqualified for legacy observations.</summary>
public enum TreasuryRateConvention { Unknown = 0, UsTreasuryCmtNominalSemiannual = 1 }

/// <summary>Reviewed source-series mapping. Evidence is required independently of numeric agreement.</summary>
/// <param name="Source">The source.</param>
/// <param name="SourceSeriesId">The source series id.</param>
/// <param name="Convention">The convention.</param>
/// <param name="Version">The version.</param>
/// <param name="EvidenceId">The evidence id.</param>
[MessagePackObject]
public sealed record TreasuryRateConversionPolicy(
    [property: Key(0)] string Source,
    [property: Key(1)] string SourceSeriesId,
    [property: Key(2)] TreasuryRateConvention Convention,
    [property: Key(3)] string Version,
    [property: Key(4)] string EvidenceId);

/// <summary>One selected flat rate and immutable source provenance; this is not a bootstrapped zero curve.</summary>
/// <param name="Tenor">The tenor.</param>
/// <param name="RatePercent">The rate percent.</param>
/// <param name="AnnualContinuousRate">The annual continuous rate.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="ObservedAtUtc">The observed at utc.</param>
/// <param name="CurveDigest">The curve digest.</param>
/// <param name="Conversion">The conversion.</param>
/// <param name="ModelingPolicy">The modeling policy.</param>
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
/// <param name="Value">The value.</param>
/// <param name="Error">The error.</param>
public sealed record TreasuryContinuousRateResult(TreasuryContinuousRate? Value, string? Error)
{
    public bool Succeeded => Value is not null && Error is null;
    /// <summary>Creates an unsuccessful result containing the supplied failure detail.</summary>
    /// <param name="error">The failure description.</param>
    /// <returns>The failed result.</returns>
    public static TreasuryContinuousRateResult Failed(string error) => new(null, error);
}
