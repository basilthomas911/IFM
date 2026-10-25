namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>
/// One normalized point on a Treasury curve.
/// </summary>
/// <param name="Tenor">The maturity represented by the point.</param>
/// <param name="RatePercent">
/// The annualized rate in percentage points; for example, 4.25 means 4.25%.
/// </param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
