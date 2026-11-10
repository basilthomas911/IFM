using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Complete initial or subsequent handoff state with durable post-commit delivery.</summary>
[MessagePackObject]
public sealed record StrategyExecutionHandoffStateSnapshottedEvent : IEvent<OrderExecutionId>, IRequireDurableProjection
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public OrderExecutionId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public StrategyExecutionHandoffDefinition Definition { get; init; } = new();
    [IgnoreMember] public bool RequiresDurableProjection => true;
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => new("StrategyExecutionHandoffCommandActor", "StrategyExecutionHandoffEventProjector", EventProjectorStageType.ApplyProjection);
    [IgnoreMember] public string EventName => nameof(StrategyExecutionHandoffStateSnapshottedEvent);
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
