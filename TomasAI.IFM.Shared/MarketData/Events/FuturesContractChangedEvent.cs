using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Events;

/// <summary>
/// Represents an event indicating that a futures contract has been changed, including details about the contract update
/// and its context.
/// </summary>
/// <remarks>This event is typically used within event-driven or denormalization workflows to signal that a
/// futures contract has been modified. It contains metadata about the change, such as the original contract identifier,
/// the updated contract view, and information about the actor and command that initiated the change. The event supports
/// conversion to completed or failed event types for downstream processing. Thread safety is not guaranteed; instances
/// should not be shared between threads without external synchronization.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractChangedEvent :
    IEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "Changed";
    [IgnoreMember] public const string Complete = "ChangedComplete";
    [IgnoreMember] public const string Fail = "ChangedFail";
    [IgnoreMember] public const int ErrorCode = 2002;
    
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesContractId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload keys start after base event keys (8..)
    [Key(8)] public FuturesContractId OriginalContractId { get; init; }
    [Key(9)] public FuturesContractV2ReadModel Contract { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string? UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesContractChangedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractChangedEvent(
        ActorSubject subject,
        Guid id,
        FuturesContractId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractId originalContractId,
        FuturesContractV2ReadModel contract,
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

    /// <summary>
    /// Creates a completed event instance representing the finalized state of a futures contract change.
    /// </summary>
    /// <remarks>This method is specific to futures contract events and only supports entity identifiers of
    /// type <see cref="FuturesContractId"/>. Attempting to use other entity identifier types will result in an
    /// exception.</remarks>
    /// <typeparam name="TComplete">The type of completed event to create. Must implement <see cref="ICompleteEvent{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the event. Must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <returns>An instance of <see cref="ICompleteEvent{TEntityId}"/> containing the completed event data for the specified
    /// entity identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="TEntityId"/> is not of type <see cref="FuturesContractId"/>.</exception>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesContractId))
            throw new InvalidOperationException($"ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesContractId).FullName}.");

        var completed = new FuturesContractChangedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesContractChangedCompleteEvent.Actor, FuturesContractChangedCompleteEvent.Verb, this.Subject.EntityId),
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
    /// Creates a failed error event for a futures contract entity using the specified exception.
    /// </summary>
    /// <remarks>This method is specific to futures contract entities and cannot be used with other entity
    /// identifier types.</remarks>
    /// <typeparam name="TFail">The type of error event to create. Must implement <see cref="IErrorEvent{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier. Must implement <see cref="IActorEntityId"/> and be <see
    /// cref="FuturesContractId"/>.</typeparam>
    /// <param name="ex">The exception that describes the error to associate with the failed event.</param>
    /// <returns>An error event of type <typeparamref name="TFail"/> containing details of the failure.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="TEntityId"/> is not <see cref="FuturesContractId"/>.</exception>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FuturesContractId))
            throw new InvalidOperationException($"ToFailedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesContractId).FullName}.");

        var failed = new FuturesContractChangedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesContractChangedFailEvent.Actor, FuturesContractChangedFailEvent.Verb, this.Subject.EntityId),
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
/// Completed event for futures contract change denormalization.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractChangedCompleteEvent : ICompleteEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "ChangedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesContractId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesContractId? OriginalContractId { get; init; }
    [Key(9)] public FuturesContractV2ReadModel? Contract { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string? UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesContractChangedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesContractChangedCompleteEvent(
        ActorSubject subject,
        FuturesContractId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractId? originalContractId,
        FuturesContractV2ReadModel? contract,
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
/// Failed event for futures contract change denormalization.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractChangedFailEvent : IErrorEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "ChangedFail";

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

    public FuturesContractChangedFailEvent() { }

    [SerializationConstructor]
    public FuturesContractChangedFailEvent(
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