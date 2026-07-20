using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Event emitted when a fund order status changes.
/// and update read models. Targets <see cref="FundOrderId"/> as the actor entity id.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderStatusChangedEvent : IEvent<FundOrderId>
{
    [IgnoreMember] public const string Actor = "FundOrder";
    [IgnoreMember] public const string Verb = "StatusChanged";
    [IgnoreMember] public const string StatusChangedComplete = "StatusChangedComplete";
    [IgnoreMember] public const string StatusChangedFail = "StatusChangedFail";
    [IgnoreMember] public const int ErrorCode = 3016;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FundOrderId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public OrderStatus OrderStatus { get; init; }
    [Key(9)] public DateTime UpdatedOn { get; init; }
    [Key(10)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FundOrderStatusChangedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FundOrderStatusChangedEvent(
        ActorSubject subject,
        Guid id,
        FundOrderId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        OrderStatus orderStatus,
        DateTime updatedOn,
        string updatedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        OrderStatus = orderStatus;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundOrderId))
            throw new InvalidOperationException($"{EventName}.ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundOrderId).FullName}.");

        ICompleteEvent<FundOrderId> completed = new FundOrderStatusChangedCompletedEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, StatusChangedComplete, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            OrderStatus = this.OrderStatus,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
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
        if (typeof(TEntityId) != typeof(FundOrderId))
            throw new InvalidOperationException($"{EventName}.ToFailedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundOrderId).FullName}.");

        IErrorEvent<FundOrderId> failed = new FundOrderStatusChangedFailedEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, StatusChangedFail, this.Subject.EntityId),
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
/// Event published when a fund order status change has completed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderStatusChangedCompletedEvent : ICompleteEvent<FundOrderId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundOrderId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public OrderStatus OrderStatus { get; init; }
    [Key(9)] public DateTime UpdatedOn { get; init; }
    [Key(10)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FundOrderStatusChangedCompletedEvent() { }

    [SerializationConstructor]
    public FundOrderStatusChangedCompletedEvent(
        ActorSubject subject,
        FundOrderId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        OrderStatus orderStatus,
        DateTime updatedOn,
        string updatedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        OrderStatus = orderStatus;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when a fund order status change has failed.
/// Contains standardized error details for downstream handling.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderStatusChangedFailedEvent : IErrorEvent<FundOrderId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundOrderId EntityId { get; init; }
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

    public FundOrderStatusChangedFailedEvent() { }

    [SerializationConstructor]
    public FundOrderStatusChangedFailedEvent(
        ActorSubject subject,
        FundOrderId entityId,
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
