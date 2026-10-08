using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles a Trade Selection timeout.</summary>
public static class TimeoutTradeSelection
{
    /// <summary>Marks Trade Selection and the workflow as timed out.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this TimeoutTradeSelectionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(command, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply TimeoutTradeSelection event";
        var updated = command.Compute(preparation.Freeze(), out var workflowTransition) switch
        {
            _ when workflowTransition.RejectionReason is not null => command.UpdateFailed(ref errorMsg, workflowTransition.RejectionReason),
            _ => state.Update(command.CreateWorkflowLifecycleEvents(workflowTransition), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }
    /// <summary>Prepares immutable workflow changes while preserving deadline, stale-result, and financial-read ordering.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    internal static ServiceResult<GuidResult> PrepareWorkflow(this TimeoutTradeSelectionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.ExpectedWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.TradeSelection)
        {
            LogStale(context, command, current); return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var failure = TimeoutFailure(now);
        var updated = current with
        {
            Status = WorkflowStrategyMachineStatus.TimedOut,
            WorkflowRevision = current.WorkflowRevision + 1,
            CausationId = command.TimeoutId,
            UpdatedAtUtc = now,
            TerminalAtUtc = now,
            StopReasonCode = "PipelineTimedOut",
            TradeSelection = current.TradeSelection with
            {
                ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                FailedAtUtc = now,
                Failure = failure,
                SourceEventId = command.TimeoutId
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        context.Logger.LogWarning("{Component}.{Method} "+"Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(TimeoutTradeSelection),nameof(Execute),            command.Subject.EntityId,updated.WorkflowId,updated.WorkflowRevision);
        return Ok(command);
    }

    /// <summary>Evaluates append snapshot business information.</summary>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="previousStatus">The lifecycle status before this transition.</param>
    /// <param name="view">The immutable workflow snapshot.</param>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    static void AppendSnapshot(WorkflowSnapshotPreparation state,
        TimeoutTradeSelectionCommand command, WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view, DateTime now)
        => state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
            EntityId = command.EntityId,
            WorkflowId = view.WorkflowId,
            WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId,
            CausationId = view.CausationId,
            PreviousStatus = previousStatus,
            WorkflowDefinition = view,
            UpdatedAtUtc = now
        }, command);

    /// <summary>Evaluates timeout failure business information.</summary>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static StrategyPipelineFailure TimeoutFailure(DateTime now) => new()
    {
        ErrorCode = 23103,
        ErrorMessage = "The fixed workflow execution deadline was reached.",
        ErrorType = "RegimeDiscoveryTimedOut",
        FailedAtUtc = now
    };

    /// <summary>Evaluates log stale business information.</summary>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="current">The current business snapshot.</param>
    static void LogStale(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        TimeoutTradeSelectionCommand command, IntrinsicTimeStrategyWorkflowView? current)
        => context.Logger.LogWarning("{Component}.{Method} "+"Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(TimeoutTradeSelection),nameof(LogStale),            command.CommandName,command.Subject.EntityId,current?.WorkflowId,current?.WorkflowRevision);

    /// <summary>Evaluates ok business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> Ok(TimeoutTradeSelectionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this TimeoutTradeSelectionCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this TimeoutTradeSelectionCommand command, WorkflowSnapshotTransition workflowTransition)
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
