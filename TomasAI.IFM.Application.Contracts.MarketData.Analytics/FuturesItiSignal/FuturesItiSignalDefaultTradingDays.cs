namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;

/// <summary>Defines editable trading-day defaults for Futures ITI timeframes.</summary>
[MessagePack.MessagePackObject]
public sealed record FuturesItiSignalDefaultTradingDays
{
    /// <summary>Gets the number of trading days represented by the Daily timeframe.</summary>
    [MessagePack.Key(0)] public int Daily { get; init; } = 1;

    /// <summary>Gets the number of trading days represented by the Weekly timeframe.</summary>
    [MessagePack.Key(1)] public int Weekly { get; init; } = 10;

    /// <summary>Gets the number of trading days represented by the Monthly timeframe.</summary>
    [MessagePack.Key(2)] public int Monthly { get; init; } = 30;
}
