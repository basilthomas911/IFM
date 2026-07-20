using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

/// <summary>
/// Represents a domain event indicating that futures RSI signals have been generated.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalsGeneratedEvent : IEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "SignalsGenerated";
    [IgnoreMember] public const int ErrorCode = 19002;

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
    [Key(8)] public FuturesRsiSignalsId FuturesRsiSignalsId { get; init; }
    [Key(9)] public FuturesRsiSignalReadModel[] FuturesRsiSignals { get; init; }
    [Key(10)] public int PeriodLength { get; init; }
    [Key(11)] public DateTime CreatedOn { get; init; }
    [Key(12)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesRsiSignalsGeneratedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesRsiSignalsGeneratedEvent(
        ActorSubject subject,
        Guid id,
        FuturesRsiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesRsiSignalsId futuresRsiSignalsId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        int periodLength,
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
        FuturesRsiSignalsId = futuresRsiSignalsId;
        FuturesRsiSignals = futuresRsiSignals ?? [];
        PeriodLength = periodLength;
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
        if (typeof(TEntityId) != typeof(FuturesRsiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesRsiSignalEntityId).FullName}.");

        ICompleteEvent<FuturesRsiSignalEntityId> completed = new FuturesRsiSignalsGeneratedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalsGeneratedCompleteEvent.Actor, FuturesRsiSignalsGeneratedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FuturesRsiSignalsId = this.FuturesRsiSignalsId,
            FuturesRsiSignals = this.FuturesRsiSignals,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
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

        IErrorEvent<FuturesRsiSignalEntityId> failed = new FuturesRsiSignalsGeneratedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalsGeneratedFailEvent.Actor, FuturesRsiSignalsGeneratedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when futures RSI signals have been generated successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalsGeneratedCompleteEvent : ICompleteEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "SignalsGeneratedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesRsiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesRsiSignalsId FuturesRsiSignalsId { get; init; }
    [Key(9)] public FuturesRsiSignalReadModel[] FuturesRsiSignals { get; init; }
    [Key(10)] public int PeriodLength { get; init; }
    [Key(11)] public DateTime CreatedOn { get; init; }
    [Key(12)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesRsiSignalsGeneratedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesRsiSignalsGeneratedCompleteEvent(
        ActorSubject subject,
        FuturesRsiSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesRsiSignalsId futuresRsiSignalsId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        int periodLength,
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
        FuturesRsiSignalsId = futuresRsiSignalsId;
        FuturesRsiSignals = futuresRsiSignals ?? [];
        PeriodLength = periodLength;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when generating futures RSI signals fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalsGeneratedFailEvent : IErrorEvent<FuturesRsiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalEvent";
    [IgnoreMember] public const string Verb = "SignalsGeneratedFail";

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

    public FuturesRsiSignalsGeneratedFailEvent() { }

    [SerializationConstructor]
    public FuturesRsiSignalsGeneratedFailEvent(
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
