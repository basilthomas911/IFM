using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

public static class FuturesOptionQuoteDataInsertedComplete
{
    static FuturesOptionQuoteDataInsertedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesOptionQuoteDataEvent}";
    }

    static string ServiceId { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="context"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesOptionQuoteDataInsertedCompleteEvent e, IEventActorContext context, FuturesOptionQuoteDataEventParameters p)
    {
        var source = $"FuturesOptionQuoteDataInsertedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return await context.SendFuturesOptionQuoteDataUpdatedEventAsync(e);
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionQuoteDataEvent, e.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}  failed", source);
        }
        return false;
    }
}
