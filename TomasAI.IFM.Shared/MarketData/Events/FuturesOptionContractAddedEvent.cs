using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Events;

/// <summary>
/// Event emitted when a new futures option contract is added to the system.
/// and emit completed/failed events derived from this event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractAddedEvent : IEvent<FuturesOptionContractEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "Added";
    [IgnoreMember] public const int ErrorCode = 2004;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload keys (8..)
    [Key(8)] public FuturesOptionContractReadModel Contract { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesOptionContractAddedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractAddedEvent(
        ActorSubject subject,
        Guid id,
        FuturesOptionContractEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionContractReadModel contract,
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
        Contract = contract;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    /// <typeparam name="TComplete">The complete event type to produce.</typeparam>
    /// <typeparam name="TEntityId">The entity id type (must be <see cref="FuturesOptionContractEntityId"/>).</typeparam>
    /// <returns>A completed event instance.</returns>
    /// <exception cref="InvalidOperationException">If <typeparamref name="TEntityId"/> is not <see cref="FuturesOptionContractEntityId"/>.</exception>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesOptionContractEntityId))
            throw new InvalidOperationException($"ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesOptionContractEntityId).FullName}.");

        var completed = new FuturesOptionContractAddedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractAddedCompleteEvent.Actor, FuturesOptionContractAddedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            Contract = this.Contract,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    /// <typeparam name="TFail">The error event type to produce.</typeparam>
    /// <typeparam name="TEntityId">The entity id type (must be <see cref="FuturesOptionContractEntityId"/>).</typeparam>
    /// <param name="ex">The exception encountered while denormalizing.</param>
    /// <returns>An <see cref="IErrorEvent{TEntityId}"/> describing the failure.</returns>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var failed = new FuturesOptionContractAddedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractAddedFailEvent.Actor, FuturesOptionContractAddedFailEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
        return (IErrorEvent<TEntityId>)(object)failed;
    }
}

/// <summary>
/// Event published when a futures option contract add operation has been completed successfully.
/// Carries the added contract and metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractAddedCompleteEvent : ICompleteEvent<FuturesOptionContractEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "AddedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesOptionContractReadModel? Contract { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string? CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesOptionContractAddedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractAddedCompleteEvent(
        ActorSubject subject,
        FuturesOptionContractEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionContractReadModel? contract,
        DateTime createdOn,
        string? createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        Contract = contract;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }
}

/// <summary>
/// Event published when a futures option contract add operation has failed.
/// Inherits standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractAddedFailEvent : IErrorEvent<FuturesOptionContractEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "AddedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionContractEntityId EntityId { get; init; }
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
    public FuturesOptionContractAddedFailEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractAddedFailEvent(
        ActorSubject subject,
        FuturesOptionContractEntityId entityId,
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
