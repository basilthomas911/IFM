using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Carries one non-durable update to the primary futures market-price realtime actor and its registered routes.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesMarketPriceUpdatedRealtimeEvent : IEvent<TickDataEntityId>, IRealtimeSourceGeneration
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesMarketPriceUpdatedRealtimeEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="price">The Price field.</param>
    /// <param name="updateSource">The UpdateSource field.</param>
    /// <param name="vwapCheckpoint">The optional cumulative VWAP checkpoint.</param>
    /// <param name="sourceTrade">The original qualifying trade payload, or null for quote, checkpoint, or legacy events.</param>
    [SerializationConstructor]
    public FuturesMarketPriceUpdatedRealtimeEvent(ActorSubject subject, Guid id, TickDataEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, ushort schemaVersion, FuturesMarketPriceSnapshot price, FuturesMarketPriceUpdateSource updateSource, FuturesVwapSourceCheckpoint? vwapCheckpoint, FuturesTickTradeDataChangedEvent? sourceTrade = null)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        SchemaVersion = schemaVersion;
        Price = price;
        UpdateSource = updateSource;
        VwapCheckpoint = vwapCheckpoint;
        SourceTrade = sourceTrade;
    }
    /// <summary>The primary actor mailbox name used by the realtime subject.</summary>
    public const string Actor = "FuturesMarketPrice";

    /// <summary>The realtime event verb.</summary>
    public const string Verb = "Updated";
    [IgnoreMember] public string SourceDataset { get; init; } = string.Empty;
    [IgnoreMember] public Guid SourceGenerationId { get; init; }

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TickDataEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ushort SchemaVersion { get; init; } = 2;
    [Key(9)] public FuturesMarketPriceSnapshot Price { get; init; }
    [Key(10)] public FuturesMarketPriceUpdateSource UpdateSource { get; init; }
    /// <summary>Optional cumulative ES VWAP state on the dedicated VWAP subject.</summary>
    [Key(11)] public FuturesVwapSourceCheckpoint? VwapCheckpoint { get; init; }

    /// <summary>The original qualifying trade to forward to tick storage and EOD; absent for quotes and VWAP checkpoints.</summary>
    [Key(12)] public FuturesTickTradeDataChangedEvent? SourceTrade { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesMarketPriceUpdatedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
