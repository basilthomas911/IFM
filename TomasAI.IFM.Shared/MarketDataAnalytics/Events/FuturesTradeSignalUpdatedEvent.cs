using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesTradeSignalUpdatedEvent : IEvent<FuturesTradeSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalEvent";
    [IgnoreMember] public const string Verb = "Updated";
    [IgnoreMember] public const int ErrorCode = 19001;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesTradeSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string? CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesTradeSignalUpdatedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesTradeSignalUpdatedEvent() { }

    [SerializationConstructor]
    public FuturesTradeSignalUpdatedEvent(
        ActorSubject subject,
        Guid id,
        FuturesTradeSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesTradeSignalV2ReadModel? futuresTradeSignal,
        DateTime createdOn,
        string? createdBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        FuturesTradeSignal = futuresTradeSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesTradeSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesTradeSignalEntityId).FullName}.");

        ICompleteEvent<FuturesTradeSignalEntityId> completed = new FuturesTradeSignalUpdatedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTradeSignalUpdatedCompleteEvent.Actor, FuturesTradeSignalUpdatedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FuturesTradeSignal = this.FuturesTradeSignal,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesTradeSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesTradeSignalEntityId).FullName}.");

        IErrorEvent<FuturesTradeSignalEntityId> failed = new FuturesTradeSignalUpdatedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTradeSignalUpdatedFailEvent.Actor, FuturesTradeSignalUpdatedFailEvent.Verb, this.Subject.EntityId),
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
public record FuturesTradeSignalUpdatedCompleteEvent : ICompleteEvent<FuturesTradeSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalEvent";
    [IgnoreMember] public const string Verb = "UpdatedComplete";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesTradeSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string? CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesTradeSignalUpdatedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesTradeSignalUpdatedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesTradeSignalUpdatedCompleteEvent(
        ActorSubject subject,
        FuturesTradeSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesTradeSignalV2ReadModel? futuresTradeSignal,
        DateTime createdOn,
        string? createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        FuturesTradeSignal = futuresTradeSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record FuturesTradeSignalUpdatedFailEvent : IErrorEvent<FuturesTradeSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalEvent";
    [IgnoreMember] public const string Verb = "UpdatedFail";
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesTradeSignalEntityId EntityId { get; init; }
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

    [IgnoreMember] public string EventName => nameof(FuturesTradeSignalUpdatedFailEvent);
    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    public FuturesTradeSignalUpdatedFailEvent() { }

    [SerializationConstructor]
    public FuturesTradeSignalUpdatedFailEvent(
        ActorSubject subject,
        FuturesTradeSignalEntityId entityId,
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
        EventSource = eventSource;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId;
        CommandName = commandName;
        CommandData = commandData;
    }
}
