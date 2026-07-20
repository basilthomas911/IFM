using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

public static class FuturesRsiSignalStopped
{
    static FuturesRsiSignalStopped()
    {
        ServiceId = $"{LogSourceType.FuturesRsiSignalEvent}";
    }

    static string ServiceId { get; } = default!;

    /// <summary>
    /// Executes the stop operation for a Futures RSI signal timer, halting the generation of RSI signals for the specified contract and value date.
    /// </summary>
    /// <param name="e">The event containing details about the stopped Futures RSI signal.</param>
    /// <param name="context">The event actor context.</param>
    /// <param name="statusConsoleWriter">The status console writer for logging messages.</param>
    /// <param name="logger">The logger for recording error messages.</param>
    /// <returns>A value indicating whether the execution completed successfully. Returns <see langword="true"/> if the operation
    /// succeeded; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesRsiSignalStoppedEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"FuturesRsiSignalStoppedEvent for ContractId: {e.EntityId.ContractId}, TimePeriod: {e.EntityId.TimePeriod}, PeriodLength: {e.EntityId.PeriodLength}";
        try
        {
            e.StopTimer();
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiSignalStoppedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  {ContractId} handler failed", source, e.EntityId.ContractId);
        }
        return false;
    }
}
