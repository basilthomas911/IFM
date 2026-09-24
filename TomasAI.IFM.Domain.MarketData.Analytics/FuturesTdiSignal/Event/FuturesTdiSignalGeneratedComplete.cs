using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;

/// <summary>Handles the FuturesTdiSignalGeneratedCompleteEvent message in the FuturesTdiSignalEventActor lifecycle.</summary>
public static class FuturesTdiSignalGeneratedComplete
{
    static FuturesTdiSignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesTdiSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="context"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTdiSignalGeneratedCompleteEvent e,
        IEventActorContext<FuturesTdiSignalEventActor> context,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesTdiSignalEventActor> logger)
    {
        var source = $"FuturesTdiSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            await context.PublishMarketOutlookComponentAsync(e).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTdiSignalEvent, FuturesTdiSignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
        }
        return false;
    }
}
