using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

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
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiDailySignalGeneratedEvent e,
        IFuturesRsiSignalEventContext context,
        ILogger logger)
    {
        var source = $"FuturesRsiDailySignalGeneratedEvent for ContractId: {e.FuturesRsiSignal.ContractId}, ValueDate: {e.FuturesRsiSignal.ValueDate}";
        try
        {
            context.BlackboardService.MarketDataAnalytics.FuturesRsiDailySignal.Set(e.EntityId, e.FuturesRsiSignal);
        }
        catch (Exception ex)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiDailySignalGeneratedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  event handler failed", source);
        }
        return false;
    }
}
