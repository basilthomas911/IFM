using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>
    /// Converts eligible current-contract ES market-price updates into realtime ITI projections.
/// </summary>
public static class FuturesMarketPriceUpdated
{
    static FuturesMarketPriceUpdated() =>
        ServiceId = $"{LogSourceType.FuturesMarketPriceUpdated}";

    static string ServiceId { get; }

    /// <summary>
    /// Processes a current ES trade update when both ES and VX streams are active
    /// and a fresh VX last-trade price is available.
    /// </summary>
    /// <param name="event">The routed normalized futures market-price event.</param>
    /// <param name="context">The owning realtime actor context.</param>
    /// <param name="projector">The one-attempt realtime source/storage/complete-or-fail projector.</param>
    /// <param name="marketDataApi">The provider-neutral current-contract and hot-price API.</param>
    /// <param name="streamOwnership">The actor-owned ES/VX stream lifecycle.</param>
    /// <param name="logger">The typed ITI realtime actor logger.</param>
    /// <returns>
    /// <see langword="true"/> when the event was handled or intentionally ignored;
    /// otherwise an exception is propagated to actor error handling.
    /// </returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IEventActorContext context,
        IRealtimeProjector<FuturesItiSignalRealtimeActor> projector,
        IMarketDataApi marketDataApi,
        FuturesItiSignalStreamOwnership streamOwnership,
        FuturesItiSignalRealtimeState realtimeState,
        ILogger<FuturesItiSignalRealtimeActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentNullException.ThrowIfNull(streamOwnership);
        ArgumentNullException.ThrowIfNull(realtimeState);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            if (@event.Price.AssetTypeId != AssetTypeId.Futures
                || @event.UpdateSource == FuturesMarketPriceUpdateSource.Quote
                || @event.Price.Trade is not { } esTrade)
                return true;

            if (!StringComparer.Ordinal.Equals(
                    @event.EntityId.ContractId,
                    @event.Price.ContractId)
                || @event.EntityId.ValueDate != @event.Price.ValueDate
                || @event.EntityId.AssetTypeId != @event.Price.AssetTypeId)
            {
                throw new MarketDataContractMappingException(
                    @event.EntityId.ContractId,
                    "the realtime event entity and price snapshot identities do not match");
            }

            FuturesItiSignalStreamContracts contracts;
            try
            {
                contracts = await streamOwnership.EnsureAsync(marketDataApi).ConfigureAwait(false);
            }
            catch (MarketDataApiNotRunningException)
            {
                // Feed shutdown closes epoch admission before draining already
                // accepted ticks. Those late realtime notifications are expected
                // and must not reacquire stream routes or create an error storm.
                return true;
            }
            var esContract = contracts.Es;
            if (!StringComparer.Ordinal.Equals(esContract.ContractId, @event.Price.ContractId))
                return true;

            var vxContract = contracts.Vx;

            if (!marketDataApi.IsTickDataStreamActive(esContract.ContractId)
                || !marketDataApi.IsTickDataStreamActive(vxContract.ContractId))
                return true;

            decimal vxPrice;
            try
            {
                vxPrice = await marketDataApi.GetFuturesPriceAsync(vxContract.ContractId)
                    .ConfigureAwait(false);
            }
            catch (FuturesLastPriceUnavailableException)
            {
                return true;
            }

            var evaluations = await realtimeState.EvaluateAsync(
                esContract.ContractId,
                @event.Price.ValueDate,
                esTrade.EventTimestamp.UtcDateTime,
                Convert.ToDouble(esTrade.LastPrice),
                Convert.ToDouble(vxPrice)).ConfigureAwait(false);
            foreach (var evaluation in evaluations)
            {
                var generated = CreateGeneratedEvent(@event, evaluation);
                var success = await projector.ProcessRealtimeEventAsync(generated)
                    .ConfigureAwait(false);
                if (success)
                    realtimeState.Confirm(evaluation);
            }
            return true;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{EventName} for {ContractId}: realtime ITI projection failed",
                nameof(FuturesMarketPriceUpdatedRealtimeEvent),
                @event.EntityId.ContractId);
            throw;
        }
    }

    static FuturesItiSignalGeneratedEvent CreateGeneratedEvent(
        FuturesMarketPriceUpdatedRealtimeEvent source,
        FuturesItiSignalEvaluation evaluation)
    {
        var command = evaluation.Command;
        var entityId = evaluation.Signal.EntityId;
        return new FuturesItiSignalGeneratedEvent
        {
            Subject = new(
                ActorType.Realtime,
                FuturesItiSignalRealtimeActor.ActorName,
                FuturesItiSignalGeneratedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = nameof(FuturesMarketPriceUpdatedRealtimeEvent),
            ReceivedOn = DateTime.UtcNow,
            FuturesItiSignal = evaluation.Signal,
            VixFuturesPrice = command.VixFuturesPrice,
            DeriveLongerPeriods = false,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = source.UserName
        };
    }
}
