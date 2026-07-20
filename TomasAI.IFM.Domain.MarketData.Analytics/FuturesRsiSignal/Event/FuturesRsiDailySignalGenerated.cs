using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

public static class FuturesRsiDailySignalGenerated
{
    static FuturesRsiDailySignalGenerated()
    {
        ServiceId = $"{LogSourceType.FuturesRsiSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"> </param>
    /// <param name="context"> </param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <param name="blackboardService"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesRsiDailySignalGeneratedEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger, IBlackboardService blackboardService)
    {
        var source = $"FuturesRsiDailySignalGeneratedEvent for ContractId: {e.FuturesRsiSignal.ContractId}, ValueDate: {e.FuturesRsiSignal.ValueDate}";
        try
        {
                blackboardService.FuturesRsiDailySignal.Set(e.EntityId, e.FuturesRsiSignal);
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiDailySignalGeneratedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  event handler failed", source);
        }
        return false;
    }
}
