using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

public static class FuturesRsiSignalStarted
{
    static FuturesRsiSignalStarted()
    {
        _serviceId = $"{LogSourceType.FuturesRsiSignalEvent}";
    }

    static string _serviceId { get; } = default!;

    /// <summary>
    /// Executes the start operation for a Futures RSI signal timer, initiating the generation of RSI signals for the specified contract and value date.
    /// </summary>
    /// <param name="e">The event containing details about the futures RSI signal to be processed, including the contract ID and value date.</param>
    /// <param name="context">The event actor context used to dispatch queries and commands.</param>
    /// <param name="statusConsoleWriter">The status console writer used to log messages and errors to the console.</param>
    /// <param name="logger">The logger used to log errors and informational messages related to the execution of the event handler.</param>
    /// <returns>A value indicating whether the execution completed successfully. Returns <see langword="true"/> if the operation
    /// succeeded; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesRsiSignalStartedEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"FuturesRsiSignalStartedEvent for ContractId: {e.EntityId.ContractId}, TimePeriod: {e.EntityId.TimePeriod}, PeriodLength: {e.EntityId.PeriodLength}";
        try
        {
            e.StartTimer(async o =>
            {
                try
                {
                    var futuresRsiSignalId = new FuturesRsiSignalId(e.EntityId.ContractId, e.EntityId.ValueDate,
                        e.EntityId.TimePeriod, e.EntityId.PeriodLength, TimeOnly.FromDateTime(DateTime.UtcNow));
                    await GenerateFuturesRsiSignalsAsync(futuresRsiSignalId);
                }
                catch (Exception ex)
                {
                    await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiSignalStartedEvent.ErrorCode, ex.GetErrorMessage());
                    logger.LogErrorEvent(_serviceId, ex.GetErrorMessage(), "{Source}:  {ContractId} handler failed", source, e.EntityId.ContractId);
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiSignalStartedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(_serviceId, ex.GetErrorMessage(), "{Source}:  {ContractId} handler failed", source, e.EntityId.ContractId);
        }
        return false;

        async ValueTask GenerateFuturesRsiSignalsAsync(FuturesRsiSignalId futuresRsiSignalId)
        {
            try
            {
                if (e.EntityId.ContractId.StartsWith("ES"))
                {
                    var futuresEodData = await context.GetLastFuturesEodDataAsync(e.EntityId.ContractId, e.ValueDate);
                    if (futuresEodData is not null)
                           await context.GenerateFuturesRsiSignalAsync(futuresRsiSignalId, futuresEodData.ClosePrice);
                }
            }
            catch (Exception ex)
            {
                await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiSignalStartedEvent.ErrorCode, ex.GetErrorMessage());
                logger.LogErrorEvent(_serviceId, ex.GetErrorMessage(), "{Source}:  {ContractId} handler failed", source, e.EntityId.ContractId);
            }
        }
    }

}
