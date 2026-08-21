using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot;

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
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesItiSignalGeneratedCompleteEvent e,
        IEventActorContext context,
        IActorMarketDataAnalyticsCommandApi commandApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        var source = $"FuturesItiSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            await e.PublishUpdatedNotificationAsync(context, logger).ConfigureAwait(false);
            await e.PublishAsync(context).ConfigureAwait(false);
            if (!FuturesTradeSignalPrerequisites.ShouldGenerate(e))
                return true;

            var prerequisites = await FuturesTradeSignalPrerequisites.LoadAsync(e, context)
                .ConfigureAwait(false);
            if (prerequisites.Inputs is not { } inputs)
            {
                logger.LogTrace(
                    "Futures Trade Signal is not ready for {ContractId}/{ValueDate}: {MissingInputs}",
                    e.EntityId.ContractId,
                    e.EntityId.ValueDate,
                    prerequisites.MissingInputs);
                return true;
            }

            await commandApi.UpdateFuturesTradeSignalAsync(
                inputs.FuturesEodData,
                inputs.FuturesRsiSignal,
                inputs.FuturesTdiSignal,
                inputs.FuturesItiSignalData,
                inputs.VixFuturesPrice,
                FuturesTradeSignalPrerequisites.SignalTimePeriod);
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
