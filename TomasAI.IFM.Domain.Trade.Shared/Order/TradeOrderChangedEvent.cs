using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

[MessagePackObject]
public sealed record TradeOrderChangedEvent : IEvent<NewTradeOrderId>
{
    public const string Actor = "TradeOrderEvent";
    public const string Verb = "TradeOrderChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public NewTradeOrderId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public TradeOrderDefinition State { get; init; } = new();
    [Key(9)] public Guid ExecutionAttemptId { get; init; }
    [Key(10)] public ExecutionChannel ExecutionChannel { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(TradeOrderChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
