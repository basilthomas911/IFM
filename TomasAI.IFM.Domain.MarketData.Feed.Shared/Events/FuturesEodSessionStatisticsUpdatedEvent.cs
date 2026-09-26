using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Realtime projection source containing a coherent futures EOD row whose session
/// fields and open-dependent metrics must be updated without appending intraday history.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesEodSessionStatisticsUpdatedEvent : IEvent<FuturesEodDataId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesEodSessionStatisticsUpdatedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="futuresEodData">The FuturesEodData field.</param>
    /// <param name="createdOn">The CreatedOn field.</param>
    /// <param name="createdBy">The CreatedBy field.</param>
    [SerializationConstructor]
    public FuturesEodSessionStatisticsUpdatedEvent(ActorSubject subject, Guid id, FuturesEodDataId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, FuturesEodDataV2ReadModel futuresEodData, DateTime createdOn, string createdBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        FuturesEodData = futuresEodData;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }
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
