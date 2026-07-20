using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;

public static class FuturesEodDataInsertedComplete
{
    static FuturesEodDataInsertedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesItiSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the FuturesEodDataInsertedCompleteEvent to generate Futures ITI Signal based on the inserted EOD data and corresponding VIX EOD data.
    /// </summary>
    /// <param name="e">The event instance containing the inserted EOD data.</param>
    /// <param name="context">The context in which the event is processed, supplying information necessary for asynchronous operations.</param>
    /// <param name="statusConsoleWriter">The writer used to output status messages to the console.</param>
    /// <param name="logger">The logger used to log error messages.</param>
    /// <returns>A value indicating whether the execution completed successfully. Returns <see langword="true"/> if the operation
    /// succeeded; otherwise, <see langword="false"/>.</returns>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesEodDataInsertedCompleteEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"FuturesEodDataInsertedCompleteEvent for ContractId: {e.FuturesEodData.ContractId}, ValueDate: {e.FuturesEodData.ValueDate}";
        try
        {
            var contractId = e.FuturesEodData.ContractId;
            var valueDate = e.FuturesEodData.ValueDate;
            var futuresEodData = await context.GetFuturesEodDataAsync(contractId, valueDate);
            var vixFuturesEodData = await context.GetVixFuturesEodDataAsync(valueDate);
            if (futuresEodData is null || vixFuturesEodData is null)
                return false;

            await context.GenerateFuturesItiSignalAsync(
                futuresEodData.ContractId,
                futuresEodData.ValueDate,
                TradeTimePeriodType.Weekly,
                DateTime.Now,
                Convert.ToDouble(futuresEodData.ClosePrice),
                Convert.ToDouble(vixFuturesEodData.ClosePrice));
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, FuturesEodDataInsertedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  {ContractId} handler failed", source, e.FuturesEodData.ContractId);
        }
        return false;
    }
}
