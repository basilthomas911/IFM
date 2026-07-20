using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Events;

[MessagePackObject(AllowPrivate = true)]
public record OptionTradeSpreadDistributionStatisticsChangedEvent : IEvent<OptionTradeEntityId>
{
    [IgnoreMember] public const string Actor = "OptionTradeEvent";
    [IgnoreMember] public const string Verb = "SpreadDistributionStatisticsChanged";
    [IgnoreMember] public const int ErrorCode = 6004;

    // base metadata (keys 0..7)
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
    [Key(10)] public double ForwardLossRatio { get; init; }
    [Key(11)] public double LossProbability { get; init; }
    [Key(12)] public DateOnly ValueDate { get; init; }
    [Key(13)] public DateTime UpdatedOn { get; init; }
    [Key(14)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public OptionTradeSpreadDistributionStatisticsChangedEvent() { }

    [SerializationConstructor]
    public OptionTradeSpreadDistributionStatisticsChangedEvent(
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
        double forwardLossRatio,
        double lossProbability,
        DateOnly valueDate,
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
        ForwardLossRatio = forwardLossRatio;
        LossProbability = lossProbability;
        ValueDate = valueDate;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        return default!;
    }

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        return default!;
    }

}
