using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalHoldTradeSetEvent : IEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "HoldTradeSet";
    [IgnoreMember] public const int ErrorCode = 19015;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateTime CreatedOn { get; init; }
    [Key(9)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesItiSignalHoldTradeSetEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesItiSignalHoldTradeSetEvent() { }

    [SerializationConstructor]
    public FuturesItiSignalHoldTradeSetEvent(
        ActorSubject subject,
        Guid id,
        FuturesItiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesItiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesItiSignalEntityId).FullName}.");

        ICompleteEvent<FuturesItiSignalEntityId> completed = new FuturesItiSignalHoldTradeSetCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalHoldTradeSetCompleteEvent.Actor, FuturesItiSignalHoldTradeSetCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesItiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesItiSignalEntityId).FullName}.");

        IErrorEvent<FuturesItiSignalEntityId> failed = new FuturesItiSignalHoldTradeSetFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalHoldTradeSetFailEvent.Actor, FuturesItiSignalHoldTradeSetFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.UtcNow,
            EventId = this.EventId,
            CommandId = this.CommandId == Guid.Empty ? Guid.NewGuid() : this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode,
            ErrorData = ex.ToString(),
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId
        };

        return (IErrorEvent<TEntityId>)failed;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalHoldTradeSetCompleteEvent : ICompleteEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "HoldTradeSetComplete";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateTime CreatedOn { get; init; }
    [Key(9)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesItiSignalHoldTradeSetCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesItiSignalHoldTradeSetCompleteEvent() { }

    [SerializationConstructor]
    public FuturesItiSignalHoldTradeSetCompleteEvent(
        ActorSubject subject,
        FuturesItiSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalHoldTradeSetFailEvent : IErrorEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "HoldTradeSetFail";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public string ErrorMessage { get; init; }
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public ErrorType ErrorType { get; init; }
    [Key(10)] public string ErrorData { get; init; }
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; }
    [Key(13)] public string CommandName { get; init; }
    [Key(14)] public string CommandData { get; init; }

    [IgnoreMember] public string EventName => nameof(FuturesItiSignalHoldTradeSetFailEvent);
    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public FuturesItiSignalHoldTradeSetFailEvent() { }

    [SerializationConstructor]
    public FuturesItiSignalHoldTradeSetFailEvent(
        ActorSubject subject,
        FuturesItiSignalEntityId entityId,
        Guid id,
        DateTime errorDate,
        long eventId,
        Guid commandId,
        string eventSource,
        string errorMessage,
        int errorCode,
        ErrorType errorType,
        string errorData,
        DateTime receivedOn,
        string aggregateId,
        string commandName,
        string commandData)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        ErrorDate = errorDate;
        EventId = eventId;
        CommandId = commandId;
        EventSource = eventSource;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId;
        CommandName = commandName;
        CommandData = commandData;
    }
}
