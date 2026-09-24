using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.Events;

/// <summary>
/// Event published when an application startup is initiated.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record ApplicationStartupEvent : IEvent<ApplicationEntityId>
{
    [IgnoreMember] public const string Actor = "ApplicationEvent";
    [IgnoreMember] public const string Verb = "Startup";
    [IgnoreMember] public const int ErrorCode = 10001;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }  
    [Key(2)] public ApplicationEntityId EntityId{ get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime CreatedOn { get; init; }
    [Key(9)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates an empty startup event for serialization.</summary>
    public ApplicationStartupEvent() { }

    /// <summary>Rehydrates the application lifecycle event from its wire fields.</summary>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="id">The event identifier.</param>
    /// <param name="entityId">The application value-date identity.</param>
    /// <param name="eventId">The durable event sequence identifier.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="eventSource">The event source.</param>
    /// <param name="receivedOn">The receive timestamp.</param>
    /// <param name="createdOn">The creation timestamp.</param>
    /// <param name="createdBy">The initiating principal.</param>
    [SerializationConstructor]
    public ApplicationStartupEvent(
        ActorSubject subject,
        Guid id,
        ApplicationEntityId entityId,
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
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    /// <typeparam name="TComplete">The requested completion contract.</typeparam>
    /// <typeparam name="TEntityId">The requested entity-identifier type.</typeparam>
    /// <returns>The correlated startup completion event.</returns>
    /// <exception cref="InvalidOperationException">The requested entity-identifier type is not the application identity.</exception>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(ApplicationEntityId))
            throw new InvalidOperationException($"ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(ApplicationEntityId).FullName}.");        
        var completed = new ApplicationStartupCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, ApplicationStartupCompleteEvent.Actor, ApplicationStartupCompleteEvent.Verb, Subject.EntityId),
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

        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    /// <typeparam name="TFail">The requested failure contract.</typeparam>
    /// <typeparam name="TEntityId">The requested entity-identifier type.</typeparam>
    /// <param name="ex">The originating failure.</param>
    /// <returns>The correlated startup failure event.</returns>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var failed = new ApplicationStartupFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, ApplicationStartupFailEvent.Actor, ApplicationStartupFailEvent.Verb, Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.Now,
            EventId = this.EventId,
            CommandId = this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex?.Message ?? string.Empty,
            ErrorType = ErrorType.System,
            ErrorCode = ErrorCode,
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId
        };

        return (IErrorEvent<TEntityId>)(object)failed;
    }
}

/// <summary>Reports successful completion of application startup.</summary>
[MessagePackObject(AllowPrivate = true)]
public record ApplicationStartupCompleteEvent : ICompleteEvent<ApplicationEntityId>
{
    [IgnoreMember] public const string Actor = "ApplicationEvent";
    [IgnoreMember] public const string Verb = "StartupComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ApplicationEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime CreatedOn { get; init; }
    [Key(9)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>Creates an empty startup complete event for serialization.</summary>
    public ApplicationStartupCompleteEvent() { }

    /// <summary>Rehydrates the application completion notification from its wire fields.</summary>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="entityId">The application value-date identity.</param>
    /// <param name="id">The event identifier.</param>
    /// <param name="eventId">The durable event sequence identifier.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="eventSource">The event source.</param>
    /// <param name="receivedOn">The receive timestamp.</param>
    /// <param name="createdOn">The creation timestamp.</param>
    /// <param name="createdBy">The initiating principal.</param>
    [SerializationConstructor]
    public ApplicationStartupCompleteEvent(
        ActorSubject subject,
        ApplicationEntityId entityId,
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
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}

/// <summary>Terminal notification indicating startup completed with optional degradation.</summary>
[MessagePackObject(AllowPrivate = true)]
public record ApplicationStartupDegradedEvent : ICompleteEvent<ApplicationEntityId>
{
    [IgnoreMember] public const string Actor = "ApplicationEvent";
    [IgnoreMember] public const string Verb = "StartupDegraded";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ApplicationEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DateTime CreatedOn { get; init; }
    [Key(9)] public string CreatedBy { get; init; } = string.Empty;
    [Key(10)] public string Reason { get; init; } = string.Empty;

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

/// <summary>Reports a failed application startup attempt.</summary>
[MessagePackObject(AllowPrivate = true)]
public record ApplicationStartupFailEvent : IErrorEvent<ApplicationEntityId>
{
    [IgnoreMember] public const string Actor = "ApplicationEvent";
    [IgnoreMember] public const string Verb = "StartupFail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ApplicationEntityId EntityId { get; init; }
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

    /// <summary>Creates an empty startup fail event for serialization.</summary>
    public ApplicationStartupFailEvent() { }

    /// <summary>Rehydrates the application failure notification from its wire fields.</summary>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="entityId">The application value-date identity.</param>
    /// <param name="id">The event identifier.</param>
    /// <param name="errorDate">The failure timestamp.</param>
    /// <param name="eventId">The durable event sequence identifier.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="eventSource">The event source.</param>
    /// <param name="errorMessage">The failure description.</param>
    /// <param name="errorCode">The stable failure code.</param>
    /// <param name="errorType">The failure classification.</param>
    /// <param name="errorData">The failure details.</param>
    /// <param name="receivedOn">The receive timestamp.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="commandName">The originating command name.</param>
    /// <param name="commandData">The originating command data.</param>
    /// <param name="routeTo">The reply route.</param>
    [SerializationConstructor]
    public ApplicationStartupFailEvent(
        ActorSubject subject,
        ApplicationEntityId entityId,
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
