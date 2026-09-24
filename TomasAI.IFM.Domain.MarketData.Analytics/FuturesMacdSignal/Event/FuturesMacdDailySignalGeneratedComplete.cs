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
    static string ServiceId { get; } = default!;

    /// <summary>Publishes the completed daily MACD observation to market outlook.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdDailySignalGeneratedCompleteEvent e, IFuturesMacdSignalEventContext context, ILogger<FuturesMacdSignalEventActor> logger)
    {
        var source = $"FuturesMacdSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent, FuturesMacdDailySignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
        }
        return false;
    }
}
