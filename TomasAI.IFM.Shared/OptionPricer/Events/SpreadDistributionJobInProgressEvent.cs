using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer.Events;

[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionJobInProgressEvent : IEvent<SpreadDistributionJobEntityId>
{
    [IgnoreMember] public const string Actor = "SpreadDistributionJobEvent";
    [IgnoreMember] public const string Verb = "InProgress";
    [IgnoreMember] public const int ErrorCode = 6002;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public SpreadDistributionJobEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public SpreadDistributionJobReadModel SpreadDistributionJob { get; init; }
    [Key(9)] public string CreatedBy { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public SpreadDistributionJobInProgressEvent() { }

    [SerializationConstructor]
    public SpreadDistributionJobInProgressEvent(
        ActorSubject subject,
        Guid id,
        SpreadDistributionJobEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        SpreadDistributionJobReadModel spreadDistributionJob,
        string createdBy,
        DateTime createdOn)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        SpreadDistributionJob = spreadDistributionJob;
        CreatedBy = createdBy ?? string.Empty;
        CreatedOn = createdOn;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(SpreadDistributionJobEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(SpreadDistributionJobEntityId).FullName}.");

        ICompleteEvent<SpreadDistributionJobEntityId> completed = new SpreadDistributionJobInProgressCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobInProgressCompleteEvent.Actor, SpreadDistributionJobInProgressCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            SpreadDistributionJob = this.SpreadDistributionJob,
            CreatedBy = this.CreatedBy,
            CreatedOn = this.CreatedOn
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(SpreadDistributionJobEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(SpreadDistributionJobEntityId).FullName}.");

        IErrorEvent<SpreadDistributionJobEntityId> failed = new SpreadDistributionJobInProgressFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobInProgressFailEvent.Actor, SpreadDistributionJobInProgressFailEvent.Verb, this.Subject.EntityId),
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

    public ICompleteEvent ToCompletedEvent()
        => (ICompleteEvent)ToCompleteEvent<SpreadDistributionJobInProgressCompleteEvent, SpreadDistributionJobEntityId>();

    public IErrorEvent ToFailedEvent(Exception ex)
        => (IErrorEvent)ToFailEvent<SpreadDistributionJobInProgressFailEvent, SpreadDistributionJobEntityId>(ex);
}

[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionJobInProgressCompleteEvent : ICompleteEvent<SpreadDistributionJobEntityId>
{
    [IgnoreMember] public const string Actor = "SpreadDistributionJobEvent";
    [IgnoreMember] public const string Verb = "InProgressComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public SpreadDistributionJobEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public SpreadDistributionJobReadModel SpreadDistributionJob { get; init; }
    [Key(9)] public string CreatedBy { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public SpreadDistributionJobInProgressCompleteEvent() { }

    [SerializationConstructor]
    public SpreadDistributionJobInProgressCompleteEvent(
        ActorSubject subject,
        SpreadDistributionJobEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        SpreadDistributionJobReadModel spreadDistributionJob,
        string createdBy,
        DateTime createdOn)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        SpreadDistributionJob = spreadDistributionJob;
        CreatedBy = createdBy ?? string.Empty;
        CreatedOn = createdOn;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionJobInProgressFailEvent : IErrorEvent<SpreadDistributionJobEntityId>
{
    [IgnoreMember] public const string Actor = "SpreadDistributionJobEvent";
    [IgnoreMember] public const string Verb = "InProgressFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public SpreadDistributionJobEntityId EntityId { get; init; }
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

    public SpreadDistributionJobInProgressFailEvent() { }

    [SerializationConstructor]
    public SpreadDistributionJobInProgressFailEvent(
        ActorSubject subject,
        SpreadDistributionJobEntityId entityId,
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
