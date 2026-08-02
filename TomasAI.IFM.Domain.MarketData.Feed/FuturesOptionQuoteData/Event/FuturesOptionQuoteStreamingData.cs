using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

public static class FuturesOptionQuoteStreamingData
{
    static FuturesOptionQuoteStreamingData()
    {
        ServiceId = $"{LogSourceType.FuturesOptionQuoteDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask ExecuteAsync(this FuturesOptionQuoteStreamingDataEvent e, IEventActorContext context, IActorMarketDataFeedCommandApi commandApi, FuturesOptionQuoteDataEventParameters p)
    {
        var source = $"FuturesOptionQuoteStreamingDataEvent for QuoteId: {e.QuoteId}";
        try
        {
            var futuresOptionQuoteMap = p.BlackboardService.MarketDataFeed.FuturesOptionQuote.Get(e.QuoteId);
            var optionContractId = futuresOptionQuoteMap[e.RequestId].ContractId;
            await commandApi.InsertFuturesOptionQuoteDataAsync(e.QuoteId, optionContractId, e.QuoteData);
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
