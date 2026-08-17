using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

[MessagePackObject]
public sealed record FuturesTickTradeDataChangedEvent : IEvent<TickDataEntityId>
{
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

[MessagePackObject]
public sealed record FuturesTickQuoteDataChangedEvent : IEvent<TickDataEntityId>
{
    public const string Actor = "TickAggregationRealtime";
    public const string Verb = "FuturesTickQuoteDataChanged";
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
    [Key(15)] public QuoteEmissionReason EmissionReason { get; init; }
    [Key(16)] public ushort QuoteCount { get; init; }
    [Key(17)] public FuturesTickQuoteDataSegment QuoteData { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesTickQuoteDataChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
