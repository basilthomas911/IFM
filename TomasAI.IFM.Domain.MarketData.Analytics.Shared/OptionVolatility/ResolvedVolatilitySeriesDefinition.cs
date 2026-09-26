namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>
/// Represents a resolved volatility-series definition and its canonical payload digest.
/// </summary>
public sealed record ResolvedVolatilitySeriesDefinition(
    VolatilitySeriesDefinition Definition,
    string PayloadSha256);
