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
        ILogger<FuturesItiSignalRealtimeActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(commandApi);
        ArgumentNullException.ThrowIfNull(marketDataApi);
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

            if (!marketDataApi.TryGetCurrentlyTradedFuturesContract("ES", out var esContract))
            {
                throw new FuturesContractRolloverConfigurationException(
                    "The current ES futures contract is not available in the startup rollover registry.");
            }
            if (!StringComparer.Ordinal.Equals(esContract.ContractId, @event.Price.ContractId))
                return true;

            if (!marketDataApi.TryGetCurrentlyTradedFuturesContract("VX", out var vxContract))
            {
                throw new FuturesContractRolloverConfigurationException(
                    "The current VX futures contract is not available in the startup rollover registry.");
            }

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

            await commandApi.GenerateFuturesItiSignalAsync(
                esContract.ContractId,
                @event.Price.ValueDate,
                TimeFrameType.Weekly,
                esTrade.EventTimestamp.UtcDateTime,
                Convert.ToDouble(esTrade.LastPrice),
                Convert.ToDouble(vxPrice)).ConfigureAwait(false);
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
