namespace TomasAI.IFM.Shared.Application;

public static class MarketDataFeedUriPath
{
    public const string StartMarketDataFeed = "/api/marketdata/feed/start";
    public const string StopMarketDataFeed = "/api/marketdata/feed/stop";
    public const string ResetMarketDataFeed = "/api/marketdata/feed/reset";
    public const string InsertFuturesEodData = "/api/marketdata/futures/eod/insert";
    public const string InsertFuturesTickData = "/api/marketdata/futures/tick/insert";
    public const string InsertFuturesOptionTickData = "/api/marketdata/futures/option/tick/insert";
    public const string InsertVixFuturesEodData = "/api/marketdata/futures/eod/vix/insert";
    public const string InsertFuturesBarData = "/api/marketdata/futures/bardata/insert";
    public const string DeleteFuturesBarData = "/api/marketdata/futures/bardata/delete";
    public const string InsertFuturesClosingPrice = "/api/marketdata/futures/closingprice/insert";
    public const string TurnTradeLiveFeedOn = "/api/marketdata/livefeed/turn/on";
    public const string TurnTradeLiveFeedOff = "/api/marketdata/livefeed/turn/off";
    public const string AddTradeLiveFeed = "/api/marketdata/livefeed/trade/add";
    public const string RemoveTradeLiveFeed = "/api/marketdata/livefeed/trade/remove";
    public const string HaltTradeLiveFeed = "/api/marketdata/livefeed/trade/halt";
    public const string DeleteStreamingRequestId = "/api/marketdata/streaming/requestid/delete";
    public const string StartFuturesOptionTickDataStreaming = "/api/marketdata/futures/option/tick/streaming/start";
    public const string StopFuturesOptionTickDataStreaming = "/api/marketdata/futures/option/tick/streaming/stop";
    public const string StartFuturesTickDataStreaming = "/api/marketdata/futures/tick/streaming/start";
    public const string StopFuturesTickDataStreaming = "/api/marketdata/futures/tick/streaming/stop";
    public const string StartFuturesBarDataStreaming = "/api/marketdata/futures/bardata/streaming/start";
    public const string StopFuturesBarDataStreaming = "/api/marketdata/futures/bardata/streaming/stop";
    public const string EnableTradeLiveFeed = "/api/marketdata/livefeed/trade/enable";
    public const string DisableTradeLiveFeed = "/api/marketdata/livefeed/trade/disable";
    public const string RemoveTradeLiveFeeds = "/api/marketdata/livefeeds/trade/remove";
}
