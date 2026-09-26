using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

[MessagePackObject]
public sealed record BrokerOrderChangedEvent : IEvent<BrokerOrderId>
{
    public const string Actor = "BrokerOrderEvent";
    public const string Verb = "BrokerOrderChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public BrokerOrderId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public BrokerOrderDefinition State { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(BrokerOrderChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
