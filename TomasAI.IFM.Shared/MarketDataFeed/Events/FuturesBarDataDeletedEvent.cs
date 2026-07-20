using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Represents a domain event indicating that futures bar data has been deleted.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesBarDataDeletedEvent : IEvent<FuturesBarDataId>
{
    [IgnoreMember] public const string Actor = "FuturesBarDataEvent";
    [IgnoreMember] public const string Verb = "Deleted";
    [IgnoreMember] public const int ErrorCode = 5026;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; } = Guid.NewGuid();
    [Key(2)] public FuturesBarDataId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FuturesBarDataId BarDataId { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; } = string.Empty;

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesBarDataDeletedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesBarDataDeletedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesBarDataDeletedEvent(
        ActorSubject subject,
        Guid id,
        FuturesBarDataId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesBarDataId barDataId,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        BarDataId = barDataId;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesBarDataId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesBarDataId).FullName}.");

        ICompleteEvent<FuturesBarDataId> completed = new FuturesBarDataDeletedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesBarDataDeletedCompleteEvent.Actor, FuturesBarDataDeletedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            BarDataId = this.BarDataId,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesBarDataId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesBarDataId).FullName}.");

        IErrorEvent<FuturesBarDataId> failed = new FuturesBarDataDeletedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesBarDataDeletedFailEvent.Actor, FuturesBarDataDeletedFailEvent.Verb, this.Subject.EntityId),
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

/// <summary>
/// Event published when futures bar data has been deleted successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesBarDataDeletedCompleteEvent : ICompleteEvent<FuturesBarDataId>
{
    [IgnoreMember] public const string Actor = "FuturesBarDataEvent";
    [IgnoreMember] public const string Verb = "DeletedComplete";
    [IgnoreMember] static readonly string CachedUserName = string.Concat(Environment.UserDomainName, "\\", Environment.UserName);

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesBarDataId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; } = Guid.NewGuid();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesBarDataId BarDataId { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; } = string.Empty;

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesBarDataDeletedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesBarDataDeletedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesBarDataDeletedCompleteEvent(
        ActorSubject subject,
        FuturesBarDataId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesBarDataId barDataId,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        BarDataId = barDataId;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when deleting futures bar data fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesBarDataDeletedFailEvent : IErrorEvent<FuturesBarDataId>
{
    [IgnoreMember] public const string Actor = "FuturesBarDataEvent";
    [IgnoreMember] public const string Verb = "DeletedFail";
    [IgnoreMember] static readonly string CachedUserName = string.Concat(Environment.UserDomainName, "\\", Environment.UserName);

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesBarDataId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; } = Guid.NewGuid();
    [Key(3)] public DateTime ErrorDate { get; init; } = DateTime.UtcNow;
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

    [IgnoreMember] public string EventName => nameof(FuturesBarDataDeletedFailEvent);
    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public FuturesBarDataDeletedFailEvent() { }

    [SerializationConstructor]
    public FuturesBarDataDeletedFailEvent(
        ActorSubject subject,
        FuturesBarDataId entityId,
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
        string commandData,
        string routeTo)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        ErrorDate = errorDate;
        EventId = eventId;
        CommandId = commandId;
        EventSource = eventSource ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData ?? string.Empty;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId ?? string.Empty;
        CommandName = commandName ?? string.Empty;
        CommandData = commandData ?? string.Empty;
        RouteTo = routeTo ?? string.Empty;
    }
}
