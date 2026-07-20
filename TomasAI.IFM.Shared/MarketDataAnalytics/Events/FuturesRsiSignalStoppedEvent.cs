using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

/// <summary>
/// Represents a domain event indicating that a futures RSI signal workflow has been stopped.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalStoppedEvent : IEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "Stopped";
    [IgnoreMember] public const int ErrorCode = 19004;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesRsiSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public DateOnly ValueDate { get; init; }
    [Key(9)] public DateTime StoppedOn { get; init; }
    [Key(10)] public string StoppedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesRsiSignalStoppedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesRsiSignalStoppedEvent(
        ActorSubject subject,
        Guid id,
        FuturesRsiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateOnly valueDate,
        DateTime stoppedOn,
        string stoppedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ValueDate = valueDate;
        StoppedOn = stoppedOn;
        StoppedBy = stoppedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesRsiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesRsiSignalEntityId).FullName}.");

        ICompleteEvent<FuturesRsiSignalEntityId> completed = new FuturesRsiSignalStoppedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStoppedCompleteEvent.Actor, FuturesRsiSignalStoppedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            StoppedOn = this.StoppedOn,
            StoppedBy = this.StoppedBy
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
        if (typeof(TEntityId) != typeof(FuturesRsiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesRsiSignalEntityId).FullName}.");

        IErrorEvent<FuturesRsiSignalEntityId> failed = new FuturesRsiSignalStoppedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStoppedFailEvent.Actor, FuturesRsiSignalStoppedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a futures RSI signal has been stopped successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalStoppedCompleteEvent : ICompleteEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "StoppedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesRsiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateOnly ValueDate { get; init; }
    [Key(9)] public DateTime StoppedOn { get; init; }
    [Key(10)] public string StoppedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesRsiSignalStoppedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesRsiSignalStoppedCompleteEvent(
        ActorSubject subject,
        FuturesRsiSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateOnly valueDate,
        DateTime stoppedOn,
        string stoppedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ValueDate = valueDate;
        StoppedOn = stoppedOn;
        StoppedBy = stoppedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when stopping a futures RSI signal fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalStoppedFailEvent : IErrorEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "StoppedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesRsiSignalEntityId EntityId { get; init; }
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
    [Key(15)] public string RouteTo { get; init; }

    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public FuturesRsiSignalStoppedFailEvent() { }

    [SerializationConstructor]
    public FuturesRsiSignalStoppedFailEvent(
        ActorSubject subject,
        FuturesRsiSignalEntityId entityId,
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
