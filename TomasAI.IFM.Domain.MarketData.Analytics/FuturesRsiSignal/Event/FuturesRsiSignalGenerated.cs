using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

/// <summary>Handles the FuturesRsiSignalGeneratedEvent message in the FuturesRsiSignalEventActor lifecycle.</summary>
public static class FuturesRsiSignalGenerated
{
    static FuturesRsiSignalGenerated()
    {
        ServiceId = $"{LogSourceType.FuturesRsiSignalEvent}";
    }
    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"> </param>
    /// <param name="context"> </param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <param name="blackboardService"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesRsiSignalGeneratedEvent e, IFuturesRsiSignalEventContext context, ILogger<FuturesRsiSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(e);
        var source = $"FuturesRsiSignalGeneratedEvent for ContractId: {e.FuturesRsiSignal.ContractId}, ValueDate: {e.FuturesRsiSignal.ValueDate}";
        try
        {
            if (e.FuturesRsiSignal is { IsWarm: true, RSI: >= 0d }
                && e.FuturesRsiSignal.Metadata is { IsValid: true })
            {
                context.BlackboardService.MarketDataAnalytics.FuturesRsiSignal.Set(
                    e.EntityId,
                    e.FuturesRsiSignal);
            }
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}:  event handler failed", source);
            await (context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiSignalsGeneratedEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
