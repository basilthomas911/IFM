using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

public static class FuturesRsiSignalGenerated
{
    static FuturesRsiSignalGenerated()
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
    public static async ValueTask<bool> ExecuteAsync(this FuturesRsiSignalGeneratedEvent e, IFuturesRsiSignalEventContext context, ILogger logger)
    {
        var source = $"FuturesRsiSignalGeneratedEvent for ContractId: {e.FuturesRsiSignal.ContractId}, ValueDate: {e.FuturesRsiSignal.ValueDate}";
        try
        {
                context.BlackboardService.MarketDataAnalytics.FuturesRsiSignal.Set(e.EntityId, e.FuturesRsiSignal);
        }
        catch (Exception ex)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiSignalsGeneratedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  event handler failed", source);
        }
        return false;
    }
}
