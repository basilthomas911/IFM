using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

public static class FuturesEodDataInserted
{
    static FuturesEodDataInserted()
    {
        ServiceId = $"{LogSourceType.FuturesEodDataEvent}";
    }
    static string ServiceId { get; } = default!;

    public static async ValueTask<bool> ExecuteAsync(this FuturesEodDataInsertedEvent e, IEventActorContext context, FuturesEodDataEventParameters p)
    {
        var source = $"FuturesEodDataInsertedEvent for EntityId: {e.EntityId}";
        try
        {
            p.BlackboardService.FuturesEodData.Set(e.FuturesEodData.ContractId, e.FuturesEodData.ValueDate, e.FuturesEodData);
            await context.SendFuturesEodDataUpdatedEventAsync(e);
            p.Logger.LogInformationEvent(ServiceId, "{Source}: futures eod data {ContractId} {ClosePrice}",
                source, e.FuturesEodData.ContractId, Convert.ToDecimal(e.FuturesEodData.ClosePrice));
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesEodDataEvent, FuturesEodDataInsertedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}: futures eod data {ContractId} insert failed", source, e.FuturesEodData.ContractId);
        }
        return false;

    }



}
