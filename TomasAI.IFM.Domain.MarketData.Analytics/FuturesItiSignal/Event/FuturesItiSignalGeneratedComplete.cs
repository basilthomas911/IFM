using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;

public static class FuturesItiSignalGeneratedComplete
{
    static FuturesItiSignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesItiSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the completion of the Futures ITI signal generation process. It retrieves necessary data, updates the trade signal, and logs any errors that occur during the process.
    /// </summary>
    /// <param name="e">The event instance containing details required for generating the futures trade signal, including the entity
    /// identifier.</param>
    /// <param name="context">The context in which the event is processed, supplying information necessary for asynchronous operations.</param>
    /// <param name="statusConsoleWriter">The writer used to output status messages to the console.</param>
    /// <param name="logger">The logger used to log error messages.</param>
    /// <returns>A value indicating whether the execution completed successfully. Returns <see langword="true"/> if the operation
    /// succeeded; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesItiSignalGeneratedCompleteEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"FuturesItiSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            var contractId = e.EntityId.ContractId;
            var valueDate = e.EntityId.ValueDate;
            var futuresEodData = await context.GetFuturesEodDataAsync(contractId, valueDate);
            var futuresRsiSignal = await context.GetFuturesRsiSignalAsync(contractId, valueDate, TradeTimePeriodType.Daily, 14);
            var futuresTdiSignal = await context.GetFuturesTdiSignalAsync(contractId, valueDate);
            var futuresItiSignalData = await context.GetFuturesItiSignalDataAsync(contractId, valueDate, e.EntityId.TimePeriod);
            var vixFuturesPrice = await context.GetVixFuturesEodDataClosePriceAsync(valueDate);
            if (futuresEodData is null || futuresRsiSignal is null || futuresTdiSignal is null || futuresItiSignalData is null || vixFuturesPrice == 0)
                return false;
            await context.UpdateFuturesTradeSignalAsync(futuresEodData!, futuresRsiSignal!, futuresTdiSignal!, futuresItiSignalData!, vixFuturesPrice, TradeTimePeriodType.FifteenSeconds);
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, FuturesItiSignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
        }
        return false;
    }
}
