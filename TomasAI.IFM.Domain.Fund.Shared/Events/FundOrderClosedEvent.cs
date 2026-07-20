using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Represents an event that indicates a fund order has been closed within the system.
/// </summary>
/// <remarks>This event is typically used in event sourcing or message-driven architectures to signal that a fund
/// order has reached its closed state. It provides information about the closure, including the time and the user
/// responsible. Consumers of this event can use it to update projections, trigger workflows, or audit fund order
/// lifecycle changes.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderClosedEvent : IEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "FundOrderClosed";
    [IgnoreMember] public const string ClosedComplete = "FundOrderClosedComplete";
    [IgnoreMember] public const string Fail = "FundOrderClosedFail";
    [IgnoreMember] public const int ErrorCode = 3017;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FundId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FundOrderId FundOrderId { get; init; }
    [Key(9)] public DateTime ClosedOn { get; init; }
    [Key(10)] public string ClosedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FundOrderClosedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FundOrderClosedEvent(
        ActorSubject subject,
        Guid id,
        FundId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderId fundOrderId,
        DateTime closedOn,
        string closedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundOrderId = fundOrderId;
        ClosedOn = closedOn;
        ClosedBy = closedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundId).FullName}.");

        ICompleteEvent<FundId> completed = new FundOrderClosedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundOrderClosedCompleteEvent.Actor, FundOrderClosedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,

            ClosedOn = this.ClosedOn,
            ClosedBy = this.ClosedBy
        } ;

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundId).FullName}.");

        IErrorEvent<FundId> failed = new FundOrderClosedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundOrderClosedFailEvent.Actor, FundOrderClosedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a fund order close operation has completed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderClosedCompleteEvent : ICompleteEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "FundOrderClosedComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FundOrderId FundOrderId { get; init; }
    [Key(9)] public DateTime ClosedOn { get; init; }
    [Key(10)] public string ClosedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FundOrderClosedCompleteEvent() { }

    [SerializationConstructor]
    public FundOrderClosedCompleteEvent(
        ActorSubject subject,
        FundId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderId fundOrderId,
        DateTime closedOn,
        string closedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundOrderId = fundOrderId;
        ClosedOn = closedOn;
        ClosedBy = closedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when a fund order close operation has failed.
/// Inherits standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderClosedFailEvent : IErrorEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "FundOrderClosedFail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundId EntityId { get; init; }
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

    public FundOrderClosedFailEvent() { }

    [SerializationConstructor]
    public FundOrderClosedFailEvent(
        ActorSubject subject,
        FundId entityId,
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