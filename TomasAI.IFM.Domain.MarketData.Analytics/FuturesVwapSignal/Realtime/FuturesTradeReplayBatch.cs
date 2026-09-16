using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime;

/// <summary>Translates one private source replay batch into a durable VWAP recovery command.</summary>
public static class FuturesTradeReplayBatch
{
    /// <summary>Applies a bounded ordered replay batch and its final live-stream handoff.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTradeReplayBatchRealtimeEvent @event,
        IFuturesVwapSignalRealtimeContext context,
        FuturesContractV3ReadModel currentContract,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentContract);
        ArgumentNullException.ThrowIfNull(logger);
        if (!StringComparer.Ordinal.Equals(@event.EntityId.ContractId, currentContract.ContractId))
            return true;

        var configuration = FuturesVwapConfiguration.Standard;
        var entityId = new FuturesVwapSignalEntityId(
            @event.EntityId.ContractId, @event.EntityId.ValueDate, configuration.ConfigurationId);
        var session = context.SessionCalendar.GetSession(@event.EntityId.ValueDate);
        var trades = @event.Trades.Select(value => new FuturesVwapTradeObservation
        {
            ContractId = @event.EntityId.ContractId,
            ValueDate = @event.EntityId.ValueDate,
            Price = value.Price,
            Size = value.Size,
            SourceSequence = value.SourceSequence,
            EventTimestampUtc = value.EventTimestampUtc.ToUniversalTime(),
            Action = MapAction(value.Action),
            Conditions = MapConditions(value.Conditions),
            StreamEpochId = @event.RecoveryGenerationId,
            SessionStartUtc = session.StartUtc,
            SessionEndUtc = session.EndUtc
        }).ToArray();

        var result = await context.RecoverFuturesVwapSignalAsync(
            entityId,
            @event.RecoveryGenerationId,
            @event.BatchOrdinal,
            @event.IsFirstBatch,
            @event.IsFinalBatch,
            @event.LiveStreamEpochId,
            trades,
            configuration).ConfigureAwait(false);
        if (result is ServiceFailed<GuidResult> failed)
        {
            FuturesVwapSignalRealtimeLogging.ReplayBatchFailed(
                logger, entityId.ContractId, @event.BatchOrdinal, @event.IsFinalBatch,
                failed.ErrorCode, failed.ErrorMessage);
            return false;
        }
        if (@event.IsFinalBatch)
            FuturesVwapSignalRealtimeLogging.ReplayCompleted(
                logger, entityId.ContractId, entityId.ValueDate,
                @event.LiveStreamEpochId, @event.BatchOrdinal + 1);
        return true;
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
        var result = FuturesVwapTradeConditionFlags.Replay;
        if (conditions.HasFlag(NormalizedTradeConditionFlags.Snapshot))
            result |= FuturesVwapTradeConditionFlags.Snapshot;
        if (conditions.HasFlag(NormalizedTradeConditionFlags.UndefinedPrice))
            result |= FuturesVwapTradeConditionFlags.UndefinedPrice;
        return result;
    }
}
