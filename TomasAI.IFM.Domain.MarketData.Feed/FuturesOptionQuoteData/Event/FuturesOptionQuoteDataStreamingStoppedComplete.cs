using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

public static class FuturesOptionQuoteDataStreamingStoppedComplete
{
    static FuturesOptionQuoteDataStreamingStoppedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesOptionQuoteDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesOptionQuoteDataStreamingStoppedCompleteEvent e, IEventActorContext context, FuturesOptionQuoteDataEventParameters p)
    {
        var source = $"FuturesOptionQuoteDataStreamingStoppedCompleteEvent for QuoteId: {e.QuoteId}";
        try
        {
            foreach (var o in e.FuturesOptionQuotes)
            {
                p.MarketDataSnapshotApi.StopStreamingFuturesOptionQuoteData(o.RequestId);
                p.BlackboardService.FuturesOptionQuoteData.Clear(o.Id);
                await context.DeleteStreamingRequestIdAsync(new FeedId(o.RequestId));
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            p.MarketDataSnapshotApi.Stop();
            p.BlackboardService.FuturesOptionQuote.Clear(e.QuoteId);
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
