namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;

/// <summary>Defines editable trading-day defaults for Futures ITI timeframes.</summary>
public sealed record FuturesItiSignalDefaultTradingDays
{
    /// <summary>Gets the number of trading days represented by the Daily timeframe.</summary>
    public int Daily { get; init; } = 1;

    /// <summary>Gets the number of trading days represented by the Weekly timeframe.</summary>
    public int Weekly { get; init; } = 10;

    /// <summary>Gets the number of trading days represented by the Monthly timeframe.</summary>
    public int Monthly { get; init; } = 30;
}
