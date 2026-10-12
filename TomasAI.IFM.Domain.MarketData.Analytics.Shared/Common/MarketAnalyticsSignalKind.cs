using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Identifies the calculation family represented by an analytics signal.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketAnalyticsSignalKind : byte
{
    /// <summary>No signal family is identified.</summary>
    Unknown = 0,

    /// <summary>Relative Strength Index.</summary>
    Rsi = 1,

    /// <summary>Average Directional Index.</summary>
    Adx = 2,

    /// <summary>Average True Range.</summary>
    Atr = 3,

    /// <summary>Moving Average Convergence Divergence.</summary>
    Macd = 4,

    /// <summary>Exponential Moving Average.</summary>
    Ema = 5,

    /// <summary>Bollinger Band.</summary>
    BollingerBand = 6,

    /// <summary>Market structure.</summary>
    MarketStructure = 7,

    /// <summary>VX futures term structure.</summary>
    VxTermStructure = 8,

    /// <summary>Volume Weighted Average Price.</summary>
    Vwap = 9,

    /// <summary>Intrinsic Time Indicator.</summary>
    Iti = 10,

    /// <summary>Traders Dynamic Index.</summary>
    Tdi = 11
}
