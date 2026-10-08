using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
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

/// <summary>Handles successful Trade Selection completion.</summary>
public static class CompleteTradeSelection
{
    /// <summary>Records the Trade Selection result and selects Order Composition.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this CompleteTradeSelectionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(command, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply CompleteTradeSelection event";
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
    internal static ServiceResult<GuidResult> PrepareWorkflow(this CompleteTradeSelectionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.TradeSelection ||
            current.TradeSelection.SourceEventId == command.SourceEventId)
        {
            context.Logger.LogWarning("{Component}.{Method} "+"Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteTradeSelection),nameof(Execute),                command.CommandName,command.Subject.EntityId,current?.WorkflowId,current?.WorkflowRevision);
            return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc)
        {
            var failure = TimeoutFailure(now);
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut,
                Outcome = StrategyWorkflowOutcome.TimedOut,
                WorkflowRevision = current.WorkflowRevision + 1,
                CausationId = command.SourceEventId,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = "WorkflowExecutionExpired",
                TradeSelection = current.TradeSelection with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                    FailedAtUtc = now,
                    Failure = failure,
                    SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            context.Logger.LogWarning("{Component}.{Method} "+"Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteTradeSelection),nameof(Execute),                command.Subject.EntityId,timedOut.WorkflowId,timedOut.WorkflowRevision);
            return Ok(command);
        }
        TradeSelectionResult result;
        try
        {
            result = TradeSelectionContracts.ReadResult(command.Result);
            var dispatch = current.SelectionDispatch ?? throw new ArgumentException("Missing durable selector dispatch.");
            var expected = TradeSelectionEvaluator.Evaluate(dispatch);
            TradeSelectionContracts.Require(command.SourceEventId == result.ResultId && result.InputWorkflowRevision == command.InputWorkflowRevision
                && TradeSelectionContracts.EvidenceHash(expected) == TradeSelectionContracts.EvidenceHash(result), "TS.RESULT.INVALID", "Selector result differs from the deterministic decision over the saved request.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            var invalid = current with
            {
                Status = WorkflowStrategyMachineStatus.Failed,
                Outcome = StrategyWorkflowOutcome.PipelineFailed,
                WorkflowRevision = current.WorkflowRevision + 1,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = "TS.RESULT.INVALID",
                TradeSelection = current.TradeSelection with { ProcessingStatus = StrategyActorProcessingStatus.Failed, FailedAtUtc = now, SourceEventId = command.SourceEventId, Failure = new() { ErrorCode = 23023, ErrorType = "SelectionResultInvalid", ErrorMessage = ex.Message, FailedAtUtc = now } }
            };
            AppendSnapshot(state, command, current.Status, invalid, now); return Ok(command);
        }
        if (now >= result.ValidUntilUtc)
        {
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut,
                Outcome = StrategyWorkflowOutcome.TimedOut,
                WorkflowRevision = current.WorkflowRevision + 1,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                CausationId = command.SourceEventId,
                StopReasonCode = "TS.TIME.EXPIRED",
                TradeSelection = current.TradeSelection with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                    FailedAtUtc = now,
                    Failure = TimeoutFailure(now),
                    SourceEventId = command.SourceEventId,
                    Result = null
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            return Ok(command);
        }
        var noTrade = result.Outcome == SelectionOutcome.NoTrade;
        var neutral = current.SelectionBinding?.SchemaVersion == 2;
        var revision = current.WorkflowRevision + 1;
        var updated = current with
        {
            CausationId = command.CausationId,
            WorkflowRevision = revision,
            UpdatedAtUtc = now,
            CurrentStage = noTrade || !neutral ? StrategyWorkflowStage.TradeSelection : StrategyWorkflowStage.OrderComposition,
            Status = noTrade ? WorkflowStrategyMachineStatus.Completed : WorkflowStrategyMachineStatus.Started,
            Outcome = noTrade ? StrategyWorkflowOutcome.NoTrade : StrategyWorkflowOutcome.None,
            TerminalAtUtc = noTrade ? now : null,
            StopReasonCode = noTrade ? result.PrimaryReasonCode : string.Empty,
            TradeSelection = current.TradeSelection with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = noTrade ? StrategyWorkflowContinuationDecision.Stop : StrategyWorkflowContinuationDecision.Proceed,
                CompletedAtUtc = now,
                FailedAtUtc = null,
                Result = command.Result,
                Failure = null,
                SourceEventId = command.SourceEventId,
                ContinuationRuleSetId = "ts-rank-v1",
                ContinuationRuleSetVersion = 1,
                ContinuationReasonCodes = [result.PrimaryReasonCode]
            },
            CompositionHandoff = noTrade || neutral ? null
                : TradeSelectionHandoff.Pending(result, command.Result, revision, command.SourceEventId, now),
            OrderComposition = noTrade || !neutral ? current.OrderComposition : new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = now,
                InputWorkflowRevision = revision,
                ExpiresAtUtc = new[] { current.ExpiresAtUtc, result.ValidUntilUtc }.Min()
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        return Ok(command);
    }

    /// <summary>Evaluates append snapshot business information.</summary>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="previousStatus">The lifecycle status before this transition.</param>
    /// <param name="view">The immutable workflow snapshot.</param>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    static void AppendSnapshot(WorkflowSnapshotPreparation state,
        CompleteTradeSelectionCommand command, WorkflowStrategyMachineStatus previousStatus,
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
        ErrorType = "TradeSelectionTimedOut",
        FailedAtUtc = now
    };

    /// <summary>Evaluates ok business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> Ok(CompleteTradeSelectionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this CompleteTradeSelectionCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this CompleteTradeSelectionCommand command, WorkflowSnapshotTransition workflowTransition)
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
