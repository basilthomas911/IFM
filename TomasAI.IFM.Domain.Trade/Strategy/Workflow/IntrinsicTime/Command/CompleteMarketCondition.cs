using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles successful Market Condition completion.</summary>
public static class CompleteMarketCondition
{
    /// <summary>Records the typed Market Condition result and applies its authoritative continuation.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this CompleteMarketConditionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(command, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply CompleteMarketCondition event";
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
    internal static ServiceResult<GuidResult> PrepareWorkflow(this CompleteMarketConditionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        using var activity = MarketConditionTelemetry.Start("market-condition.workflow-continuation");
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.MarketCondition ||
            current.MarketCondition.SourceEventId == command.SourceEventId)
        {
            context.Logger.LogWarning("{Component}.{Method} "+"Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteMarketCondition),nameof(Execute),                command.CommandName,command.Subject.EntityId,current?.WorkflowId,current?.WorkflowRevision);
            return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var initialized = command.AssessmentBinding is null ? current : current with { AssessmentBinding = command.AssessmentBinding };
        if (!TryReadContinuation(command, initialized, out var result, out var validationError))
        {
            var failure = new StrategyPipelineFailure
            {
                ErrorCode = CompleteMarketConditionCommand.ErrorId,
                ErrorMessage = validationError,
                ErrorType = nameof(StrategyWorkflowOutcome.InvalidResult),
                ErrorData = MarketConditionReasonCodes.ContractInvalid,
                FailedAtUtc = now
            };
            var invalid = current with
            {
                Status = WorkflowStrategyMachineStatus.Failed,
                Outcome = StrategyWorkflowOutcome.InvalidResult,
                WorkflowRevision = current.WorkflowRevision + 1,
                CausationId = command.SourceEventId,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = MarketConditionReasonCodes.ContractInvalid,
                MarketCondition = current.MarketCondition with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.Failed,
                    FailedAtUtc = now,
                    Failure = failure,
                    SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, invalid, now);
            return Ok(command);
        }
        if (now >= current.ExpiresAtUtc || now >= result.ValidUntilUtc)
        {
            MarketConditionTelemetry.RecordExpired(result.TargetHorizon);
            var failure = TimeoutFailure(now);
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut,
                Outcome = StrategyWorkflowOutcome.TimedOut,
                WorkflowRevision = current.WorkflowRevision + 1,
                CausationId = command.SourceEventId,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = MarketConditionReasonCodes.ResultExpired,
                MarketCondition = current.MarketCondition with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                    FailedAtUtc = now,
                    Failure = failure,
                    Result = command.Result,
                    SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            context.Logger.LogWarning("{Component}.{Method} "+"Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteMarketCondition),nameof(Execute),                command.Subject.EntityId,timedOut.WorkflowId,timedOut.WorkflowRevision);
            return Ok(command);
        }

        var revision = current.WorkflowRevision + 1;
        if (result.Stop)
        {
            var noTrade = current with
            {
                Status = WorkflowStrategyMachineStatus.Completed,
                Outcome = StrategyWorkflowOutcome.NoTrade,
                CausationId = command.CausationId,
                WorkflowRevision = revision,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = result.PrimaryReasonCode,
                AssessmentBinding = command.AssessmentBinding ?? current.AssessmentBinding,
                MarketCondition = current.MarketCondition with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.Completed,
                    ContinuationDecision = StrategyWorkflowContinuationDecision.Stop,
                    CompletedAtUtc = now,
                    FailedAtUtc = null,
                    Result = command.Result,
                    Failure = null,
                    SourceEventId = command.SourceEventId,
                    ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.Assessment.v2",
                    ContinuationRuleSetVersion = 2,
                    ContinuationReasonCodes = result.Reasons,
                    ParameterSetId = (command.AssessmentBinding ?? current.AssessmentBinding)?.Parameters.ParameterSetId ?? Guid.Empty,
                    ParameterSetVersion = (command.AssessmentBinding ?? current.AssessmentBinding)?.Parameters.Version ?? 0,
                    ParameterPayloadSha256 = (command.AssessmentBinding ?? current.AssessmentBinding)?.PayloadSha256 ?? string.Empty
                }
            };
            AppendSnapshot(state, command, current.Status, noTrade, now);
            return Ok(command);
        }

        var updated = current with
        {
            Status = command.TradeSelectionInitializationFailure is null ? WorkflowStrategyMachineStatus.Started : WorkflowStrategyMachineStatus.Failed,
            Outcome = command.TradeSelectionInitializationFailure is null ? StrategyWorkflowOutcome.None : StrategyWorkflowOutcome.PipelineFailed,
            CausationId = command.CausationId,
            WorkflowRevision = revision,
            UpdatedAtUtc = now,
            CurrentStage = StrategyWorkflowStage.TradeSelection,
            TerminalAtUtc = command.TradeSelectionInitializationFailure is null ? null : now,
            StopReasonCode = command.TradeSelectionInitializationFailure is null ? string.Empty : "TS.INIT.FAILED",
            AssessmentBinding = command.AssessmentBinding ?? current.AssessmentBinding,
            SelectionBinding = command.SelectionBinding ?? current.SelectionBinding,
            FundId = command.FundId > 0 ? command.FundId : current.FundId,
            MarketCondition = current.MarketCondition with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = StrategyWorkflowContinuationDecision.Proceed,
                CompletedAtUtc = now,
                FailedAtUtc = null,
                Result = command.Result,
                Failure = null,
                SourceEventId = command.SourceEventId,
                ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.Assessment.v2",
                ContinuationRuleSetVersion = 2,
                ContinuationReasonCodes = result.Reasons,
                ParameterSetId = (command.AssessmentBinding ?? current.AssessmentBinding)?.Parameters.ParameterSetId ?? Guid.Empty,
                ParameterSetVersion = (command.AssessmentBinding ?? current.AssessmentBinding)?.Parameters.Version ?? 0,
                ParameterPayloadSha256 = (command.AssessmentBinding ?? current.AssessmentBinding)?.PayloadSha256 ?? string.Empty
            },
            TradeSelection = new StrategyWorkflowStageState
            {
                ProcessingStatus = command.TradeSelectionInitializationFailure is null
                    ? StrategyActorProcessingStatus.Processing : StrategyActorProcessingStatus.Failed,
                StartedAtUtc = now,
                FailedAtUtc = command.TradeSelectionInitializationFailure is null ? null : now,
                Failure = command.TradeSelectionInitializationFailure,
                InputWorkflowRevision = revision,
                ExpiresAtUtc = current.ExpiresAtUtc,
                ParameterSetId = (command.SelectionBinding ?? current.SelectionBinding)?.CommonPolicy.Id ?? Guid.Empty,
                ParameterSetVersion = (command.SelectionBinding ?? current.SelectionBinding)?.CommonPolicy.Version ?? 0,
                ParameterPayloadSha256 = (command.SelectionBinding ?? current.SelectionBinding)?.PayloadSha256 ?? string.Empty
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
        CompleteMarketConditionCommand command, WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view, DateTime now)
    {
        var snapshotId = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero));
        if (view.Status == WorkflowStrategyMachineStatus.Started && view.CurrentStage == StrategyWorkflowStage.TradeSelection)
        {
            try { view = view with { SelectionDispatch = TradeSelection.TradeSelectionDispatch.Create(view, snapshotId) }; }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                view = view with
                {
                    Status = WorkflowStrategyMachineStatus.Failed,
                    Outcome = StrategyWorkflowOutcome.PipelineFailed,
                    TerminalAtUtc = now,
                    StopReasonCode = "TS.CONFIG.INVALID",
                    TradeSelection = view.TradeSelection with { ProcessingStatus = StrategyActorProcessingStatus.Failed, FailedAtUtc = now, Failure = new() { ErrorCode = 23023, ErrorType = "SelectionBindingInvalid", ErrorMessage = ex.Message, FailedAtUtc = now } }
                };
            }
        }
        state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = snapshotId,
            EntityId = command.EntityId,
            WorkflowId = view.WorkflowId,
            WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId,
            CausationId = view.CausationId,
            PreviousStatus = previousStatus,
            WorkflowDefinition = view,
            UpdatedAtUtc = now
        }, command);
    }

    /// <summary>Evaluates timeout failure business information.</summary>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static StrategyPipelineFailure TimeoutFailure(DateTime now) => new()
    {
        ErrorCode = MarketConditionAssessmentFailedEvent.ErrorId,
        ErrorMessage = "The Market Condition result or workflow execution deadline was reached.",
        ErrorType = nameof(MarketConditionFailureCategory.Timeout),
        ErrorData = MarketConditionReasonCodes.ResultExpired,
        FailedAtUtc = now
    };

    sealed record Continuation(bool Stop, DateTime? ValidUntilUtc,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType TargetHorizon, string PrimaryReasonCode, string[] Reasons);

    /// <summary>Evaluates try read continuation business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="current">The current business snapshot.</param>
    /// <param name="result">The result business information.</param>
    /// <param name="error">The error business information.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static bool TryReadContinuation(CompleteMarketConditionCommand command, IntrinsicTimeStrategyWorkflowView current,
        out Continuation result, out string error)
    {
        result = new(false, null, default, "", []);
        try
        {
            var r = MarketConditionAssessmentContracts.ReadResult(command.Result);
            MarketConditionAssessmentContracts.ValidateAcceptance(r, current, command.InputWorkflowRevision);
            if (command.SourceEventId != r.ResultId) throw new ArgumentException("Assessment terminal event identity mismatch.");
            var noNewTrade = r.Assessment.InheritedRestrictions.Contains(RegimeRestriction.NoNewTrade);
            var unavailable = r.Assessment.Availability == AssessmentAvailability.Unavailable;
            var reason = noNewTrade ? "MC.ASSESSMENT.INHERITED_NO_NEW_TRADE" : unavailable ? "MC.ASSESSMENT.UNAVAILABLE" : "MC.ASSESSMENT.AVAILABLE";
            result = new(noNewTrade || unavailable, r.Assessment.ValidUntilUtc, r.TargetHorizon, reason, [reason]);
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or MessagePackSerializationException or InvalidOperationException)
        {
            error = "Assessment result conflicts with the accepted workflow invocation: " + ex.GetType().Name;
            return false;
        }
    }

    /// <summary>Evaluates ok business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> Ok(CompleteMarketConditionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this CompleteMarketConditionCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this CompleteMarketConditionCommand command, WorkflowSnapshotTransition workflowTransition)
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
