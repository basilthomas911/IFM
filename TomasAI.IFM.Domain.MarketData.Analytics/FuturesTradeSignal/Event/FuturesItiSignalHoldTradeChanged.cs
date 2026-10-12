using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;

/// <summary>Handles the FuturesItiSignalHoldTradeChangedEvent message in the FuturesTradeSignalEventActor lifecycle.</summary>
public static class FuturesItiSignalHoldTradeChanged
{
    static FuturesItiSignalHoldTradeChanged()
    {
        ServiceId = $"{LogSourceType.FuturesTradeSignalEvent}";
    }
    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="context"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesItiSignalHoldTradeChangedEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesTradeSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(statusConsoleWriter);
        var source = $"FuturesItiSignalHoldTradeChangedEvent for {e.FuturesItiSignalId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: complete handler failed", source);
            await (statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTradeSignalEvent, 19014, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
