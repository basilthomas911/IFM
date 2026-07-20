using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Events;

[MessagePackObject(AllowPrivate = true)]
public record TradePositionStatusUpdatedEvent : IEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "TradePositionEvent";
    [IgnoreMember] public const string Verb = "StatusUpdated";
    [IgnoreMember] public const int ErrorCode = 7015;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public OptionTradeEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public int OrderId { get; init; }
    [Key(9)] public int TradeId { get; init; }
    [Key(10)] public TradeType TradeType { get; init; }
    [Key(11)] public DateOnly ValueDate { get; init; }
    [Key(12)] public int DaysToExpiry { get; init; }
    [Key(13)] public TradeStatus OldTradeStatus { get; init; }
    [Key(14)] public TradeStatus NewTradeStatus { get; init; }
    [Key(15)] public DateTime UpdatedOn { get; init; }
    [Key(16)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public TradePositionStatusUpdatedEvent() { }

    [SerializationConstructor]
    public TradePositionStatusUpdatedEvent(
        ActorSubject subject,
        Guid id,
        OptionTradeEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus oldTradeStatus,
        TradeStatus newTradeStatus,
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
        TradeType = tradeType;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        OldTradeStatus = oldTradeStatus;
        NewTradeStatus = newTradeStatus;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(OptionTradeEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

        ICompleteEvent<OptionTradeEntityId> completed = new TradePositionStatusUpdatedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, TradePositionStatusUpdatedCompleteEvent.Actor, TradePositionStatusUpdatedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            OrderId = this.OrderId,
            TradeId = this.TradeId,
            TradeType = this.TradeType,
            ValueDate = this.ValueDate,
            DaysToExpiry = this.DaysToExpiry,
            OldTradeStatus = this.OldTradeStatus,
            NewTradeStatus = this.NewTradeStatus,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(OptionTradeEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

        IErrorEvent<OptionTradeEntityId> failed = new TradePositionStatusUpdatedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, TradePositionStatusUpdatedFailEvent.Actor, TradePositionStatusUpdatedFailEvent.Verb, this.Subject.EntityId),
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

[MessagePackObject(AllowPrivate = true)]
public record TradePositionStatusUpdatedCompleteEvent : ICompleteEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "TradePositionEvent";
    [IgnoreMember] public const string Verb = "StatusUpdatedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public OptionTradeEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public int OrderId { get; init; }
    [Key(9)] public int TradeId { get; init; }
    [Key(10)] public TradeType TradeType { get; init; }
    [Key(11)] public DateOnly ValueDate { get; init; }
    [Key(12)] public int DaysToExpiry { get; init; }
    [Key(13)] public TradeStatus OldTradeStatus { get; init; }
    [Key(14)] public TradeStatus NewTradeStatus { get; init; }
    [Key(15)] public DateTime UpdatedOn { get; init; }
    [Key(16)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public TradePositionStatusUpdatedCompleteEvent() { }

    [SerializationConstructor]
    public TradePositionStatusUpdatedCompleteEvent(
        ActorSubject subject,
        OptionTradeEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus oldTradeStatus,
        TradeStatus newTradeStatus,
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
        TradeType = tradeType;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        OldTradeStatus = oldTradeStatus;
        NewTradeStatus = newTradeStatus;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record TradePositionStatusUpdatedFailEvent : IErrorEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "TradePositionEvent";
    [IgnoreMember] public const string Verb = "StatusUpdatedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public OptionTradeEntityId EntityId { get; init; }
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

    public TradePositionStatusUpdatedFailEvent() { }

    [SerializationConstructor]
    public TradePositionStatusUpdatedFailEvent(
        ActorSubject subject,
        OptionTradeEntityId entityId,
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
