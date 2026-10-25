namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI paths for querying market data feed endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// market data feed API. These endpoints are used to retrieve futures tick data, EOD data, bar data, option contracts,
/// and related analytics. The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public static class MarketDataFeedQueryUriPath
{
    public const string GetLastFuturesTickData = "/api/marketdata/feed/futures/lasttick";
    public const string GetLastFuturesTickDataByTickDate = "/api/marketdata/feed/futures/lasttick/bydate";
    public const string GetLastFuturesOptionTickData = "/api/marketdata/feed/futures/option/lasttick";
    public const string GetFuturesEodData = "/api/marketdata/feed/futures/eod";
    public const string GetLastFuturesEodData = "/api/marketdata/feed/futures/eod/last";
    public const string GetFuturesEodDataByDateRange = "/api/marketdata/feed/futures/eod/daterange";
    public const string GetFuturesBarData = "/api/marketdata/feed/futures/bar";
    public const string GetLastFuturesBarData = "/api/marketdata/feed/futures/bar/last";
    public const string GetIronCondorMarketDataFeed = "/api/marketdata/feed/ironcondor";
    public const string GetFuturesEodDataParameters = "/api/marketdata/feed/futures/eod/parameters";
    public const string GetFuturesOptionContract = "/api/marketdata/feed/futures/option/contract";
    public const string GetFuturesOptionSpreadData = "/api/marketdata/feed/futures/option/spread";
    public const string GetNormalCurveTable = "/api/marketdata/feed/normalcurvetable";
    public const string GetVixFuturesEodData = "/api/marketdata/feed/vixfutures/eod";
    public const string GetLastVixFuturesEodData = "/api/marketdata/feed/vixfutures/eod/last";
    public const string GetFuturesRiskPositionType = "/api/marketdata/feed/futures/riskpositiontype";
    public const string GetFuturesEodMovingAverages = "/api/marketdata/feed/futures/eod/movingaverages";
    public const string GetStreamingRequestId = "/api/marketdata/feed/streaming/requestid";
    public const string GetRuntimeStatus = "/api/marketdata/feed/runtime/status";
    public const string GetDatabentoReadiness = "/api/marketdata/feed/databento/readiness";
    public const string GetDatabentoCurrentContracts = "/api/marketdata/feed/databento/contracts/current";
    public const string GetDatabentoWatchdogHistory = "/api/marketdata/feed/databento/watchdog/history";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
