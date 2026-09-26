using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

[MessagePackObject]
public sealed record OrderExecutionChangedEvent : IEvent<OrderExecutionId>
{
    public const string Actor = "OrderExecutionEvent";
    public const string Verb = "OrderExecutionChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public OrderExecutionId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public OrderExecutionDefinition State { get; init; } = new();
    [Key(9)] public EstablishedTradeDefinition[] CreatedTrades { get; init; } = [];
    [Key(10)] public PositionCloseExecution[] ClosedPositions { get; init; } = [];
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(OrderExecutionChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
