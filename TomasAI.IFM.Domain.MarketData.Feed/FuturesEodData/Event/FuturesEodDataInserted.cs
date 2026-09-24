using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

/// <summary>Handles the FuturesEodDataInsertedEvent message in the FuturesEodDataEventActor lifecycle.</summary>
public static class FuturesEodDataInserted
{
    static FuturesEodDataInserted()
    {
        ServiceId = $"{LogSourceType.FuturesEodDataEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>Projects the futures EOD event and returns whether the projection succeeded.</summary>
public static async ValueTask<bool> ExecuteAsync(
    this FuturesEodDataInsertedEvent e,
    IEventActorContext context,
    IEventActorContext eventApi,
    FuturesEodDataEventParameters p, ILogger<FuturesEodDataEventActor> logger)
    {
        var source = $"FuturesEodDataInsertedEvent for EntityId: {e.EntityId}";
        try
        {
            p.BlackboardService.MarketDataFeed.FuturesEodData.Set(e.FuturesEodData.ContractId, e.FuturesEodData.ValueDate, e.FuturesEodData);
            await eventApi.SendFuturesEodDataUpdatedEventAsync(e);
            logger.LogInformationEvent(ServiceId, "{Source}: futures eod data {ContractId} {ClosePrice}",
                source, e.FuturesEodData.ContractId, Convert.ToDecimal(e.FuturesEodData.ClosePrice));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures eod data {ContractId} insert failed", source, e.FuturesEodData.ContractId);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesEodDataEvent, FuturesEodDataInsertedEvent.ErrorCode, ex.GetErrorMessage());
        }
        return false;

    }



}
