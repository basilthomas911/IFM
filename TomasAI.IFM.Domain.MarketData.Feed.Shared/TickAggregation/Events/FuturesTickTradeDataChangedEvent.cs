using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesTickTradeDataChangedEvent : IEvent<TickDataEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesTickTradeDataChangedEvent() { }

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
    /// <param name="tickDataId">The TickDataId field.</param>
    /// <param name="assetTypeId">The AssetTypeId field.</param>
    /// <param name="dataset">The Dataset field.</param>
    /// <param name="definitionDate">The DefinitionDate field.</param>
    /// <param name="publisherId">The PublisherId field.</param>
    /// <param name="instrumentId">The InstrumentId field.</param>
    /// <param name="tradeData">The TradeData field.</param>
    [SerializationConstructor]
    public FuturesTickTradeDataChangedEvent(ActorSubject subject, Guid id, TickDataEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, ushort schemaVersion, TickDataId tickDataId, AssetTypeId assetTypeId, string dataset, DateOnly definitionDate, ushort publisherId, uint instrumentId, FuturesTickTradeData tradeData)
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
        TickDataId = tickDataId;
        AssetTypeId = assetTypeId;
        Dataset = dataset;
        DefinitionDate = definitionDate;
        PublisherId = publisherId;
        InstrumentId = instrumentId;
        TradeData = tradeData;
    }
    public const string Actor = "TickAggregationRealtime";
    public const string Verb = "FuturesTickTradeDataChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TickDataEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ushort SchemaVersion { get; init; } = 1;
    [Key(9)] public TickDataId TickDataId { get; init; }
    [Key(10)] public AssetTypeId AssetTypeId { get; init; }
    [Key(11)] public string Dataset { get; init; } = string.Empty;
    [Key(12)] public DateOnly DefinitionDate { get; init; }
    [Key(13)] public ushort PublisherId { get; init; }
    [Key(14)] public uint InstrumentId { get; init; }
    [Key(15)] public FuturesTickTradeData TradeData { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesTickTradeDataChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
