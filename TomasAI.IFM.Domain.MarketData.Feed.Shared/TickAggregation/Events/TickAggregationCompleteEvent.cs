using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

[MessagePackObject]
[Union(0, typeof(FuturesTickTradeDataInsertedCompleteEvent))]
[Union(1, typeof(FuturesTickQuoteDataInsertedCompleteEvent))]
public abstract record TickAggregationCompleteEvent : ICompleteEvent<TickDataEntityId>
{
    public const string Actor = "TickAggregationRealtime";
    public const string Verb = "Complete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public TickDataEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ushort SchemaVersion { get; init; }
    [Key(9)] public TickDataId TickDataId { get; init; }
    [Key(10)] public AssetTypeId AssetTypeId { get; init; }
    [Key(11)] public ushort PersistedRecordCount { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
