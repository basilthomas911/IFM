using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Extensions;

/// <summary>Translates individual current-contract trades into durable VWAP commands.</summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Forwards one trade-originated update without retaining calculation state.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IFuturesVwapSignalRealtimeContext context,
        FuturesContractV3ReadModel currentContract,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentContract);
        ArgumentNullException.ThrowIfNull(logger);
        if (@event.UpdateSource != FuturesMarketPriceUpdateSource.Trade
            || @event.Price.Trade is not { } trade
            || !StringComparer.Ordinal.Equals(@event.Price.ContractId, currentContract.ContractId))
            return true;
        var configuration = FuturesVwapConfiguration.Standard;
        var entityId = new FuturesVwapSignalEntityId(
            @event.Price.ContractId, @event.Price.ValueDate, configuration.ConfigurationId);
        var session = context.SessionCalendar.GetSession(@event.Price.ValueDate);
        var observation = new FuturesVwapTradeObservation
        {
            ContractId = @event.Price.ContractId,
            ValueDate = @event.Price.ValueDate,
            Price = trade.LastPrice,
            Size = trade.LastSize,
            SourceSequence = trade.SourceSequence,
            EventTimestampUtc = trade.EventTimestamp.ToUniversalTime(),
            Action = MapAction(trade.NormalizedTradeAction),
            Conditions = MapConditions(trade.NormalizedTradeConditionFlags),
            StreamEpochId = trade.StreamEpochId,
            TradeOrdinal = trade.TradeOrdinal,
            SessionStartUtc = session.StartUtc,
            SessionEndUtc = session.EndUtc
        };
        var result = await context.UpdateFuturesVwapSignalAsync(
            entityId, observation, configuration).ConfigureAwait(false);
        if (result is ServiceFailed<GuidResult>)
            logger.LogError("VWAP command rejected contract {ContractId} trade ordinal {TradeOrdinal}.",
                observation.ContractId, observation.TradeOrdinal);
        return result is not ServiceFailed<GuidResult>;
    }

    static FuturesVwapTradeAction MapAction(NormalizedTradeAction action) => action switch
    {
        NormalizedTradeAction.New => FuturesVwapTradeAction.New,
        NormalizedTradeAction.Change => FuturesVwapTradeAction.Change,
        NormalizedTradeAction.Cancel => FuturesVwapTradeAction.Cancel,
        NormalizedTradeAction.Correct => FuturesVwapTradeAction.Correct,
        NormalizedTradeAction.Clear => FuturesVwapTradeAction.Clear,
        NormalizedTradeAction.None => FuturesVwapTradeAction.None,
        _ => FuturesVwapTradeAction.Unknown
    };

    static FuturesVwapTradeConditionFlags MapConditions(NormalizedTradeConditionFlags conditions)
    {
        var result = FuturesVwapTradeConditionFlags.None;
        if (conditions.HasFlag(NormalizedTradeConditionFlags.Snapshot))
            result |= FuturesVwapTradeConditionFlags.Snapshot;
        if (conditions.HasFlag(NormalizedTradeConditionFlags.UndefinedPrice))
            result |= FuturesVwapTradeConditionFlags.UndefinedPrice;
        if (conditions.HasFlag(NormalizedTradeConditionFlags.Replay))
            result |= FuturesVwapTradeConditionFlags.Replay;
        return result;
    }
}
