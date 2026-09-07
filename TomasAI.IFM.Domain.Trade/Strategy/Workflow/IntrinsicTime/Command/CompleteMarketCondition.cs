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

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles successful Market Condition completion.</summary>
public static class CompleteMarketCondition
{
    /// <summary>Records the typed Market Condition result and applies its authoritative continuation.</summary>
    public static ServiceResult<GuidResult> Execute(this CompleteMarketConditionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
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
            context.Logger.LogWarning("Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);
            return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (!TryReadContinuation(command, current, out var result, out var validationError))
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
                CausationId = command.SourceEventId, UpdatedAtUtc = now, TerminalAtUtc = now,
                StopReasonCode = MarketConditionReasonCodes.ResultExpired,
                MarketCondition = current.MarketCondition with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut, FailedAtUtc = now,
                    Failure = failure, Result = command.Result, SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            context.Logger.LogWarning("Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.Subject.EntityId, timedOut.WorkflowId, timedOut.WorkflowRevision);
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
                    ContinuationReasonCodes = result.Reasons
                }
            };
            AppendSnapshot(state, command, current.Status, noTrade, now);
            return Ok(command);
        }

        var updated = current with
        {
            Outcome = StrategyWorkflowOutcome.None,
            CausationId = command.CausationId, WorkflowRevision = revision, UpdatedAtUtc = now,
            CurrentStage = StrategyWorkflowStage.TradeSelection,
            MarketCondition = current.MarketCondition with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = StrategyWorkflowContinuationDecision.Proceed,
                CompletedAtUtc = now, FailedAtUtc = null, Result = command.Result, Failure = null,
                SourceEventId = command.SourceEventId, ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.Assessment.v2",
                ContinuationRuleSetVersion = 2, ContinuationReasonCodes = result.Reasons
            },
            TradeSelection = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing, StartedAtUtc = now,
                InputWorkflowRevision = revision, ExpiresAtUtc = current.ExpiresAtUtc
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        return Ok(command);
    }

    static void AppendSnapshot(IntrinsicTimeStrategyWorkflowCommandState state,
        CompleteMarketConditionCommand command, WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view, DateTime now)
        => state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor,
                WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)), EntityId = command.EntityId,
            CommandId = command.CommandId, AggregateId = command.EntityId.Format(), EventSource = command.EventSource,
            ReceivedOn = now, WorkflowId = view.WorkflowId, WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId, CausationId = view.CausationId, PreviousStatus = previousStatus,
            State = view, UpdatedAtUtc = now
        }, command);

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

    static ServiceResult<GuidResult> Ok(CompleteMarketConditionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
