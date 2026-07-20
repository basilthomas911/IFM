using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;

public static class MarketDataFeedCommandApiResult
{
    public static Task FromStartMarketDataFeedAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStopMarketDataFeedAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromResetMarketDataFeedAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertFuturesTickDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertFuturesOptionTickDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStartFuturesOptionTickDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStopFuturesOptionTickDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromDeleteStreamingRequestIdAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStartFuturesTickDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStopFuturesTickDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStartFuturesBarDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStopFuturesBarDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromEnableTradeLiveFeedAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromDisableTradeLiveFeedAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromAddTradeLiveFeedAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveTradeLiveFeedAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromRemoveTradeLiveFeedsAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertFuturesEodDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertVixFuturesEodDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertFuturesBarDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertFuturesClosingPriceAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromDeleteFuturesBarDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromInsertFuturesOptionQuoteDataAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStartFuturesOptionQuoteDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();

    public static Task FromStopFuturesOptionQuoteDataStreamingAsync(HttpResponse resp)
        => resp.SetResult();
}
