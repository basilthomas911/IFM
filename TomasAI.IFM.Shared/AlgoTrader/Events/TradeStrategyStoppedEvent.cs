using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeOrder;

namespace TomasAI.IFM.Shared.AlgoTrader.Events;

/// <summary>
/// trade strategy stopped event
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record TradeStrategyStoppedEvent : IEvent<TradeOrderEntityId>
{
    [IgnoreMember] public const string Actor = "TradeStrategy";
    [IgnoreMember] public const string Verb = "Stopped";
    [IgnoreMember] public const int ErrorCode = 12002;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TradeOrderEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public int OrderId { get; init; }
    [Key(9)] public int TradeId { get; init; }
    [Key(10)] public TradeType TradeType { get; init; }
    [Key(11)] public DateOnly valueDate { get; init; }
    [Key(12)] public string TradeStrategyName { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(TradeStrategyStoppedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public TradeStrategyStoppedEvent() { }

    [SerializationConstructor]
    public TradeStrategyStoppedEvent(
        ActorSubject subject,
        Guid id,
        TradeOrderEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        string tradeStrategyName)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        this.valueDate = valueDate;
        TradeStrategyName = tradeStrategyName;
    }
}
