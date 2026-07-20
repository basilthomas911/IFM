using System;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Events;

/// <summary>
/// Represents a domain event indicating that an economic calendar entry has been changed. Used to communicate the modification
/// of an economic calendar entry within the event-driven system and to support denormalization workflows.
/// </summary>
/// <remarks>This event is typically published when an existing economic calendar entry is successfully updated. It carries
/// metadata and the details of the changed economic calendar entry for downstream processing, auditing, or projection
/// updates. The event supports conversion to completion or failure events to indicate the outcome of related command
/// handling. Thread safety is not guaranteed; instances are generally used as data transfer objects within event
/// processing pipelines.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarChangedEvent : IEvent<EconomicCalendarId>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarEvent";
    [IgnoreMember] public const string Verb = "Changed";
    [IgnoreMember] public const int ErrorCode = 7002;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public EconomicCalendarId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public EconomicCalendarReadModel EconomicCalendar { get; init; }
    [Key(9)] public DateTime UpdatedOn { get; init; }
    [Key(10)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public EconomicCalendarChangedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public EconomicCalendarChangedEvent(
        ActorSubject subject,
        Guid id,
        EconomicCalendarId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        EconomicCalendarReadModel economicCalendar,
        DateTime updatedOn,
        string updatedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        EconomicCalendar = economicCalendar ?? throw new ArgumentNullException(nameof(economicCalendar));
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(EconomicCalendarId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(EconomicCalendarId).FullName}.");

        ICompleteEvent<EconomicCalendarId> completed = new EconomicCalendarChangedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarChangedCompleteEvent.Actor, EconomicCalendarChangedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            EconomicCalendar = this.EconomicCalendar,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
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
        if (typeof(TEntityId) != typeof(EconomicCalendarId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(EconomicCalendarId).FullName}.");

        IErrorEvent<EconomicCalendarId> failed = new EconomicCalendarChangedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, EconomicCalendarChangedFailEvent.Actor, EconomicCalendarChangedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when an economic calendar entry has been changed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarChangedCompleteEvent : ICompleteEvent<EconomicCalendarId>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarEvent";
    [IgnoreMember] public const string Verb = "ChangedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public EconomicCalendarId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public EconomicCalendarReadModel EconomicCalendar { get; init; }
    [Key(9)] public DateTime UpdatedOn { get; init; }
    [Key(10)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public EconomicCalendarChangedCompleteEvent() { }

    [SerializationConstructor]
    public EconomicCalendarChangedCompleteEvent(
        ActorSubject subject,
        EconomicCalendarId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        EconomicCalendarReadModel economicCalendar,
        DateTime updatedOn,
        string updatedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        EconomicCalendar = economicCalendar ?? throw new ArgumentNullException(nameof(economicCalendar));
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when changing an economic calendar entry fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarChangedFailEvent : IErrorEvent<EconomicCalendarId>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarEvent";
    [IgnoreMember] public const string Verb = "ChangedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public EconomicCalendarId EntityId { get; init; }
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

    public EconomicCalendarChangedFailEvent() { }

    [SerializationConstructor]
    public EconomicCalendarChangedFailEvent(
        ActorSubject subject,
        EconomicCalendarId entityId,
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
