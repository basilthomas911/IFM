using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Represents a domain event indicating that a trade has been removed from a fund order.
/// </summary>
/// <remarks>This event is typically used in event sourcing or message-driven architectures to signal the removal
/// of a trade from a fund order. It contains metadata and payload information relevant to the removal action, such as
/// the identifiers involved, the time of removal, and the user who performed the action. Consumers of this event can
/// use it to update projections, trigger workflows, or audit changes related to fund orders.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeRemovedFromFundOrderEvent : IEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "TradeRemovedFromFundOrder";
    [IgnoreMember] public const string CompleteName = "TradeRemovedFromFundOrderComplete";
    [IgnoreMember] public const string Fail = "TradeRemovedFromFundOrderFail";
    [IgnoreMember] public const int ErrorCode = 3005;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FundId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FundOrderTradeId FundOrderTradeId { get; init; }
    [Key(9)] public DateTime RemovedOn { get; init; }
    [Key(10)] public string RemovedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public TradeRemovedFromFundOrderEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public TradeRemovedFromFundOrderEvent(
        ActorSubject subject,
        Guid id,
        FundId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderTradeId fundOrderTradeId,
        DateTime removedOn,
        string removedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundOrderTradeId = fundOrderTradeId;
        RemovedOn = removedOn;
        RemovedBy = removedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalizer event into a completed event which indicates successful handling.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundId).FullName}.");

        ICompleteEvent<FundId> completed = new TradeRemovedFromFundOrderCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, TradeRemovedFromFundOrderCompleteEvent.Actor, TradeRemovedFromFundOrderCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FundOrderTradeId = this.FundOrderTradeId,
            RemovedOn = this.RemovedOn,
            RemovedBy = this.RemovedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalizer event into a failed error event describing the provided exception.
    /// Validates the requested entity id type and returns a strongly-typed error event.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundId).FullName}.");

        IErrorEvent<FundId> failed = new TradeRemovedFromFundOrderFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, TradeRemovedFromFundOrderFailEvent.Actor, TradeRemovedFromFundOrderFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a trade has been removed from a fund order successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record TradeRemovedFromFundOrderCompleteEvent : ICompleteEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "TradeRemovedFromFundOrderComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FundOrderTradeId FundOrderTradeId { get; init; }
    [Key(9)] public DateTime RemovedOn { get; init; }
    [Key(10)] public string RemovedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public TradeRemovedFromFundOrderCompleteEvent() { }

    [SerializationConstructor]
    public TradeRemovedFromFundOrderCompleteEvent(
        ActorSubject subject,
        FundId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderTradeId fundOrderTradeId,
        DateTime removedOn,
        string removedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundOrderTradeId = fundOrderTradeId;
        RemovedOn = removedOn;
        RemovedBy = removedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when removing a trade from a fund order fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record TradeRemovedFromFundOrderFailEvent : IErrorEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "TradeRemovedFromFundOrderFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundId EntityId { get; init; }
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

    public TradeRemovedFromFundOrderFailEvent() { }

    [SerializationConstructor]
    public TradeRemovedFromFundOrderFailEvent(
        ActorSubject subject,
        FundId entityId,
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