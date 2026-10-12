using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts;

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
