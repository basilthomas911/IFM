using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Recovery;

/// <summary>Translates bounded normalized historical trades into private VWAP recovery commands.</summary>
public sealed class FuturesVwapHistoricalReplayPublisher(
    IActorService actorService,
    IMarketSessionCalendar sessionCalendar) : IHistoricalReplayPublisher
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Dictionary<Guid, AttemptState> attempts = [];

    /// <inheritdoc />
    public async ValueTask PublishAsync(
        NormalizedHistoricalBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!attempts.TryGetValue(batch.DataLoadAttemptId, out var attempt))
                attempts.Add(batch.DataLoadAttemptId, attempt = new());
            var configuration = FuturesVwapConfiguration.Standard;
            var groups = batch.Trades
                .GroupBy(value => new FuturesVwapSignalEntityId(
                    value.ContractId, value.ValueDate, configuration.ConfigurationId))
                .ToDictionary(group => group.Key, group => group.ToArray());
            foreach (var group in groups)
                attempt.Track(group.Key);

            foreach (var entityId in attempt.Entities.ToArray())
            {
                var containsTrades = groups.TryGetValue(entityId, out var sourceTrades);
                if (!containsTrades && !batch.IsFinal) continue;
                var session = sessionCalendar.GetSession(entityId.ValueDate);
                var trades = sourceTrades?.Select(value => new FuturesVwapTradeObservation
                {
                    ContractId = value.ContractId,
                    ValueDate = value.ValueDate,
                    Price = value.Price,
                    Size = value.Size,
                    SourceSequence = value.SourceSequence,
                    EventTimestampUtc = value.EventTimestampUtc.ToUniversalTime(),
                    Action = MapAction(value.Action),
                    Conditions = MapConditions(value.Conditions),
                    StreamEpochId = batch.DataLoadAttemptId,
                    SessionStartUtc = session.StartUtc,
                    SessionEndUtc = session.EndUtc
                }).ToArray() ?? [];
                var ordinal = attempt.NextOrdinal(entityId);
                RecoverFuturesVwapSignalCommand command = new()
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new(ActorType.Command, UpdateFuturesVwapSignalCommand.Actor,
                        RecoverFuturesVwapSignalCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    RecoveryGenerationId = batch.DataLoadAttemptId,
                    BatchOrdinal = ordinal,
                    IsFirstBatch = ordinal == 0,
                    IsFinalBatch = batch.IsFinal,
                    Trades = trades,
                    Configuration = configuration
                };
                var result = await actorService.RequestAsync<RecoverFuturesVwapSignalCommand,
                    FuturesVwapSignalEntityId>(command).ConfigureAwait(false);
                if (!result.Success)
                    throw new InvalidOperationException(
                        $"VWAP recovery command failed for {entityId}: {result.ErrorMessage}");
            }
            if (batch.IsFinal) attempts.Remove(batch.DataLoadAttemptId);
        }
        finally { gate.Release(); }
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

    sealed class AttemptState
    {
        readonly Dictionary<FuturesVwapSignalEntityId, long> ordinals = [];
        internal IEnumerable<FuturesVwapSignalEntityId> Entities => ordinals.Keys;
        internal void Track(FuturesVwapSignalEntityId entityId) => ordinals.TryAdd(entityId, -1);
        internal long NextOrdinal(FuturesVwapSignalEntityId entityId) =>
            ordinals[entityId] = checked(ordinals[entityId] + 1);
    }
}
