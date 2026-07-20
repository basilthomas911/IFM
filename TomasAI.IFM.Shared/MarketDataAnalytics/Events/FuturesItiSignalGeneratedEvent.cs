using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

/// <summary>
/// Represents a domain event indicating that a Futures ITI signal has been generated.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalGeneratedEvent : IEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "Generated";
    [IgnoreMember] public const int ErrorCode = 19002;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    /// <summary>The generated Futures ITI signal view model.</summary>
    [Key(8)] public FuturesItiSignalV2ReadModel? FuturesItiSignal { get; init; }
    /// <summary>The date and time when the signal was created.</summary>
    [Key(9)] public DateTime CreatedOn { get; init; }
    /// <summary>The user who created the signal.</summary>
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public FuturesItiSignalGeneratedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesItiSignalGeneratedEvent(
        ActorSubject subject,
        Guid id,
        FuturesItiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesItiSignalV2ReadModel? futuresItiSignal,
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
        FuturesItiSignal = futuresItiSignal;
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
        if (typeof(TEntityId) != typeof(FuturesItiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesItiSignalEntityId).FullName}.");

        ICompleteEvent<FuturesItiSignalEntityId> completed = new FuturesItiSignalGeneratedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalGeneratedCompleteEvent.Actor, FuturesItiSignalGeneratedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FuturesItiSignal = this.FuturesItiSignal,
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
        if (typeof(TEntityId) != typeof(FuturesItiSignalEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesItiSignalEntityId).FullName}.");

        IErrorEvent<FuturesItiSignalEntityId> failed = new FuturesItiSignalGeneratedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalGeneratedFailEvent.Actor, FuturesItiSignalGeneratedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a Futures ITI signal has been generated successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalGeneratedCompleteEvent : ICompleteEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "GeneratedComplete";
    [IgnoreMember] public const int ErrorCode = 19002;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesItiSignalV2ReadModel? FuturesItiSignal { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public FuturesItiSignalGeneratedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesItiSignalGeneratedCompleteEvent(
        ActorSubject subject,
        FuturesItiSignalEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesItiSignalV2ReadModel? futuresItiSignal,
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
        FuturesItiSignal = futuresItiSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when generating a Futures ITI signal fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalGeneratedFailEvent : IErrorEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "GeneratedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesItiSignalEntityId EntityId { get; init; }
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

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public FuturesItiSignalGeneratedFailEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesItiSignalGeneratedFailEvent(
        ActorSubject subject,
        FuturesItiSignalEntityId entityId,
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
