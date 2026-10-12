using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
/// <summary>Immutable proposed workflow snapshot and source-event metadata.</summary>
internal sealed record WorkflowSnapshotChange
{
    public Guid SnapshotEventId { get; init; }
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; } = new();
    public StrategyWorkflowId WorkflowId { get; init; }
    public long WorkflowRevision { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public WorkflowStrategyMachineStatus PreviousStatus { get; init; }
    public IntrinsicTimeStrategyWorkflowView WorkflowDefinition { get; init; } = default!;
    public DateTime UpdatedAtUtc { get; init; }
}
