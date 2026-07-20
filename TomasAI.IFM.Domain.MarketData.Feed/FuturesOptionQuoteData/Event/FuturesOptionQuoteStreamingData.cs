using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

public static class FuturesOptionQuoteStreamingData
{
    static FuturesOptionQuoteStreamingData()
    {
        ServiceId = $"{LogSourceType.FuturesOptionQuoteDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask ExecuteAsync(this FuturesOptionQuoteStreamingDataEvent e, IEventActorContext context, FuturesOptionQuoteDataEventParameters p)
    {
        var source = $"FuturesOptionQuoteStreamingDataEvent for QuoteId: {e.QuoteId}";
        try
        {
            var futuresOptionQuoteMap = p.BlackboardService.FuturesOptionQuote.Get(e.QuoteId);
            var optionContractId = futuresOptionQuoteMap[e.RequestId].ContractId;
            await context.InsertFuturesOptionQuoteDataAsync(e.QuoteId, optionContractId, e.QuoteData);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionQuoteDataEvent, $"Quote Data: {optionContractId}");
            p.Logger.LogInformationEvent(ServiceId, "{source}", source);
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionQuoteDataEvent, e.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{source} failed", source);
        }
    }
}
