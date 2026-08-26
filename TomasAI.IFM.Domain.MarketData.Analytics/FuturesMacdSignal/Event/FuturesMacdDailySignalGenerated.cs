using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

public static class FuturesMacdDailySignalGenerated
{
    static FuturesMacdDailySignalGenerated()
    {
        ServiceId = $"{LogSourceType.FuturesMacdSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdDailySignalGeneratedCompleteEvent e, IFuturesMacdSignalEventContext context, ILogger logger)
    {
        var source = $"FuturesMacdSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent, FuturesMacdDailySignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
        }
        return false;
    }
}
