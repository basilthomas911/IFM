using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

public static class FuturesOptionQuoteDataStreamingStoppedComplete
{
    static FuturesOptionQuoteDataStreamingStoppedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesOptionQuoteDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesOptionQuoteDataStreamingStoppedCompleteEvent e, IEventActorContext context, IActorMarketDataFeedCommandApi commandApi, FuturesOptionQuoteDataEventParameters p)
    {
        var source = $"FuturesOptionQuoteDataStreamingStoppedCompleteEvent for QuoteId: {e.QuoteId}";
        try
        {
            foreach (var o in e.FuturesOptionQuotes)
            {
                p.MarketDataSnapshotApi.StopStreamingFuturesOptionQuoteData(o.RequestId);
                p.BlackboardService.MarketDataFeed.FuturesOptionQuoteData.Clear(o.Id);
                await commandApi.DeleteStreamingRequestIdAsync(new FeedId(o.RequestId));
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            p.MarketDataSnapshotApi.Stop();
            p.BlackboardService.MarketDataFeed.FuturesOptionQuote.Clear(e.QuoteId);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionQuoteDataEvent, $"{e.GetType().Name}: {e.QuoteId}");
            p.Logger.LogInformationEvent(ServiceId, "{source}", source);
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionQuoteDataEvent, e.ErrorCode, ex.Message);
            p.Logger.LogErrorEvent(ServiceId, ex, "{source} failed", source);
        }
        return false;
    }

}
