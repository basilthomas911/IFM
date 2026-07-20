using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

[MessagePackObject(AllowPrivate = true)]
public record TradeLiveFeedTurnedOnEvent : IEvent<TradeLiveFeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "TurnedOn";
    [IgnoreMember] public const int ErrorCode = 0;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TradeLiveFeedId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public int OrderId { get; init; }
    [Key(9)] public int TradeId { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(TradeLiveFeedTurnedOnEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public TradeLiveFeedTurnedOnEvent() { }

    [SerializationConstructor]
    public TradeLiveFeedTurnedOnEvent(
        ActorSubject subject,
        Guid id,
        TradeLiveFeedId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int orderId,
        int tradeId,
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
        OrderId = orderId;
        TradeId = tradeId;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(TradeLiveFeedId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(TradeLiveFeedId).FullName}.");

        ICompleteEvent<TradeLiveFeedId> completed = new TradeLiveFeedTurnedOnCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedTurnedOnCompleteEvent.Actor, TradeLiveFeedTurnedOnCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            OrderId = this.OrderId,
            TradeId = this.TradeId,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// Validates the requested entity id type and returns a strongly-typed error event.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(TradeLiveFeedId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(TradeLiveFeedId).FullName}.");

        IErrorEvent<TradeLiveFeedId> failed = new TradeLiveFeedTurnedOnFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedTurnedOnFailEvent.Actor, TradeLiveFeedTurnedOnFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a trade live feed has been turned on successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record TradeLiveFeedTurnedOnCompleteEvent : ICompleteEvent<TradeLiveFeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "TurnedOnComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public TradeLiveFeedId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public int OrderId { get; init; }
    [Key(9)] public int TradeId { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public TradeLiveFeedTurnedOnCompleteEvent() { }

    [SerializationConstructor]
    public TradeLiveFeedTurnedOnCompleteEvent(
        ActorSubject subject,
        TradeLiveFeedId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int orderId,
        int tradeId,
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
        OrderId = orderId;
        TradeId = tradeId;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when turning on a trade live feed fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record TradeLiveFeedTurnedOnFailEvent : IErrorEvent<TradeLiveFeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "TurnedOnFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public TradeLiveFeedId EntityId { get; init; }
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

    public TradeLiveFeedTurnedOnFailEvent() { }

    [SerializationConstructor]
    public TradeLiveFeedTurnedOnFailEvent(
        ActorSubject subject,
        TradeLiveFeedId entityId,
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
