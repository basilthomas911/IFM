using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeSpreadDistributionStatisticsUpdatedCompleteEvent : ICompleteEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTradeEvent";
    [IgnoreMember] public const string Verb = "SpreadDistributionStatisticsUpdatedComplete";

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
    [Key(12)] public TradeStatus TradeStatus { get; init; }
    [Key(13)] public TradeSpreadDistributionSnapshot PutSpreadDistribution { get; init; }
    [Key(14)] public TradeSpreadDistributionSnapshot CallSpreadDistribution { get; init; }
    [Key(15)] public double LossProbability { get; init; }
    [Key(16)] public decimal TradePnl { get; init; }
    [Key(17)] public DateTime UpdatedOn { get; init; }
    [Key(18)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public OptionTradeSpreadDistributionStatisticsUpdatedCompleteEvent() { }

    [SerializationConstructor]
    public OptionTradeSpreadDistributionStatisticsUpdatedCompleteEvent(
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
        TradeStatus tradeStatus,
        TradeSpreadDistributionSnapshot putSpreadDistribution,
        TradeSpreadDistributionSnapshot callSpreadDistribution,
        double lossProbability,
        decimal tradePnl,
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
        TradeStatus = tradeStatus;
        PutSpreadDistribution = putSpreadDistribution;
        CallSpreadDistribution = callSpreadDistribution;
        LossProbability = lossProbability;
        TradePnl = tradePnl;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}

