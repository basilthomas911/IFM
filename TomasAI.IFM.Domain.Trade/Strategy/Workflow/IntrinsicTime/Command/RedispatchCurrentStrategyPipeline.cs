using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
/// <summary>Republishes the durable current intent. Recovery never changes an execution identity, input revision or deadline.</summary>
public static class RedispatchCurrentStrategyPipeline
{
    /// <summary>Prepares the workflow decision, validates its immutable changes, and applies the source events.</summary>
    /// <param name="c">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this RedispatchCurrentStrategyPipelineCommand c, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(c, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply RedispatchCurrentStrategyPipeline event";
        var updated = c.Compute(preparation.Freeze(), out var workflowTransition) switch
        {
            _ when workflowTransition.RejectionReason is not null => c.UpdateFailed(ref errorMsg, workflowTransition.RejectionReason),
            _ => state.Update(c.CreateWorkflowLifecycleEvents(workflowTransition), c)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(c.CommandId)) : c.UpdateFailed(errorMsg);
    }
    /// <summary>Prepares immutable workflow changes while preserving deadline, stale-result, and financial-read ordering.</summary>
    /// <param name="c">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    internal static ServiceResult<GuidResult> PrepareWorkflow(this RedispatchCurrentStrategyPipelineCommand c, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, WorkflowSnapshotPreparation state)
    {
        var view = state.CurrentView;
        if (view is null || view.WorkflowId != c.WorkflowId || view.WorkflowRevision != c.ExpectedWorkflowRevision || view.CurrentStage != c.ExpectedStage)
            return new ServiceOk<GuidResult>(new(c.CommandId));
        if (view.Status != WorkflowStrategyMachineStatus.Started && view.CompositionHandoff is null)
            return new ServiceOk<GuidResult>(new(c.CommandId));
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now)),
            EntityId = c.EntityId,
            WorkflowId = view.WorkflowId,
            WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId,
            CausationId = c.CommandId,
            PreviousStatus = view.Status,
            WorkflowDefinition = view,
            UpdatedAtUtc = now
        }, c);
        return new ServiceOk<GuidResult>(new(c.CommandId));
    }
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this RedispatchCurrentStrategyPipelineCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
    {
        workflowTransition = workflowChanges.Any(change => change.WorkflowDefinition is null || !Equals(change.EntityId, command.EntityId))
            ? new([], "IntrinsicTimeStrategyWorkflow: the computed workflow snapshot is missing or belongs to another entity.")
            : new(workflowChanges);
        return workflowTransition.RejectionReason is null;
    }
    /// <summary>Creates each ordered source event with the originating command identity.</summary>
    /// <param name="command">The originating workflow command.</param>
    /// <param name="workflowTransition">The accepted workflow snapshots.</param>
    /// <returns>The source events ready for state application.</returns>
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this RedispatchCurrentStrategyPipelineCommand command, WorkflowSnapshotTransition workflowTransition)
        => workflowTransition.WorkflowChanges.Select(change => new WorkflowStrategyStateUpdatedEvent
        {
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = change.SnapshotEventId,
            EntityId = command.EntityId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = change.UpdatedAtUtc,
            WorkflowId = change.WorkflowId,
            WorkflowRevision = change.WorkflowRevision,
            CorrelationId = change.CorrelationId,
            CausationId = change.CausationId,
            PreviousStatus = change.PreviousStatus,
            WorkflowDefinition = change.WorkflowDefinition,
            UpdatedAtUtc = change.UpdatedAtUtc
        }).ToArray();
}
