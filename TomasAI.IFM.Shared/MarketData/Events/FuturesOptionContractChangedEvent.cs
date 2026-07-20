using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Events;

/// <summary>
/// Represents a domain event indicating that a futures option contract has been changed.
/// </summary>
/// <remarks>This event is typically published when a modification occurs to a futures option contract entity,
/// capturing both the original contract identifier and the updated contract details. It is used within event sourcing
/// and denormalization workflows to propagate contract changes throughout the system. Consumers can use this event to
/// update projections or trigger downstream processes that depend on contract state changes.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractChangedEvent : IEvent<FuturesOptionContractEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "Changed";
    [IgnoreMember] public const int ErrorCode = 2005;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload keys (8..)
    [Key(8)] public string OriginalContractId { get; init; }
    [Key(9)] public FuturesOptionContractReadModel Contract { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string? UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesOptionContractChangedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractChangedEvent(
        ActorSubject subject,
        Guid id,
        FuturesOptionContractEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        string originalContractId,
        FuturesOptionContractReadModel contract,
        DateTime updatedOn,
        string? updatedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        OriginalContractId = originalContractId ?? string.Empty;
        Contract = contract;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Creates a completed event instance representing the finalized state of a futures option contract change.
    /// </summary>
    /// <typeparam name="TComplete">The type of completed event to create.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier (must be <see cref="FuturesOptionContractEntityId"/>).</typeparam>
    /// <returns>An instance of <see cref="ICompleteEvent{TEntityId}"/> containing the completed event data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="TEntityId"/> is not <see cref="FuturesOptionContractEntityId"/>.</exception>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesOptionContractEntityId))
            throw new InvalidOperationException($"ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesOptionContractEntityId).FullName}.");

        var completed = new FuturesOptionContractChangedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractChangedCompleteEvent.Actor, FuturesOptionContractChangedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            OriginalContractId = this.OriginalContractId,
            Contract = this.Contract,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
        };

        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Creates a failed error event for a futures option contract entity using the specified exception.
    /// </summary>
    /// <typeparam name="TFail">The type of error event to create.</typeparam>
    /// <typeparam name="TEntityId">The entity id type (must be <see cref="FuturesOptionContractEntityId"/>).</typeparam>
    /// <param name="ex">The exception that describes the error to associate with the failed event.</param>
    /// <returns>An error event containing details of the failure.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="TEntityId"/> is not <see cref="FuturesOptionContractEntityId"/>.</exception>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesOptionContractEntityId))
            throw new InvalidOperationException($"ToFailedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesOptionContractEntityId).FullName}.");

        var failed = new FuturesOptionContractChangedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractChangedFailEvent.Actor, FuturesOptionContractChangedFailEvent.Verb, this.Subject.EntityId),
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
/// Completed event produced when a futures option contract change has been processed for denormalization.
/// Carries the updated contract and metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractChangedCompleteEvent : ICompleteEvent<FuturesOptionContractEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "ChangedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public string? OriginalContractId { get; init; }
    [Key(9)] public FuturesOptionContractReadModel? Contract { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string? UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <summary>
    /// Parameterless constructor required for serializers.
    /// </summary>
    public FuturesOptionContractChangedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractChangedCompleteEvent(
        ActorSubject subject,
        FuturesOptionContractEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        string? originalContractId,
        FuturesOptionContractReadModel? contract,
        DateTime updatedOn,
        string? updatedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        OriginalContractId = originalContractId;
        Contract = contract;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
    }
}

/// <summary>
/// Failed/error event produced when denormalization of a futures option contract change fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractChangedFailEvent : IErrorEvent<FuturesOptionContractEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractEvent";
    [IgnoreMember] public const string Verb = "ChangedFail";

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
    public FuturesOptionContractChangedFailEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionContractChangedFailEvent(
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