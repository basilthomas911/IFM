namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>
/// Supported Treasury curve maturities. Values represent maturity months and
/// can be used for deterministic ceiling-tenor selection.
/// </summary>
public enum TreasuryTenor
{
    OneMonth = 1,
    TwoMonth = 2,
    ThreeMonth = 3,
    SixMonth = 6,
    OneYear = 12,
    TwoYear = 24,
    ThreeYear = 36,
    FiveYear = 60,
    SevenYear = 84,
    TenYear = 120,
    TwentyYear = 240,
    ThirtyYear = 360
}

/// <summary>
/// One normalized point on a Treasury curve.
/// </summary>
/// <param name="Tenor">The maturity represented by the point.</param>
/// <param name="RatePercent">
/// The annualized rate in percentage points; for example, 4.25 means 4.25%.
/// </param>
public readonly record struct TreasuryRatePoint(
    TreasuryTenor Tenor,
    decimal RatePercent)
{
    /// <summary>
    /// Gets the annualized decimal rate used by pricing calculations; for
    /// example, 4.25 percentage points becomes 0.0425.
    /// </summary>
    public decimal DecimalRate => RatePercent / 100m;
}
