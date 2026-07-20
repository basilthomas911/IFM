using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.Events;

/// <summary>
/// Raised when a futures contract is removed. Designed for denormalization flows and supports conversion
/// to completed or failed events used by denormalizers.
/// </summary>
/// <remarks>
/// Mirrors the structure and semantics used by <see cref="FuturesContractChangedEvent"/>, including
/// MessagePack serialization annotations and typed conversion helpers.
/// Instances are not guaranteed to be thread-safe; do not share between threads without external synchronization.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractRemovedEvent : IEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "Removed";
    [IgnoreMember] public const int ErrorCode = 2003;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesContractId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload keys start after base event keys (8..)
    [Key(8)] public FuturesContractId ContractId { get; init; }
    [Key(9)] public DateTime DeletedOn { get; init; }
    [Key(10)] public string? DeletedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesContractRemovedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractRemovedEvent(
        ActorSubject subject,
        Guid id,
        FuturesContractId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractId contractId,
        DateTime deletedOn,
        string? deletedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ContractId = contractId;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy;
    }

    /// <summary>
    /// Converts this event into a typed completed event for denormalizers.
    /// </summary>
    /// <typeparam name="TComplete">The complete event type to produce.</typeparam>
    /// <typeparam name="TEntityId">The entity id type (must be <see cref="FuturesContractId"/>).</typeparam>
    /// <returns>A completed event instance.</returns>
    /// <exception cref="InvalidOperationException">If <typeparamref name="TEntityId"/> is not <see cref="FuturesContractId"/>.</exception>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesContractId))
            throw new InvalidOperationException($"{Actor}.ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesContractId).FullName}.");

        var completed = new FuturesContractRemovedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesContractRemovedCompleteEvent.Actor, FuturesContractRemovedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            ContractId = this.ContractId,
            DeletedOn = this.DeletedOn,
            DeletedBy = this.DeletedBy
        };

        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Converts this event into a typed failed/error event for denormalizers.
    /// </summary>
    /// <typeparam name="TFail">The error event type to produce.</typeparam>
    /// <typeparam name="TEntityId">The entity id type (must be <see cref="FuturesContractId"/>).</typeparam>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>An error event instance.</returns>
    /// <exception cref="InvalidOperationException">If <typeparamref name="TEntityId"/> is not <see cref="FuturesContractId"/>.</exception>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesContractId))
            throw new InvalidOperationException($"{Actor}.ToFailedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesContractId).FullName}.");

        var failed = new FuturesContractRemovedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesContractRemovedFailEvent.Actor, FuturesContractRemovedFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.Now,
            EventId = this.EventId,
            CommandId = this.CommandId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.EventService,
            ErrorCode = ErrorCode
        };

        return (IErrorEvent<TEntityId>)(object)failed;
    }
}

/// <summary>
/// Completed event produced when a futures contract removal has been processed for denormalization.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractRemovedCompleteEvent : ICompleteEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "RemovedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesContractId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesContractId? ContractId { get; init; }
    [Key(9)] public DateTime DeletedOn { get; init; }
    [Key(10)] public string? DeletedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesContractRemovedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractRemovedCompleteEvent(
        ActorSubject subject,
        FuturesContractId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractId? contractId,
        DateTime deletedOn,
        string? deletedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ContractId = contractId;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy;
    }
}

/// <summary>
/// Failed/error event produced when denormalization of a futures contract removal fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractRemovedFailEvent : IErrorEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "RemovedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesContractId EntityId { get; init; }
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

    [IgnoreMember] public string EventName => this.GetType().Name;
    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesContractRemovedFailEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractRemovedFailEvent(
        ActorSubject subject,
        FuturesContractId entityId,
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