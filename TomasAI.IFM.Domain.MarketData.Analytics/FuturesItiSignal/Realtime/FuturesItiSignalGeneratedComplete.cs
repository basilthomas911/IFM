using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot;

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
            await source.PublishAsync(context).ConfigureAwait(false);
            if (!FuturesTradeSignalPrerequisites.ShouldGenerate(source))
                return true;

            var prerequisites = await FuturesTradeSignalPrerequisites.LoadAsync(source, context)
                .ConfigureAwait(false);
            if (prerequisites.Inputs is not { } inputs)
            {
                logger.LogTrace(
                    "Futures Trade Signal is not ready for {ContractId}/{ValueDate}: {MissingInputs}",
                    source.EntityId.ContractId,
                    source.EntityId.ValueDate,
                    prerequisites.MissingInputs);
                return true;
            }

            var command = new UpdateFuturesTradeSignalCommand(
                inputs.FuturesEodData,
                inputs.FuturesRsiSignal,
                inputs.FuturesTdiSignal,
                inputs.FuturesItiSignalData,
                inputs.VixFuturesPrice,
                FuturesTradeSignalPrerequisites.SignalTimePeriod);
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
