namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Classifies the current TDI price-line position against its reference levels.</summary>
public enum FuturesTdiMarketStateType
{
    Unknown = 0,
    Oversold = 1,
    BelowMidline = 2,
    AboveMidline = 3,
    Overbought = 4
}
