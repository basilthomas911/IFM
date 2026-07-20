using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeEndOfDayProcessedEvent : IEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "OptionTradeEvent";
    [IgnoreMember] public const string Verb = "EndOfDayProcessed";
    [IgnoreMember] public const int ErrorCode = 9057;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public OptionTradeEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public int FundId { get; init; }
    [Key(9)] public int OrderId { get; init; }
    [Key(10)] public TradePositionEntityId EodKey { get; init; }
    [Key(11)] public string Symbol { get; init; }
    [Key(12)] public decimal OpenPrice { get; init; }
    [Key(13)] public decimal HighPrice { get; init; }
    [Key(14)] public decimal LowPrice { get; init; }
    [Key(15)] public decimal ClosePrice { get; init; }
    [Key(16)] public int Volume { get; init; }
    [Key(17)] public decimal TradePnl { get; init; }
    [Key(18)] public string Reference { get; init; }
    [Key(19)] public DateTime UpdatedOn { get; init; }
    [Key(20)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public OptionTradeEndOfDayProcessedEvent() { }

    [SerializationConstructor]
    public OptionTradeEndOfDayProcessedEvent(
        ActorSubject subject,
        Guid id,
        OptionTradeEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int fundId,
        int orderId,
        TradePositionEntityId eodKey,
        string symbol,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        int volume,
        decimal tradePnl,
        string reference,
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
        FundId = fundId;
        OrderId = orderId;
        EodKey = eodKey;
        Symbol = symbol ?? string.Empty;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        TradePnl = tradePnl;
        Reference = reference ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(OptionTradeEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

        ICompleteEvent<OptionTradeEntityId> completed = new OptionTradeEndOfDayProcessedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeEndOfDayProcessedCompleteEvent.Actor, OptionTradeEndOfDayProcessedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FundId = this.FundId,
            OrderId = this.OrderId,
            EodKey = this.EodKey,
            Symbol = this.Symbol,
            OpenPrice = this.OpenPrice,
            HighPrice = this.HighPrice,
            LowPrice = this.LowPrice,
            ClosePrice = this.ClosePrice,
            Volume = this.Volume,
            TradePnl = this.TradePnl,
            Reference = this.Reference,
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

        IErrorEvent<OptionTradeEntityId> failed = new OptionTradeEndOfDayProcessedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeEndOfDayProcessedFailEvent.Actor, OptionTradeEndOfDayProcessedFailEvent.Verb, this.Subject.EntityId),
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
public record OptionTradeEndOfDayProcessedCompleteEvent : ICompleteEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "OptionTradeEvent";
    [IgnoreMember] public const string Verb = "EndOfDayProcessedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public OptionTradeEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public int FundId { get; init; }
    [Key(9)] public int OrderId { get; init; }
    [Key(10)] public TradePositionEntityId EodKey { get; init; }
    [Key(11)] public string Symbol { get; init; }
    [Key(12)] public decimal OpenPrice { get; init; }
    [Key(13)] public decimal HighPrice { get; init; }
    [Key(14)] public decimal LowPrice { get; init; }
    [Key(15)] public decimal ClosePrice { get; init; }
    [Key(16)] public int Volume { get; init; }
    [Key(17)] public decimal TradePnl { get; init; }
    [Key(18)] public string Reference { get; init; }
    [Key(19)] public DateTime UpdatedOn { get; init; }
    [Key(20)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public OptionTradeEndOfDayProcessedCompleteEvent() { }

    [SerializationConstructor]
    public OptionTradeEndOfDayProcessedCompleteEvent(
        ActorSubject subject,
        OptionTradeEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int fundId,
        int orderId,
        TradePositionEntityId eodKey,
        string symbol,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        int volume,
        decimal tradePnl,
        string reference,
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
        FundId = fundId;
        OrderId = orderId;
        EodKey = eodKey;
        Symbol = symbol ?? string.Empty;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        TradePnl = tradePnl;
        Reference = reference ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeEndOfDayProcessedFailEvent : IErrorEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "OptionTradeEvent";
    [IgnoreMember] public const string Verb = "EndOfDayProcessedFail";

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

    public OptionTradeEndOfDayProcessedFailEvent() { }

    [SerializationConstructor]
    public OptionTradeEndOfDayProcessedFailEvent(
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

