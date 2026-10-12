namespace TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

/// <summary>
/// Identifies the option-price observation used to calculate implied
/// volatility and Black-76 Greeks.
/// </summary>
public enum OptionGreeksPriceSource
{
    None = 0,
    QuoteMidpoint = 1,
    Trade = 2
}
