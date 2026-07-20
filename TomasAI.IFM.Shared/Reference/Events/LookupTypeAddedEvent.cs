using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Events;

/// <summary>
/// Represents a domain event indicating that a lookup type has been added. Used to communicate the addition of a
/// lookup type within the event-driven system and to support denormalization workflows.
/// </summary>
/// <remarks>This event is typically published when a new lookup type is successfully created. It carries metadata
/// and the details of the added lookup type for downstream processing, auditing, or projection updates. The event
/// supports conversion to completion or failure events to indicate the outcome of related command handling. Thread
/// safety is not guaranteed; instances are generally used as data transfer objects within event processing
/// pipelines.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeAddedEvent : IEvent<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeEvent";
    [IgnoreMember] public const string Verb = "Added";
    [IgnoreMember] public const int ErrorCode = 7003;

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
    [Key(8)] public LookupTypeReadModel LookupType { get; init; }
    [Key(9)] public DateTime AddedOn { get; init; }
    [Key(10)] public string AddedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public LookupTypeAddedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public LookupTypeAddedEvent(
        ActorSubject subject,
        Guid id,
        LookupTypeId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LookupTypeReadModel lookupType,
        DateTime addedOn,
        string addedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        LookupType = lookupType ?? throw new ArgumentNullException(nameof(lookupType));
        AddedOn = addedOn;
        AddedBy = addedBy ?? string.Empty;
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

        ICompleteEvent<LookupTypeId> completed = new LookupTypeAddedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, LookupTypeAddedCompleteEvent.Actor, LookupTypeAddedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            LookupType = this.LookupType,
            AddedOn = this.AddedOn,
            AddedBy = this.AddedBy
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

        IErrorEvent<LookupTypeId> failed = new LookupTypeAddedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, LookupTypeAddedFailEvent.Actor, LookupTypeAddedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a lookup type has been added successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeAddedCompleteEvent : ICompleteEvent<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeEvent";
    [IgnoreMember] public const string Verb = "AddedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LookupTypeId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public LookupTypeReadModel LookupType { get; init; }
    [Key(9)] public DateTime AddedOn { get; init; }
    [Key(10)] public string AddedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public LookupTypeAddedCompleteEvent() { }

    [SerializationConstructor]
    public LookupTypeAddedCompleteEvent(
        ActorSubject subject,
        LookupTypeId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LookupTypeReadModel lookupType,
        DateTime addedOn,
        string addedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        LookupType = lookupType ?? throw new ArgumentNullException(nameof(lookupType));
        AddedOn = addedOn;
        AddedBy = addedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when adding a lookup type fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeAddedFailEvent : IErrorEvent<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeEvent";
    [IgnoreMember] public const string Verb = "AddedFail";

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

    public LookupTypeAddedFailEvent() { }

    [SerializationConstructor]
    public LookupTypeAddedFailEvent(
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
