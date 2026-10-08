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

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles successful Risk Management completion.</summary>
public static class CompleteRiskManagement
{
    /// <summary>Accepts a verified sizing proposal. Approval still awaits Portfolio reservation and execution ownership.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this CompleteRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(command, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply CompleteRiskManagement event";
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
    internal static ServiceResult<GuidResult> PrepareWorkflow(this CompleteRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.RiskManagement ||
            current.RiskManagement.SourceEventId == command.SourceEventId)
        {
            context.Logger.LogWarning("{Component}.{Method} "+"Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteRiskManagement),nameof(Execute),                command.CommandName,command.Subject.EntityId,current?.WorkflowId,current?.WorkflowRevision);
            return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc || current.RiskExecution is { } execution && now >= execution.ExpiresAtUtc)
        {
            var failure = TimeoutFailure(now);
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut,
                WorkflowRevision = current.WorkflowRevision + 1,
                Outcome = StrategyWorkflowOutcome.TimedOut,
                CausationId = command.SourceEventId,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = now >= current.ExpiresAtUtc ? "WorkflowExecutionExpired" : "RM.TIME.EXPIRED",
                RiskManagement = current.RiskManagement with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                    FailedAtUtc = now,
                    Failure = failure,
                    SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            context.Logger.LogWarning("{Component}.{Method} "+"Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteRiskManagement),nameof(Execute),                command.Subject.EntityId,timedOut.WorkflowId,timedOut.WorkflowRevision);
            return Ok(command);
        }
        if (command.PortfolioDecision is { } portfolioDecision)
            return CompletePortfolioDecision(command, context, state, current, portfolioDecision, now);
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
                WorkflowRevision = current.WorkflowRevision + 1,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                CausationId = command.SourceEventId,
                StopReasonCode = expired ? "RM.TIME.EXPIRED" : "RM.RESULT.INVALID",
                RiskManagement = current.RiskManagement with
                {
                    ProcessingStatus = expired ? StrategyActorProcessingStatus.TimedOut : StrategyActorProcessingStatus.Failed,
                    SourceEventId = command.SourceEventId,
                    FailedAtUtc = now,
                    Failure = new()
                    {
                        ErrorCode = 23025,
                        ErrorType = "RiskManagementResultInvalid",
                        ErrorMessage = "Risk result failed immutable-input verification.",
                        FailedAtUtc = now
                    }
                }
            };
            AppendSnapshot(state, command, current.Status, invalid, now);
            return Ok(command);
        }
        var rejected = result.Outcome == RiskAssessmentOutcome.Rejected;
        var updated = current with
        {
            RiskExplanation = RiskExplanationModel.Create(current.RiskExecution!, result),
            Status = rejected ? WorkflowStrategyMachineStatus.Completed : WorkflowStrategyMachineStatus.Started,
            Outcome = rejected ? StrategyWorkflowOutcome.NoTrade : StrategyWorkflowOutcome.None,
            CausationId = command.CausationId,
            WorkflowRevision = current.WorkflowRevision + 1,
            UpdatedAtUtc = now,
            TerminalAtUtc = rejected ? now : null,
            StopReasonCode = rejected ? result.Reasons[0] : string.Empty,
            RiskManagement = current.RiskManagement with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = rejected ? StrategyWorkflowContinuationDecision.Stop : StrategyWorkflowContinuationDecision.None,
                CompletedAtUtc = now,
                FailedAtUtc = null,
                Result = command.Result,
                Failure = null,
                SourceEventId = command.SourceEventId,
                ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.v1",
                ContinuationRuleSetVersion = 1,
                ContinuationReasonCodes = []
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
        CompleteRiskManagementCommand command, WorkflowStrategyMachineStatus previousStatus,
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
        ErrorType = "RiskManagementTimedOut",
        FailedAtUtc = now
    };

    /// <summary>Evaluates ok business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> Ok(CompleteRiskManagementCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));

    /// <summary>Evaluates complete portfolio decision business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <param name="current">The current business snapshot.</param>
    /// <param name="decision">The decision business information.</param>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> CompletePortfolioDecision(
        CompleteRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state,
        IntrinsicTimeStrategyWorkflowView current,
        PortfolioRiskDecision decision,
        DateTime now)
    {
        PortfolioOrderCompositionMapper.ValidateDecision(current, decision);
        var noTrade = decision.Status == PortfolioRiskDecisionStatus.NoTradeOrders;
        var updated = current with
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = noTrade ? StrategyWorkflowOutcome.NoTrade : StrategyWorkflowOutcome.Completed,
            WorkflowRevision = checked(current.WorkflowRevision + 1),
            CausationId = command.CausationId,
            UpdatedAtUtc = now,
            TerminalAtUtc = now,
            StopReasonCode = noTrade ? "RM.PORTFOLIO.NO_TRADE_ORDERS" : string.Empty,
            PortfolioRiskDecision = decision,
            RiskManagement = current.RiskManagement with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = StrategyWorkflowContinuationDecision.Stop,
                CompletedAtUtc = now,
                FailedAtUtc = null,
                Failure = null,
                SourceEventId = command.SourceEventId,
                ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.v2",
                ContinuationRuleSetVersion = 2,
                ContinuationReasonCodes = noTrade ? ["RM.PORTFOLIO.NO_TRADE_ORDERS"] : ["RM.PORTFOLIO.TRADE_ORDERS_DISPATCHED"]
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        context.Logger.LogInformation(
            "{Component}.{Method} "+"Portfolio completed workflow {WorkflowId} with {Status}, {OrderCount} Trade Orders and financial revision {FinancialRevision}",nameof(CompleteRiskManagement),nameof(CompletePortfolioDecision),            current.WorkflowId,decision.Status,decision.TradeOrders.Length,decision.FinancialRevision);
        return Ok(command);
    }
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this CompleteRiskManagementCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this CompleteRiskManagementCommand command, WorkflowSnapshotTransition workflowTransition)
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
