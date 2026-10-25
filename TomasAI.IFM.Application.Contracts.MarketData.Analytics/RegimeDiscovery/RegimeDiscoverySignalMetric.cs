using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Identifies one normalized metric consumed by Regime Discovery.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeDiscoverySignalMetric : byte
{
    /// <summary>No metric is identified.</summary>
    Unknown = 0,
    /// <summary>Current futures price.</summary>
    CurrentPrice = 1,
    /// <summary>EMA20 value.</summary>
    Ema20 = 2,
    /// <summary>EMA50 value.</summary>
    Ema50 = 3,
    /// <summary>EMA200 value.</summary>
    Ema200 = 4,
    /// <summary>ATR-normalized EMA20 slope.</summary>
    Ema20Slope = 5,
    /// <summary>ATR-normalized EMA50 slope.</summary>
    Ema50Slope = 6,
    /// <summary>ATR-normalized EMA200 slope.</summary>
    Ema200Slope = 7,
    /// <summary>RSI14 value.</summary>
    Rsi14 = 8,
    /// <summary>RSI14 slope.</summary>
    Rsi14Slope = 9,
    /// <summary>ADX14 value.</summary>
    Adx14 = 10,
    /// <summary>ADX positive directional indicator.</summary>
    PlusDi14 = 11,
    /// <summary>ADX negative directional indicator.</summary>
    MinusDi14 = 12,
    /// <summary>Conventional MACD histogram.</summary>
    MacdHistogram = 13,
    /// <summary>ATR14 value.</summary>
    Atr14 = 14,
    /// <summary>ATR14 divided by its baseline.</summary>
    AtrBaselineRatio = 15,
    /// <summary>Current VIX level.</summary>
    VixLevel = 16,
    /// <summary>Front VX contract divided by the second contract.</summary>
    VxFrontSecondRatio = 17,
    /// <summary>Optional realized-volatility percentile.</summary>
    RealizedVolatilityPercentile = 18,
    /// <summary>Prior warm volatility composite.</summary>
    PriorVolatilityComposite = 19,
    /// <summary>Bollinger width.</summary>
    BollingerWidth = 20,
    /// <summary>Bollinger width divided by its baseline.</summary>
    BollingerWidthRatio = 21,
    /// <summary>Normalized Bollinger position.</summary>
    BollingerPosition = 22,
    /// <summary>Price minus EMA20 normalized by ATR.</summary>
    Ema20Interaction = 23,
    /// <summary>Observation range normalized by ATR.</summary>
    AtrNormalizedRange = 24,
    /// <summary>Rolling 20-observation high.</summary>
    RollingHigh20 = 25,
    /// <summary>Rolling 20-observation low.</summary>
    RollingLow20 = 26,
    /// <summary>Signed breakout distance normalized by ATR.</summary>
    BreakoutDistanceAtr = 27,
    /// <summary>ITI direction represented as -1 or +1.</summary>
    ItiDirection = 28,
    /// <summary>ITI threshold-band progress.</summary>
    ItiBandLevel = 29,
    /// <summary>ITI reversal progress.</summary>
    ItiReversalLevel = 30,
    /// <summary>Optional TDI value.</summary>
    Tdi = 31,
    /// <summary>Current front VX futures level.</summary>
    VxFrontLevel = 32
}
