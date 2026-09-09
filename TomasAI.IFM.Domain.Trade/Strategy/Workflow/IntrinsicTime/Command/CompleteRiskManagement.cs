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
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles successful Risk Management completion.</summary>
public static class CompleteRiskManagement
{
    /// <summary>Accepts a verified sizing proposal. Approval still awaits Portfolio reservation and execution ownership.</summary>
    public static ServiceResult<GuidResult> Execute(this CompleteRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.RiskManagement ||
            current.RiskManagement.SourceEventId == command.SourceEventId)
        {
            context.Logger.LogWarning("Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);
            return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc || current.RiskExecution is { } execution && now >= execution.ExpiresAtUtc)
        {
            var failure = TimeoutFailure(now);
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut, WorkflowRevision = current.WorkflowRevision + 1,
                Outcome = StrategyWorkflowOutcome.TimedOut,
                CausationId = command.SourceEventId, UpdatedAtUtc = now, TerminalAtUtc = now,
                StopReasonCode = now >= current.ExpiresAtUtc ? "WorkflowExecutionExpired" : "RM.TIME.EXPIRED",
                RiskManagement = current.RiskManagement with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut, FailedAtUtc = now,
                    Failure = failure, SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            context.Logger.LogWarning("Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.Subject.EntityId, timedOut.WorkflowId, timedOut.WorkflowRevision);
            return Ok(command);
        }
        RiskAssessmentResult result;
        try
        {
            var accepted = current.RiskExecution ?? throw new RiskCalculationException("RM.RESULT.LEGACY_READ_ONLY");
            using var deadline = new CancellationTokenSource(accepted.ExpiresAtUtc - now);
            result = RiskAcceptance.Validate(accepted, command, new RiskEvaluator(), now, deadline.Token);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OperationCanceledException)
        {
            var expired = ex is OperationCanceledException || ex is RiskCalculationException { ReasonCode: "RM.TIME.EXPIRED" };
            var invalid = current with
            {
                Status = expired ? WorkflowStrategyMachineStatus.TimedOut : WorkflowStrategyMachineStatus.Failed,
                Outcome = expired ? StrategyWorkflowOutcome.TimedOut : StrategyWorkflowOutcome.InvalidResult,
                WorkflowRevision = current.WorkflowRevision + 1, UpdatedAtUtc = now, TerminalAtUtc = now,
                CausationId = command.SourceEventId, StopReasonCode = expired ? "RM.TIME.EXPIRED" : "RM.RESULT.INVALID",
                RiskManagement = current.RiskManagement with
                {
                    ProcessingStatus = expired ? StrategyActorProcessingStatus.TimedOut : StrategyActorProcessingStatus.Failed,
                    SourceEventId = command.SourceEventId, FailedAtUtc = now,
                    Failure = new() { ErrorCode = 23025, ErrorType = "RiskManagementResultInvalid",
                        ErrorMessage = "Risk result failed immutable-input verification.", FailedAtUtc = now }
                }
            };
            AppendSnapshot(state, command, current.Status, invalid, now);
            return Ok(command);
        }
        var rejected = result.Outcome == RiskAssessmentOutcome.Rejected;
        var updated = current with
        {
            Status = rejected ? WorkflowStrategyMachineStatus.Completed : WorkflowStrategyMachineStatus.Started,
            Outcome = rejected ? StrategyWorkflowOutcome.NoTrade : StrategyWorkflowOutcome.None,
            CausationId = command.CausationId, WorkflowRevision = current.WorkflowRevision + 1,
            UpdatedAtUtc = now, TerminalAtUtc = rejected ? now : null,
            StopReasonCode = rejected ? result.Reasons[0] : string.Empty,
            RiskManagement = current.RiskManagement with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = rejected ? StrategyWorkflowContinuationDecision.Stop : StrategyWorkflowContinuationDecision.None,
                CompletedAtUtc = now, FailedAtUtc = null, Result = command.Result, Failure = null,
                SourceEventId = command.SourceEventId, ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.v1",
                ContinuationRuleSetVersion = 1, ContinuationReasonCodes = []
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        return Ok(command);
    }

    static void AppendSnapshot(IntrinsicTimeStrategyWorkflowCommandState state,
        CompleteRiskManagementCommand command, WorkflowStrategyMachineStatus previousStatus,
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
        ErrorCode = 23103, ErrorMessage = "The fixed workflow execution deadline was reached.",
        ErrorType = "RiskManagementTimedOut", FailedAtUtc = now
    };

    static ServiceResult<GuidResult> Ok(CompleteRiskManagementCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
