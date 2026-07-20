using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public static class FuturesOptionTickDataInserted
{
    static FuturesOptionTickDataInserted()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesOptionTickDataInsertedEvent e, IEventActorContext context, FuturesOptionTickDataEventParameters p)
    {
        var source = $"FuturesOptionTickDataInsertedEvent for EntityId: {e.EntityId}";
        try
        {
            await context.SendOptionTradeTickPriceDataUpdatedEventAsync(e);
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, FuturesOptionTickDataInsertedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option tick data {ContractId} insert failed", source, e.TickData.ContractId);
        }
        return false;
    }

}
