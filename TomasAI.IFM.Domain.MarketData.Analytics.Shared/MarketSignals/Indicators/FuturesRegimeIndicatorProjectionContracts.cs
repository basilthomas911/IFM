using System.Collections.Concurrent;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;

/// <summary>Contains the ordered Regime Discovery indicator outputs derived from one observation.</summary>
[MessagePackObject]
public sealed record FuturesRegimeIndicatorSnapshot
{
    /// <summary>Gets the source observation.</summary>
    [Key(0)] public FuturesTradeSessionBarReadModel Observation { get; init; } = new();
    /// <summary>Gets RSI13 reserved for TDI.</summary>
    [Key(1)] public FuturesRegimeRsiSignalReadModel Rsi13 { get; init; } = new();
    /// <summary>Gets RSI14 reserved for Regime Discovery.</summary>
    [Key(2)] public FuturesRegimeRsiSignalReadModel Rsi14 { get; init; } = new();
    /// <summary>Gets EMA10/20/50/200.</summary>
    [Key(3)] public FuturesEmaSignalReadModel Ema { get; init; } = new();
    /// <summary>Gets EMA-centered BB10/20.</summary>
    [Key(4)] public FuturesBollingerBandSignalReadModel BollingerBand { get; init; } = new();
}

/// <summary>Provides the transitional latest-snapshot cache used until the unified MDSI-15 cache lands.</summary>
public static class FuturesRegimeIndicatorSnapshotCache
{
    static readonly ConcurrentDictionary<FuturesTradeSessionBarEntityId, FuturesRegimeIndicatorSnapshot> Latest = new();

    /// <summary>Sets the latest successfully persisted snapshot.</summary>
    public static void Set(FuturesTradeSessionBarEntityId entityId, FuturesRegimeIndicatorSnapshot snapshot) =>
        Latest[entityId] = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    /// <summary>Tries to get the latest successfully persisted snapshot.</summary>
    public static bool TryGet(
        FuturesTradeSessionBarEntityId entityId,
        out FuturesRegimeIndicatorSnapshot snapshot) => Latest.TryGetValue(entityId, out snapshot!);

    /// <summary>Clears transitional cache state during actor shutdown.</summary>
    public static void Clear() => Latest.Clear();
}

/// <summary>Publishes a complete indicator snapshot before storage-first projection.</summary>
[MessagePackObject]
public sealed record FuturesRegimeIndicatorsGeneratedRealtimeEvent
    : IEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the owning realtime actor name.</summary>
    public const string Actor = "FuturesRegimeIndicators";
    /// <summary>Gets the source event verb.</summary>
    public const string Verb = "Generated";
    /// <summary>Gets the stable projection error code.</summary>
    public const int ErrorId = 26030;

    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the immutable ordered calculation snapshot.</summary>
    [Key(8)] public FuturesRegimeIndicatorSnapshot Snapshot { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesRegimeIndicatorsGeneratedRealtimeEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates the conventional projection-completed event.</summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ICompleteEvent<FuturesTradeSessionBarEntityId> completed =
            new FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent
            {
                Subject = new(ActorType.Realtime, Actor,
                    FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent.Verb, EntityId.Format()),
                EntityId = EntityId,
                Id = Id,
                EventId = EventId,
                CommandId = CommandId,
                AggregateId = AggregateId,
                EventSource = EventSource,
                ReceivedOn = ReceivedOn,
                Snapshot = Snapshot
            };
        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>Creates the conventional non-replayable projection failure event.</summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception exception)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        IErrorEvent<FuturesTradeSessionBarEntityId> failed =
            new FuturesRegimeIndicatorsGeneratedFailRealtimeEvent
            {
                Subject = new(ActorType.Realtime, Actor,
                    FuturesRegimeIndicatorsGeneratedFailRealtimeEvent.Verb, EntityId.Format()),
                EntityId = EntityId,
                Id = Id,
                ErrorDate = DateTime.UtcNow,
                EventId = EventId,
                CommandId = CommandId == Guid.Empty ? Guid.NewGuid() : CommandId,
                EventSource = EventSource,
                ErrorMessage = exception.Message,
                ErrorType = ErrorType.EventService,
                ErrorCode = ErrorId,
                ErrorData = exception.ToString(),
                ReceivedOn = ReceivedOn,
                AggregateId = AggregateId
            };
        return (IErrorEvent<TEntityId>)failed;
    }
}

/// <summary>Reports successful storage of one regime-indicator snapshot.</summary>
[MessagePackObject]
public sealed record FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent
    : ICompleteEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the completion verb.</summary>
    public const string Verb = "GeneratedComplete";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(2)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the successfully persisted snapshot.</summary>
    [Key(8)] public FuturesRegimeIndicatorSnapshot Snapshot { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Reports terminal storage failure for one non-replayable indicator snapshot.</summary>
[MessagePackObject]
public sealed record FuturesRegimeIndicatorsGeneratedFailRealtimeEvent
    : IErrorEvent<FuturesTradeSessionBarEntityId>
{
    /// <summary>Gets the failure verb.</summary>
    public const string Verb = "GeneratedFail";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(2)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(3)] public DateTime ErrorDate { get; init; }
    /// <inheritdoc />
    [Key(4)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(5)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(8)] public int ErrorCode { get; init; }
    /// <inheritdoc />
    [Key(9)] public ErrorType ErrorType { get; init; }
    /// <inheritdoc />
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(11)] public DateTime ReceivedOn { get; init; }
    /// <inheritdoc />
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesRegimeIndicatorsGeneratedFailRealtimeEvent);
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
