namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;

/// <summary>Defines immutable RSI configurations used by TDI and Regime Discovery.</summary>
public static class FuturesRsiConfigurations
{
    /// <summary>Gets the RSI13 configuration reserved for the existing TDI pipeline.</summary>
    public const string TdiRsi13 = "rsi-13-tdi-v1";

    /// <summary>Gets the independent RSI14 configuration used by Regime Discovery.</summary>
    public const string RegimeRsi14 = "rsi-14-regime-v1";
}
