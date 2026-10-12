using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeEndOfDayProcessedCompleteEvent : ICompleteEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTradeEvent";
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
    [Key(16)] public long Volume { get; init; }
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
        long volume,
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

