using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>Provides one normalized trade retained only for exact session-state recovery.</summary>
[MessagePackObject]
public readonly record struct FuturesTradeReplayObservation(
    [property: Key(0)] decimal Price,
    [property: Key(1)] uint Size,
    [property: Key(2)] long SourceSequence,
    [property: Key(3)] DateTimeOffset EventTimestampUtc,
    [property: Key(4)] NormalizedTradeAction Action,
    [property: Key(5)] NormalizedTradeConditionFlags Conditions);

/// <summary>
/// Carries one bounded startup trade-replay batch directly to recovery consumers. Replay batches
/// are separate from live market-price updates so they cannot start live signal or workflow paths.
/// </summary>
[MessagePackObject]
public sealed record FuturesTradeReplayBatchRealtimeEvent : IEvent<TickDataEntityId>
{
    /// <summary>The primary market-price mailbox that owns replay routing.</summary>
    public const string Actor = FuturesMarketPriceUpdatedRealtimeEvent.Actor;
    /// <summary>The private replay-batch verb.</summary>
    public const string Verb = "TradeReplayBatch";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TickDataEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ushort SchemaVersion { get; init; } = 1;
    [Key(9)] public Guid RecoveryGenerationId { get; init; }
    [Key(10)] public long BatchOrdinal { get; init; }
    [Key(11)] public bool IsFirstBatch { get; init; }
    [Key(12)] public bool IsFinalBatch { get; init; }
    [Key(13)] public Guid LiveStreamEpochId { get; init; }
    [Key(14)] public FuturesTradeReplayObservation[] Trades { get; init; } = [];

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesTradeReplayBatchRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
