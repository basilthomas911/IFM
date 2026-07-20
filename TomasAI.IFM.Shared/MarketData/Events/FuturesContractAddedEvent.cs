using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Events;

/// <summary>
/// Event emitted when a new futures contract is added to the system.
/// Implements <see cref="DomainEvent"/> so denormalizers can update read models
/// and emit completed/failed events derived from this event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractAddedEvent :
     IEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "Added";
    [IgnoreMember] public const int ErrorCode = 2001;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesContractId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesContractV2ReadModel Contract { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesContractAddedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractAddedEvent(
        ActorSubject subject,
        Guid id,
        FuturesContractId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractV2ReadModel contract,
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
    /// This generic method implementation always returns a new <see cref="FuturesContractAddedCompleteEvent"/>.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        // Ensure caller requested the correct entity id type.
        if (typeof(TEntityId) != typeof(FuturesContractId))
            throw new InvalidOperationException($"{EventName}.ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesContractId).FullName}.");

        var completed = new FuturesContractAddedCompleteEvent
        {
            Subject = new ActorSubject( ActorType.Event, FuturesContractAddedCompleteEvent.Actor, FuturesContractAddedCompleteEvent.Verb, this.Subject.EntityId),
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

        // Safe cast via object because compiled generic type parameter TEntityId at runtime must be FuturesContractId.
        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    /// <param name="ex">The exception encountered while denormalizing.</param>
    /// <returns>An <see cref="IErrorEvent"/> describing the failure.</returns>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var failed = new FuturesContractAddedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesContractAddedFailEvent.Actor, FuturesContractAddedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a futures contract add operation has been completed successfully.
/// Carries the added contract and metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractAddedCompleteEvent : ICompleteEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "AddedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesContractId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesContractV2ReadModel? Contract { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string? CreatedBy { get; init; }

    [IgnoreMember]
    public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [IgnoreMember]
    public string EventName => GetType().Name;

    [IgnoreMember]
    public EventType EventType => EventType.DomainEvent;

    public FuturesContractAddedCompleteEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractAddedCompleteEvent(
        ActorSubject subject,
        FuturesContractId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractV2ReadModel? contract,
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
/// Event published when a futures contract add operation has failed.
/// Inherits standardized error details from <see cref="ErrorEvent"/>.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractAddedFailEvent : IErrorEvent<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractEvent";
    [IgnoreMember] public const string Verb = "AddedFail";

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

    public FuturesContractAddedFailEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesContractAddedFailEvent(
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