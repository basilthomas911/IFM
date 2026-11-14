using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

/// <summary>Durably admits a signal that is waiting for its workflow entity to become free.</summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategySignalQueuedEvent : IEvent<IntrinsicTimeStrategyWorkflowEntityId>
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public long EventId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public string AggregateId { get; init; } = string.Empty;
    [Key(5)] public string EventSource { get; init; } = string.Empty;
    [Key(6)] public DateTime ReceivedOn { get; init; }
    [Key(7)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(8)] public ExecuteIntrinsicTimeStrategyWorkflowCommand Request { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(IntrinsicTimeStrategySignalQueuedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
