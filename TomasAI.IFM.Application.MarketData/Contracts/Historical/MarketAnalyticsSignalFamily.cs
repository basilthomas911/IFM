using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Identifies an independently persisted Analytics signal family.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum MarketAnalyticsSignalFamily : byte
{
    /// <summary>Exponential moving averages.</summary>
    Ema = 1,
    /// <summary>Bollinger bands.</summary>
    BollingerBand = 2,
    /// <summary>ATR volatility context.</summary>
    AtrVolatility = 3,
    /// <summary>Market-structure context.</summary>
    MarketStructure = 4,
    /// <summary>VX front/back term structure.</summary>
    VxTermStructure = 5
}
