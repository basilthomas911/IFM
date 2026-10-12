namespace TomasAI.IFM.Shared.Application;



public static class MarketDataAnalyticsUriPath
{
    public const string StartFuturesRsiSignal = "/api/marketdata/Analytics/Futures/RsiSignal/start";
    public const string StopFuturesRsiSignal = "/api/marketdata/Analytics/Futures/RsiSignal/stop";
    public const string StartFuturesMacdSignal = "/api/marketdata/Analytics/Futures/MacdSignal/start";
    public const string StopFuturesMacdSignal = "/api/marketdata/Analytics/Futures/MacdSignal/stop";
    public const string StartFuturesAdxSignal = "/api/marketdata/Analytics/Futures/AdxSignal/start";
    public const string StopFuturesAdxSignal = "/api/marketdata/Analytics/Futures/AdxSignal/stop";
    public const string StartFuturesAtrSignal = "/api/marketdata/Analytics/Futures/AtrSignal/start";
    public const string StopFuturesAtrSignal = "/api/marketdata/Analytics/Futures/AtrSignal/stop";
    public const string GenerateFuturesRsiSignal = "/api/marketdata/Analytics/Futures/RsiSignal/Generate";
    public const string GenerateFuturesRsiDailySignal = "/api/marketdata/Analytics/Futures/RsiSignal/Daily/Generate";
    public const string UpdateFuturesTradeSignal = "/api/marketdata/Analytics/Futures/TradeSignal/update";
    public const string GenerateFuturesTdiSignal = "/api/marketdata/Analytics/Futures/TdiSignal/Generate";
    public const string GenerateFuturesItiSignal = "/api/marketdata/Analytics/Futures/ItiSignal/Generate";
    public const string SetFuturesItiSignalHoldTrade = "/api/marketdata/Analytics/Futures/ItiSignal/HoldTrade/Set";
    public const string ClearFuturesItiSignalHoldTrade = "/api/marketdata/Analytics/Futures/ItiSignal/HoldTrade/Clear";
    public const string GenerateFuturesAtrSignal = "/api/marketdata/Analytics/Futures/AtrSignal/Generate";
    public const string GenerateFuturesAdxSignal = "/api/marketdata/Analytics/Futures/AdxSignal/Generate";
    public const string GenerateFuturesMacdSignal = "/api/marketdata/Analytics/Futures/MacdSignal/Generate";
}
