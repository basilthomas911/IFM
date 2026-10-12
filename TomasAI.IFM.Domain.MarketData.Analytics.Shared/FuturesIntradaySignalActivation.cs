namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Contains the four signal actor identities for one intraday timeframe.</summary>
public sealed record FuturesIntradaySignalActivation(
    TimeFrameType TimeFrame,
    FuturesRsiSignalEntityId Rsi,
    FuturesAtrSignalEntityId Atr,
    FuturesAdxSignalEntityId Adx,
    FuturesMacdSignalEntityId Macd);
