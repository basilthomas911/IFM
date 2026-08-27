using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Extensions;

/// <summary>Translates eligible routed VX trades into durable Command actor updates.</summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Forwards one front or back VX trade without retaining calculation state.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IFuturesVxTermStructureSignalRealtimeContext context,
        FuturesTermStructureContracts contracts,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        if (@event.UpdateSource != FuturesMarketPriceUpdateSource.Trade
            || @event.Price.Trade is not { } trade
            || trade.LastPrice <= 0
            || trade.NormalizedTradeAction is NormalizedTradeAction.Cancel
                or NormalizedTradeAction.Clear
                or NormalizedTradeAction.None)
            return true;
        var contract = StringComparer.Ordinal.Equals(@event.Price.ContractId, contracts.Front.ContractId)
            ? contracts.Front
            : StringComparer.Ordinal.Equals(@event.Price.ContractId, contracts.Back.ContractId)
                ? contracts.Back : null;
        if (contract is null) return true;
        var leg = StringComparer.Ordinal.Equals(contract.ContractId, contracts.Front.ContractId)
            ? FuturesVxTermStructureLeg.Front : FuturesVxTermStructureLeg.Back;
        var configuration = FuturesVxTermStructureConfiguration.Standard;
        var entityId = new FuturesVxTermStructureSignalEntityId(
            @event.Price.ValueDate, contracts.Front.ContractId, contracts.Back.ContractId,
            configuration.ConfigurationId);
        var observation = new FuturesVxTermStructureLegObservation
        {
            Leg = leg,
            ContractId = contract.ContractId,
            Expiry = contract.LastTradeDate,
            Price = trade.LastPrice,
            SourceSequence = trade.SourceSequence,
            SourceTimestampUtc = trade.EventTimestamp.ToUniversalTime(),
            StreamEpochId = trade.StreamEpochId
        };
        var result = await context.UpdateFuturesVxTermStructureSignalAsync(
            entityId, observation, configuration).ConfigureAwait(false);
        if (result is ServiceFailed<GuidResult>)
            logger.LogError("VX term-structure command rejected {Leg} contract {ContractId} sequence {Sequence}.",
                leg, contract.ContractId, trade.SourceSequence);
        return result is not ServiceFailed<GuidResult>;
    }
}
