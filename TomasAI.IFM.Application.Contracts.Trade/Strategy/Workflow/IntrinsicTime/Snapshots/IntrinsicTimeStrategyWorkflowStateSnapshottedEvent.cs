using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Snapshots;

/// <summary>Captures the full command state at a committed stream boundary.</summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategyWorkflowStateSnapshottedEvent : IEvent
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public WorkflowStrategyStateUpdatedEvent? Latest { get; init; }
    [Key(8)] public long AppliedEventCount { get; init; }
    [Key(9)] public StrategyWorkflowStartDecision LastStartDecision { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(IntrinsicTimeStrategyWorkflowStateSnapshottedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
