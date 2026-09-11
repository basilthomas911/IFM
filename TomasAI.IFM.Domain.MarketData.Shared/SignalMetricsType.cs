namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Identifies a market-data signal whose calculation is defined by a timeframe
/// and period length.
/// </summary>
public enum SignalMetricsType : byte
{
    /// <summary>No calculated signal is selected.</summary>
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

    /// <summary>Calculated market structure over a configured timeframe and period.</summary>
    MarketStructure = 7,

    /// <summary>Traders Dynamic Index.</summary>
    Tdi = 8
}
