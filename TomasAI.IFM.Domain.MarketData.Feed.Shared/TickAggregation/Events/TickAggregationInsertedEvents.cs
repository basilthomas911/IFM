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

internal static class TickAggregationEventFactory
{
    public static TickAggregationCompleteEvent Complete(IEvent<TickDataEntityId> source, ushort count, string verb)
    {
        var tick = source switch
        {
            FuturesTickTradeDataInsertedEvent trade => (trade.SchemaVersion, trade.TickDataId, trade.AssetTypeId),
            FuturesTickQuoteDataInsertedEvent quote => (quote.SchemaVersion, quote.TickDataId, quote.AssetTypeId),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        TickAggregationCompleteEvent result = verb == FuturesTickTradeDataInsertedCompleteEvent.Verb
            ? new FuturesTickTradeDataInsertedCompleteEvent()
            : new FuturesTickQuoteDataInsertedCompleteEvent();
        return result with
        {
            Subject = new ActorSubject(ActorType.Realtime, TickAggregationCompleteEvent.Actor, verb, source.EntityId.Format()),
            EntityId = source.EntityId, Id = source.Id, EventId = source.EventId,
            CommandId = source.CommandId, AggregateId = source.AggregateId,
            EventSource = source.EventSource, ReceivedOn = source.ReceivedOn,
            SchemaVersion = tick.SchemaVersion, TickDataId = tick.TickDataId,
            AssetTypeId = tick.AssetTypeId, PersistedRecordCount = count
        };
    }

    public static TickAggregationFailEvent Fail(IEvent<TickDataEntityId> source, Exception ex, ushort count, string verb, int errorCode)
    {
        var tick = source switch
        {
            FuturesTickTradeDataInsertedEvent trade => (trade.SchemaVersion, trade.TickDataId, trade.AssetTypeId),
            FuturesTickQuoteDataInsertedEvent quote => (quote.SchemaVersion, quote.TickDataId, quote.AssetTypeId),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        TickAggregationFailEvent result = verb == FuturesTickTradeDataInsertedFailEvent.Verb
            ? new FuturesTickTradeDataInsertedFailEvent()
            : new FuturesTickQuoteDataInsertedFailEvent();
        return result with
        {
            Subject = new ActorSubject(ActorType.Realtime, TickAggregationFailEvent.Actor, verb, source.EntityId.Format()),
            EntityId = source.EntityId, Id = source.Id, ErrorDate = DateTime.UtcNow,
            EventId = source.EventId, CommandId = source.CommandId, EventSource = source.EventSource,
            ErrorMessage = ex.Message, ErrorCode = errorCode, ErrorType = ErrorType.EventService,
            ErrorData = ex.GetType().Name, ReceivedOn = source.ReceivedOn, AggregateId = source.AggregateId,
            SchemaVersion = tick.SchemaVersion, TickDataId = tick.TickDataId,
            AssetTypeId = tick.AssetTypeId, AttemptedRecordCount = count
        };
    }
}
