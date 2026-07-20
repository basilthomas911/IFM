using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Telemetry.ViewModels;

namespace TomasAI.IFM.Shared.Telemetry.Events;

[MessagePackObject(AllowPrivate = true)]
public record LogEventsAddedEvent : IEvent<LogEventsId>
{
    [IgnoreMember] public const string Actor = "LogEvents";
    [IgnoreMember] public const string Verb = "Added";
    [IgnoreMember] public const int ErrorCode = 11001;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public LogEventsId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public LogEventsId LogEventsId { get; init; }
    [Key(9)] public LogEventReadModel[] LogEvents { get; init; }
    [Key(10)] public DateTime AddedOn { get; init; }
    [Key(11)] public string AddedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(LogEventsAddedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public LogEventsAddedEvent() { }

    [SerializationConstructor]
    public LogEventsAddedEvent(
        ActorSubject subject,
        Guid id,
        LogEventsId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LogEventsId logEventsId,
        LogEventReadModel[] logEvents,
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
        LogEventsId = logEventsId;
        LogEvents = logEvents;
        AddedOn = addedOn;
        AddedBy = addedBy ?? string.Empty;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(LogEventsId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(LogEventsId).FullName}.");

        ICompleteEvent<LogEventsId> completed = new LogEventsAddedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, LogEventsAddedCompleteEvent.Actor, LogEventsAddedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            LogEventsId = this.LogEventsId,
            LogEvents = this.LogEvents,
            AddedOn = this.AddedOn,
            AddedBy = this.AddedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(LogEventsId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(LogEventsId).FullName}.");

        IErrorEvent<LogEventsId> failed = new LogEventsAddedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, LogEventsAddedFailEvent.Actor, LogEventsAddedFailEvent.Verb, this.Subject.EntityId),
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
public record LogEventsAddedCompleteEvent : ICompleteEvent<LogEventsId>
{
    [IgnoreMember] public const string Actor = "LogEvents";
    [IgnoreMember] public const string Verb = "AddedComplete";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LogEventsId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public LogEventsId LogEventsId { get; init; }
    [Key(9)] public LogEventReadModel[] LogEvents { get; init; }
    [Key(10)] public DateTime AddedOn { get; init; }
    [Key(11)] public string AddedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(LogEventsAddedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public LogEventsAddedCompleteEvent() { }

    [SerializationConstructor]
    public LogEventsAddedCompleteEvent(
        ActorSubject subject,
        LogEventsId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        LogEventsId logEventsId,
        LogEventReadModel[] logEvents,
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
        LogEventsId = logEventsId;
        LogEvents = logEvents;
        AddedOn = addedOn;
        AddedBy = addedBy ?? string.Empty;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record LogEventsAddedFailEvent : IErrorEvent<LogEventsId>
{
    [IgnoreMember] public const string Actor = "LogEvents";
    [IgnoreMember] public const string Verb = "AddedFail";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LogEventsId EntityId { get; init; }
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

    [IgnoreMember] public string EventName => nameof(LogEventsAddedFailEvent);
    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public LogEventsAddedFailEvent() { }

    [SerializationConstructor]
    public LogEventsAddedFailEvent(
        ActorSubject subject,
        LogEventsId entityId,
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
        EventSource = eventSource ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData ?? string.Empty;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId ?? string.Empty;
        CommandName = commandName ?? string.Empty;
        CommandData = commandData ?? string.Empty;
    }
}
