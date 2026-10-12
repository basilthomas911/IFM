using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

/// <summary>Handles the FuturesRsiDailySignalGeneratedEvent message in the FuturesRsiSignalEventActor lifecycle.</summary>
public static class FuturesRsiDailySignalGenerated
{
    static FuturesRsiDailySignalGenerated()
    {
        ServiceId = $"{LogSourceType.FuturesRsiSignalEvent}";
    }
    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>
    /// 
    /// </summary>
    /// <param name="e"> </param>
    /// <param name="context"> </param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiDailySignalGeneratedEvent e,
        IFuturesRsiSignalEventContext context,
        ILogger<FuturesRsiSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(e);
        var source = $"FuturesRsiDailySignalGeneratedEvent for ContractId: {e.FuturesRsiSignal.ContractId}, ValueDate: {e.FuturesRsiSignal.ValueDate}";
        try
        {
            context.BlackboardService.MarketDataAnalytics.FuturesRsiDailySignal.Set(e.EntityId, e.FuturesRsiSignal);
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}:  event handler failed", source);
            await (context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiDailySignalGeneratedEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
