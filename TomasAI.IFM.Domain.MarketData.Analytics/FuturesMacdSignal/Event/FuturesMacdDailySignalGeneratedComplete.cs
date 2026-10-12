using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

/// <summary>Handles the FuturesMacdDailySignalGeneratedCompleteEvent message in the FuturesMacdSignalEventActor lifecycle.</summary>
public static class FuturesMacdDailySignalGeneratedComplete
{
    static FuturesMacdDailySignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesMacdSignalEvent}";
    }
    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>Publishes the completed daily MACD observation to market outlook.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdDailySignalGeneratedCompleteEvent e, IFuturesMacdSignalEventContext context, ILogger<FuturesMacdSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(e);
        var source = $"FuturesMacdSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
            await (context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent, FuturesMacdDailySignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
