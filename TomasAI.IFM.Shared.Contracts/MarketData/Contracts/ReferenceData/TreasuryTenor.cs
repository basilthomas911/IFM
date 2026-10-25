namespace TomasAI.IFM.Shared.MarketData.Contracts;

/// <summary>
/// Supported Treasury curve maturities. Values represent maturity months and
/// can be used for deterministic ceiling-tenor selection.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Treasury tenor values are explicit nonzero durations; zero is not a supported tenor.")]
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
