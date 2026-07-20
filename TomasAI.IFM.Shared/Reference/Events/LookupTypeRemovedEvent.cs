using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Events;

/// <summary>
/// Represents a domain event indicating that a lookup type has been removed. Used to communicate the deletion of a
/// lookup type within the event-driven system and to support denormalization workflows.
/// </summary>
/// <remarks>This event is typically published when a lookup type is successfully removed. It carries metadata
/// and the details of the removed lookup type for downstream processing, auditing, or projection updates. The event
/// supports conversion to completion or failure events to indicate the outcome of related command handling. Thread
/// safety is not guaranteed; instances are generally used as data transfer objects within event processing
/// pipelines.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeRemovedEvent : IEvent<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeEvent";
    [IgnoreMember] public const string Verb = "Removed";
    [IgnoreMember] public const int ErrorCode = 7005;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public LookupTypeId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public LookupTypeId LookupTypeId { get; init; }
    [Key(9)] public DateTime RemovedOn { get; init; }
    [Key(10)] public string RemovedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public LookupTypeRemovedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public LookupTypeRemovedEvent(
        ActorSubject subject,
        Guid id,
        LookupTypeId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LookupTypeId lookupTypeId,
        DateTime removedOn,
        string removedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        LookupTypeId = lookupTypeId ?? throw new ArgumentNullException(nameof(lookupTypeId));
        RemovedOn = removedOn;
        RemovedBy = removedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(LookupTypeId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(LookupTypeId).FullName}.");

        ICompleteEvent<LookupTypeId> completed = new LookupTypeRemovedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, LookupTypeRemovedCompleteEvent.Actor, LookupTypeRemovedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            LookupTypeId = this.LookupTypeId,
            RemovedOn = this.RemovedOn,
            RemovedBy = this.RemovedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// Validates the requested entity id type and returns a strongly-typed error event.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(LookupTypeId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(LookupTypeId).FullName}.");

        IErrorEvent<LookupTypeId> failed = new LookupTypeRemovedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, LookupTypeRemovedFailEvent.Actor, LookupTypeRemovedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a lookup type has been removed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeRemovedCompleteEvent : ICompleteEvent<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeEvent";
    [IgnoreMember] public const string Verb = "RemovedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LookupTypeId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public LookupTypeId LookupTypeId { get; init; }
    [Key(9)] public DateTime RemovedOn { get; init; }
    [Key(10)] public string RemovedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public LookupTypeRemovedCompleteEvent() { }

    [SerializationConstructor]
    public LookupTypeRemovedCompleteEvent(
        ActorSubject subject,
        LookupTypeId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LookupTypeId lookupTypeId,
        DateTime removedOn,
        string removedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        LookupTypeId = lookupTypeId ?? throw new ArgumentNullException(nameof(lookupTypeId));
        RemovedOn = removedOn;
        RemovedBy = removedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when removing a lookup type fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeRemovedFailEvent : IErrorEvent<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeEvent";
    [IgnoreMember] public const string Verb = "RemovedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LookupTypeId EntityId { get; init; }
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

    public LookupTypeRemovedFailEvent() { }

    [SerializationConstructor]
    public LookupTypeRemovedFailEvent(
        ActorSubject subject,
        LookupTypeId entityId,
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
