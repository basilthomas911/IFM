using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Contains all accepted business state needed to restore a workflow without earlier mutations.</summary>
[MessagePackObject]
public sealed record ExitPositionWorkflowSnapshot
{
    [Key(0)] public ExitPositionWorkflowStartedEvent Started { get; init; } = default!;
    [Key(1)] public ExitPositionWorkflowProjection Projection { get; init; } = default!;
    [Key(2)] public Guid CompositionReceiptId { get; init; }
    [Key(3)] public Guid RiskReceiptId { get; init; }
    [Key(4)] public Guid ExecutionAttemptId { get; init; }
    [Key(5)] public Guid BindingCommandId { get; init; }
    [Key(6)] public string FailureCode { get; init; } = string.Empty;
    [Key(7)] public string FailureDetail { get; init; } = string.Empty;
}

/// <summary>A source transition that replaces the complete state of the owning workflow stream.</summary>
public abstract record ExitPositionWorkflowLifecycleEvent : IEvent<ExitPositionWorkflowId>, IRequireDurableProjection
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ExitPositionWorkflowSnapshot Snapshot { get; init; } = default!;
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public string UserName => "TradePlan";
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    [IgnoreMember] public bool RequiresDurableProjection => Snapshot?.Projection.State is ExitPositionWorkflowState.OrderComposed or ExitPositionWorkflowState.RiskAccepted;
    [IgnoreMember] public DurableProjectionRequirement RequiredProjection => ExitPositionWorkflowActorNames.DurableProjection(Snapshot.Projection.StrategyKind);
}

/// <summary>Persists accepted order composition and its exact committed Function receipt.</summary>
[MessagePackObject]
public sealed record ExitPositionWorkflowOrderComposedEvent : ExitPositionWorkflowLifecycleEvent;

/// <summary>Persists the accepted Portfolio risk decision, including a terminal no-order result.</summary>
[MessagePackObject]
public sealed record ExitPositionWorkflowRiskRecordedEvent : ExitPositionWorkflowLifecycleEvent;

/// <summary>Persists successful execution binding for the accepted exit order.</summary>
[MessagePackObject]
public sealed record ExitPositionWorkflowCompletedEvent : ExitPositionWorkflowLifecycleEvent;

/// <summary>Persists a terminal exit workflow rejection with its accepted history.</summary>
[MessagePackObject]
public sealed record ExitPositionWorkflowTerminatedEvent : ExitPositionWorkflowLifecycleEvent;
