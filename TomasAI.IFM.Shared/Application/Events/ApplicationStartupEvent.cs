using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Application.Events;

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

    public ApplicationStartupEvent() { }

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

    public ApplicationStartupCompleteEvent() { }

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

    public ApplicationStartupFailEvent() { }

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
