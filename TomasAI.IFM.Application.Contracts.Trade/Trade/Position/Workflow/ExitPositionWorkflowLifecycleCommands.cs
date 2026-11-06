using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Identifies one transition of the owning exit workflow and its exact predecessor.</summary>
public abstract record ExitPositionWorkflowLifecycleCommand : ICommand<ExitPositionWorkflowId>, ICommandRetryIdentity
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(3)] public long ExpectedRevision { get; init; }
    [Key(4)] public Guid PredecessorEventId { get; init; }
    [IgnoreMember] public abstract string CommandName { get; }
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;
    [IgnoreMember] public int ErrorCode => 27203;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    ICommand ICommandRetryIdentity.ForRetryIdentity() => this;
}

/// <summary>Records the exact committed order-composition Function receipt on the workflow.</summary>
[MessagePackObject]
public sealed record RecordExitOrderCompositionCommand : ExitPositionWorkflowLifecycleCommand
{
    public const string Verb = "RecordExitOrderComposition";
    [Key(5)] public ExitOrderCompositionCompletedEvent Receipt { get; init; } = default!;
    [IgnoreMember] public override string CommandName => nameof(RecordExitOrderCompositionCommand);
}

/// <summary>Records the exact committed Portfolio exit risk Function receipt on the workflow.</summary>
[MessagePackObject]
public sealed record RecordExitRiskDecisionCommand : ExitPositionWorkflowLifecycleCommand
{
    public const string Verb = "RecordExitRiskDecision";
    [Key(5)] public PositionExitRiskCompletedEvent Receipt { get; init; } = default!;
    [IgnoreMember] public override string CommandName => nameof(RecordExitRiskDecisionCommand);
}

/// <summary>Records successful handoff to the Trade Order actor after execution binding.</summary>
[MessagePackObject]
public sealed record CompleteExitPositionWorkflowCommand : ExitPositionWorkflowLifecycleCommand
{
    public const string Verb = "CompleteExitPositionWorkflow";
    [Key(5)] public Guid ExecutionAttemptId { get; init; }
    [Key(6)] public Guid BindingCommandId { get; init; }
    [IgnoreMember] public override string CommandName => nameof(CompleteExitPositionWorkflowCommand);
}

/// <summary>Records a terminal workflow rejection tied to its exact accepted predecessor.</summary>
[MessagePackObject]
public sealed record FailExitPositionWorkflowCommand : ExitPositionWorkflowLifecycleCommand
{
    public const string Verb = "FailExitPositionWorkflow";
    [Key(5)] public string ReasonCode { get; init; } = string.Empty;
    [Key(6)] public string Detail { get; init; } = string.Empty;
    [IgnoreMember] public override string CommandName => nameof(FailExitPositionWorkflowCommand);
}
