using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeOrderPlacedCompleteEvent : ICompleteEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTradeEvent";
    [IgnoreMember] public const string Verb = "OrderPlacedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public OptionTradeEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

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
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public OptionTradeOrderPlacedCompleteEvent() { }

    [SerializationConstructor]
    public OptionTradeOrderPlacedCompleteEvent(
        ActorSubject subject,
        OptionTradeEntityId entityId,
        Guid id,
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
        EntityId = entityId;
        Id = id;
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
}
