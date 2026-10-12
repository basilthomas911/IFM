namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>
/// Supported Treasury curve maturities. Values represent maturity months and
/// can be used for deterministic ceiling-tenor selection.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is intentionally invalid; preserve existing serialized values.")]
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
