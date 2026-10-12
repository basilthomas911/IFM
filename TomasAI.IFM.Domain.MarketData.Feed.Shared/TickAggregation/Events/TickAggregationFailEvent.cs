using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

[MessagePackObject]
[Union(0, typeof(FuturesTickTradeDataInsertedFailEvent))]
[Union(1, typeof(FuturesTickQuoteDataInsertedFailEvent))]
public abstract record TickAggregationFailEvent : IErrorEvent<TickDataEntityId>
{
    public const string Actor = "TickAggregationRealtime";
    public const string Verb = "Fail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public TickDataEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public ErrorType ErrorType { get; init; }
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    [Key(16)] public ushort SchemaVersion { get; init; }
    [Key(17)] public TickDataId TickDataId { get; init; }
    [Key(18)] public AssetTypeId AssetTypeId { get; init; }
    [Key(19)] public ushort AttemptedRecordCount { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
