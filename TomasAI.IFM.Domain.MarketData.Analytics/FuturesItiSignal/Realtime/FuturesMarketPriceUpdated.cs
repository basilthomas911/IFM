using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>
/// Converts eligible current-contract ES market-price updates into durable ITI commands.
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
    /// <param name="context">The realtime actor context used for the durable handoff.</param>
    /// <param name="commandApi">The actor-bound durable analytics command API.</param>
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
        IActorMarketDataAnalyticsCommandApi commandApi,
        IMarketDataApi marketDataApi,
        FuturesItiSignalStreamOwnership streamOwnership,
        FuturesItiSignalRealtimeState realtimeState,
        ILogger<FuturesItiSignalRealtimeActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(commandApi);
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentNullException.ThrowIfNull(streamOwnership);
        ArgumentNullException.ThrowIfNull(realtimeState);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            if (@event.Price.AssetTypeId != AssetTypeId.Futures
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

            var contracts = await streamOwnership.EnsureAsync(marketDataApi).ConfigureAwait(false);
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
                var command = evaluation.Command;
                var result = await commandApi.GenerateFuturesItiSignalAsync(
                    command.ContractId,
                    command.ValueDate,
                    command.TimePeriod,
                    command.Timestamp,
                    command.FuturesPrice,
                    command.VixFuturesPrice,
                    timeFrameStartValueDate: command.TimeFrameStartValueDate)
                    .ConfigureAwait(false);
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        result.ErrorMessage ?? "The durable ITI command failed.");
                }
                realtimeState.Confirm(evaluation);
            }
            return true;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{EventName} for {ContractId}: realtime ITI command handoff failed",
                nameof(FuturesMarketPriceUpdatedRealtimeEvent),
                @event.EntityId.ContractId);
            throw;
        }
    }
}
