using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position;

[MessagePackObject]
[Union(0, typeof(IronCondorPositionChangedEvent))]
[Union(1, typeof(VerticalSpreadPositionChangedEvent))]
[Union(2, typeof(FuturesPositionChangedEvent))]
public abstract record PositionChangedEvent : IEvent<StrategyPositionId>
{
    public const string Verb = "StrategyPositionChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public StrategyPositionId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyPositionSnapshot State { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

[MessagePackObject]
[Union(0, typeof(StrategyPositionOpenedEvent))]
[Union(1, typeof(StrategyPositionClosedEvent))]
[Union(2, typeof(StrategyPositionCorrectedEvent))]
public abstract record PositionBoundaryEvent : IEvent<StrategyPositionId>
{
    public const string Actor = "PortfolioEvent";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public StrategyPositionId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyPositionSnapshot Position { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
