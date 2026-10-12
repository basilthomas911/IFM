using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
/// <summary>Local preparation workspace: reads one defensive snapshot and collects proposals without actor-state access.</summary>
internal sealed class WorkflowSnapshotPreparation(IntrinsicTimeStrategyWorkflowView? workflowDefinition)
{
    readonly List<WorkflowSnapshotChange> workflowChanges = [];
    public IntrinsicTimeStrategyWorkflowView? CurrentView { get; } = workflowDefinition;

    public IReadOnlyList<WorkflowSnapshotChange> Freeze() => workflowChanges.ToArray();
    public bool Record(WorkflowSnapshotChange workflowChange, ICommand command)
    {
        workflowChanges.Add(workflowChange);
        return true;
    }
}
