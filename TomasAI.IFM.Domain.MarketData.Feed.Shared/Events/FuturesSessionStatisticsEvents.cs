using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Non-durable provider-neutral input carrying the latest complete session statistics.
/// </summary>
[MessagePackObject]
public sealed record FuturesSessionStatisticsUpdatedRealtimeEvent : IEvent<FuturesEodDataId>
{
    public const string Actor = FuturesTickTradeDataInsertedEvent.Actor;
    public const string Verb = "SessionStatisticsObserved";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesEodDataId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesSessionStatisticsSnapshot Statistics { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesSessionStatisticsUpdatedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>
/// Realtime projection source containing a coherent futures EOD row whose session
/// fields and open-dependent metrics must be updated without appending intraday history.
/// </summary>
[MessagePackObject]
public sealed record FuturesEodSessionStatisticsUpdatedEvent : IEvent<FuturesEodDataId>
{
    public const string Actor = FuturesEodDataInsertedEvent.Actor;
    public const string Verb = "SessionStatisticsUpdated";
    public const int ErrorCode = 5011;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesEodDataId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; } = new();
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; } = string.Empty;

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesEodSessionStatisticsUpdatedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesEodDataId)
            || typeof(TComplete) != typeof(FuturesEodDataInsertedCompleteEvent))
            throw new InvalidOperationException("The requested completion event does not match the session-statistics event family.");

        object completed = new FuturesEodDataInsertedCompleteEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesEodDataInsertedCompleteEvent.Actor,
                FuturesEodDataInsertedCompleteEvent.Verb,
                EntityId.Format()),
            EntityId = EntityId,
            Id = Id,
            EventId = EventId,
            CommandId = CommandId,
            AggregateId = AggregateId,
            EventSource = EventSource,
            ReceivedOn = ReceivedOn,
            FuturesEodData = FuturesEodData,
            CreatedOn = CreatedOn,
            CreatedBy = CreatedBy
        };
        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception exception)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesEodDataId)
            || typeof(TFail) != typeof(FuturesEodDataInsertedFailEvent))
            throw new InvalidOperationException("The requested failure event does not match the session-statistics event family.");

        object failed = new FuturesEodDataInsertedFailEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesEodDataInsertedFailEvent.Actor,
                FuturesEodDataInsertedFailEvent.Verb,
                EntityId.Format()),
            EntityId = EntityId,
            Id = Id,
            ErrorDate = DateTime.UtcNow,
            EventId = EventId,
            CommandId = CommandId,
            EventSource = EventSource,
            ErrorMessage = exception.Message,
            ErrorCode = ErrorCode,
            ErrorType = ErrorType.EventService,
            ErrorData = exception.GetType().Name,
            ReceivedOn = ReceivedOn,
            AggregateId = AggregateId,
            CommandName = EventName
        };
        return (IErrorEvent<TEntityId>)failed;
    }
}
