using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

public static class FuturesOptionQuoteDataStreamingStartedComplete
{
    static FuturesOptionQuoteDataStreamingStartedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesOptionQuoteDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesOptionQuoteDataStreamingStartedCompleteEvent e, IEventActorContext context, FuturesOptionQuoteDataEventParameters p)
    {
        var source = $"FuturesOptionQuoteDataStreamingStartedCompleteEvent for QuoteId: {e.QuoteId}";
        try
        {
            p.MarketDataSnapshotApi.Start();
            foreach (var o in e.FuturesOptionQuotes)
            {
                var optionContract = e.FuturesOptionContracts.Where(a => a.ContractId == o.ContractId).FirstOrDefault();
                p.MarketDataSnapshotApi.StartStreamingFuturesOptionQuoteData(o.RequestId, optionContract!, o);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            p.BlackboardService.FuturesOptionQuote.Set(e.QuoteId, e.FuturesOptionQuotes);
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
