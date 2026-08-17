using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>
/// Produces the temporary legacy market-outlook trade signal after a realtime
/// ITI projection completes. The compatibility projection is itself realtime.
/// </summary>
internal static class FuturesItiSignalGeneratedComplete
{
    static readonly string ServiceId = $"{LogSourceType.FuturesItiSignalEvent}";

    internal static async ValueTask<bool> ExecuteRealtimeAsync(
        this FuturesItiSignalGeneratedCompleteEvent source,
        IEventActorContext context,
        IRealtimeProjector<FuturesItiSignalRealtimeActor> projector,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        try
        {
            var contractId = source.EntityId.ContractId;
            var valueDate = source.FuturesItiSignal?.ValueDate ?? source.EntityId.ValueDate;
            var futuresEodDataTask = context.GetFuturesEodDataAsync(contractId, valueDate).AsTask();
            var futuresRsiSignalTask = context.GetFuturesRsiSignalAsync(
                contractId,
                valueDate,
                TimeFrameType.Daily,
                14).AsTask();
            var futuresTdiSignalTask = context.GetFuturesTdiSignalAsync(
                contractId,
                valueDate,
                TimeFrameType.FifteenSeconds).AsTask();
            var futuresItiSignalDataTask = context.GetFuturesItiSignalDataAsync(
                contractId,
                valueDate,
                source.EntityId.TimePeriod).AsTask();
            var vixFuturesPriceTask = context.GetVixFuturesEodDataClosePriceAsync(valueDate).AsTask();
            await Task.WhenAll(
                futuresEodDataTask,
                futuresRsiSignalTask,
                futuresTdiSignalTask,
                futuresItiSignalDataTask,
                vixFuturesPriceTask).ConfigureAwait(false);

            var futuresEodData = await futuresEodDataTask.ConfigureAwait(false);
            var futuresRsiSignal = await futuresRsiSignalTask.ConfigureAwait(false);
            var futuresTdiSignal = await futuresTdiSignalTask.ConfigureAwait(false);
            var futuresItiSignalData = await futuresItiSignalDataTask.ConfigureAwait(false);
            var vixFuturesPrice = await vixFuturesPriceTask.ConfigureAwait(false);
            if (futuresEodData is null
                || futuresRsiSignal is null
                || futuresTdiSignal is null
                || futuresItiSignalData is null
                || vixFuturesPrice == 0)
                return false;

            var command = new UpdateFuturesTradeSignalCommand(
                futuresEodData,
                futuresRsiSignal,
                futuresTdiSignal,
                futuresItiSignalData,
                vixFuturesPrice,
                TimeFrameType.FifteenSeconds);
            _ = command.Compute(out FuturesTradeSignalCompute compute);
            var entityId = command.EntityId;
            var updated = new FuturesTradeSignalUpdatedEvent
            {
                Subject = new ActorSubject(
                    ActorType.Realtime,
                    FuturesItiSignalRealtimeActor.ActorName,
                    FuturesItiSignalRealtimeActor.TradeSignalUpdatedVerb,
                    entityId.Format()),
                Id = Guid.NewGuid(),
                EntityId = entityId,
                CommandId = source.CommandId,
                AggregateId = source.AggregateId,
                EventSource = nameof(FuturesItiSignalGeneratedCompleteEvent),
                ReceivedOn = DateTime.UtcNow,
                FuturesTradeSignal = compute.FuturesTradeSignal,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = source.UserName
            };
            return await projector.ProcessRealtimeEventAsync(updated).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await statusConsoleWriter.WriteConsoleAsync(
                LogSourceType.FuturesItiSignalEvent,
                FuturesItiSignalGeneratedCompleteEvent.ErrorCode,
                exception.GetErrorMessage()).ConfigureAwait(false);
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "Realtime ITI completion for {ContractId} failed",
                source.EntityId.ContractId);
            throw;
        }
    }
}
