using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeOrderPlacedEvent : IEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTradeEvent";
    [IgnoreMember] public const string Verb = "OrderPlaced";
    [IgnoreMember] public const int ErrorCode = 9058;

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
    [Key(9)] public DateOnly ValueDate { get; init; }
    [Key(10)] public decimal OrderPrice { get; init; }
    [Key(11)] public OrderActionType OrderAction { get; init; }
    [Key(12)] public OrderType OrderType { get; init; }
    [Key(13)] public OptionTradeReadModel OptionTrade { get; init; }
    [Key(14)] public decimal TradeCommission { get; init; }
    [Key(15)] public decimal TradePnl { get; init; }
    [Key(16)] public DateTime CreatedOn { get; init; }
    [Key(17)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public OptionTradeOrderPlacedEvent() { }

    [SerializationConstructor]
    public OptionTradeOrderPlacedEvent(
        ActorSubject subject,
        Guid id,
        OptionTradeEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int fundId,
        DateOnly valueDate,
        decimal orderPrice,
        OrderActionType orderAction,
        OrderType orderType,
        OptionTradeReadModel optionTrade,
        decimal tradeCommission,
        decimal tradePnl,
        DateTime createdOn,
        string createdBy)
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
        ValueDate = valueDate;
        OrderPrice = orderPrice;
        OrderAction = orderAction;
        OrderType = orderType;
        OptionTrade = optionTrade;
        TradeCommission = tradeCommission;
        TradePnl = tradePnl;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(OptionTradeEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

        ICompleteEvent<OptionTradeEntityId> completed = new OptionTradeOrderPlacedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeOrderPlacedCompleteEvent.Actor, OptionTradeOrderPlacedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FundId = this.FundId,
            ValueDate = this.ValueDate,
            OrderPrice = this.OrderPrice,
            OrderAction = this.OrderAction,
            OrderType = this.OrderType,
            OptionTrade = this.OptionTrade,
            TradeCommission = this.TradeCommission,
            TradePnl = this.TradePnl,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (typeof(TEntityId) != typeof(OptionTradeEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

        IErrorEvent<OptionTradeEntityId> failed = new OptionTradeOrderPlacedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeOrderPlacedFailEvent.Actor, OptionTradeOrderPlacedFailEvent.Verb, this.Subject.EntityId),
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
