namespace TomasAI.IFM.Shared.Application;


/// <summary>
/// Provides a collection of URI paths for querying market data analytics related to futures trading signals.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// market data analytics API. These endpoints are used to retrieve different types of futures trading signals,
/// including RSI signals, ITI signals, and trend direction data. The URIs are intended to be used as part of HTTP
/// requests to the API.</remarks>
public static class MarketDataAnalyticsQueryUriPath
{
    public const string GetMarketOutlookSnapshot = "/api/market-data-analytics/market-outlook-snapshot";
    public const string GetFuturesBollingerBandHistory = "/api/market-data-analytics/futures-bollinger-band-history";
    public const string GetFuturesTradeSignal = "/api/marketdata/analytics/futures/tradesignal";
    public const string GetLastFuturesTradeSignal = "/api/marketdata/analytics/futures/tradesignal/last";
    public const string GetFuturesTradeSignalBySymbol = "/api/marketdata/analytics/futures/tradesignal/bysymbol";
    public const string GetFuturesTradeSignalIds = "/api/marketdata/analytics/futures/tradesignal/ids";
    public const string GetFuturesRsiSignal = "/api/marketdata/analytics/futures/rsisignal";
    public const string GetFuturesTrendDirectionFromRSISignal = "/api/marketdata/analytics/futures/rsisignal/trenddirection";
    public const string GetFuturesTdiSignal = "/api/marketdata/analytics/futures/tdisignal";
    public const string GetFuturesItiSignal = "/api/marketdata/analytics/futures/itisignal";
    public const string GetFuturesItiSignalHistory = "/api/marketdata/analytics/futures/itisignal/history";
    public const string GetFuturesItiTrendDirectionChangedSignals = "/api/marketdata/analytics/futures/ititrend/directionchangedsignals";
    public const string GetFuturesItiSignalData = "/api/marketdata/analytics/futures/itisignaldata";
    public const string GetFuturesItiMDIDistribution = "/api/marketdata/analytics/futures/itimdidistribution";
    public const string GetFuturesItiMDIDistributionByTrend = "/api/marketdata/analytics/futures/itimdidistribution/bytrend";
    public const string GetFuturesItiSignalMDI = "/api/marketdata/analytics/futures/itisignal/mdi";
    public const string GetFuturesItiSignalMDIByTrend = "/api/marketdata/analytics/futures/itisignal/mdi/bytrend";
    public const string GetFuturesAtrSignal = "/api/marketdata/analytics/futures/atrsignal";
    public const string GetFuturesAdxSignal = "/api/marketdata/analytics/futures/adxsignal";
    public const string GetFuturesMacdSignal = "/api/marketdata/analytics/futures/macdsignal";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
