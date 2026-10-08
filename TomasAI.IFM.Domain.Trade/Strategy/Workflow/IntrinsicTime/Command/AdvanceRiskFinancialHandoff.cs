using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Validation;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Verifies authoritative receipts and persists the next exact request. It never submits financial mutations.</summary>
public static class AdvanceRiskFinancialHandoff
{
    /// <summary>Evaluates validate risk financial handoff business information.</summary>
    /// <param name="errors">The errors business information.</param>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    public static List<ValidationError> ValidateRiskFinancialHandoff(this List<ValidationError> errors, AdvanceRiskFinancialHandoffCommand command)
        => errors.ValidateCommandId(command.CommandId, command.CommandName)
            .ValidateWorkflowEntityId(command.EntityId).ValidateWorkflowCommand(command);

    /// <summary>Evaluates resume after audit async business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="token">Cancels preflight processing.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    public static ValueTask<bool> ResumeAfterAuditAsync(this AdvanceRiskFinancialHandoffCommand command, CancellationToken token)
    { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(true); }

    /// <summary>Prepares the workflow decision, validates its immutable changes, and applies the source events.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this AdvanceRiskFinancialHandoffCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = await PrepareWorkflowAsync(command, context, preparation).ConfigureAwait(false);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply AdvanceRiskFinancialHandoff event";
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
    internal static async ValueTask<ServiceResult<GuidResult>> PrepareWorkflowAsync(this AdvanceRiskFinancialHandoffCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, WorkflowSnapshotPreparation state)
    {
        using var timing_authorization_verify_current_view = WorkflowTrace.Start("authorization.verify.current_view", null);
        var view = state.CurrentView;
        timing_authorization_verify_current_view?.Stop();
        using var trace = WorkflowTrace.Start("risk.verify_handoff", view);
        if (view is not { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.RiskManagement }
            || view.WorkflowId != command.WorkflowId || view.WorkflowRevision != command.InputWorkflowRevision
            || view.RiskManagement.ProcessingStatus != StrategyActorProcessingStatus.Completed
            || (view.FinancialHandoff?.Phase ?? RiskFinancialHandoffPhase.None) != command.ExpectedPhase)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        using var timing_authorization_verify_build_identity = WorkflowTrace.Start("authorization.verify.build_identity", view);
        var expected = RiskFinancialHandoff.Advance(view);
        timing_authorization_verify_build_identity?.Stop();
        RiskUnitModel.Require(command.CommandId == expected.CommandId, "RM.HANDOFF.IDENTITY");
        var risk = view.RiskManagement.Result?.RiskResult ?? throw new RiskCalculationException("RM.HANDOFF.NO_RESULT");
        RiskUnitModel.Require(risk.Outcome == RiskAssessmentOutcome.Approved && view.RiskExecution is not null, "RM.HANDOFF.NOT_APPROVED");
        var owner = (IIntrinsicTimeStrategyWorkflowCommandContext)context;
        var api = owner.FinancialApi;
        var scope = new FinancialReadScope
        {
            PortfolioId = risk.PortfolioId,
            FundId = risk.FundId,
            Access = new("IntrinsicTimeStrategyWorkflow", ["LedgerRead"], [risk.PortfolioId])
        };
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var eventId = Guid.CreateVersion7(new DateTimeOffset(now));
        var handoff = view.FinancialHandoff;
        IntrinsicTimeStrategyWorkflowView? resized = null;
        switch (command.ExpectedPhase)
        {
            case RiskFinancialHandoffPhase.None:
                {
                    using var timing_authorization_verify_order_read = WorkflowTrace.Start("authorization.verify.order_read", view);
                    var order = await owner.PortfolioQueries.GetOrderAsync(checked((int)risk.OrderId)).ConfigureAwait(false);
                    timing_authorization_verify_order_read?.Stop();
                    RiskUnitModel.Require(order.Success && order.Value is { Status: "RiskPending" } && order.Value.PortfolioId == risk.PortfolioId &&
                        order.Value.FundId == risk.FundId && order.Value.WorkflowId == risk.WorkflowId.Value && order.Value.CompositionResultHash == risk.CompositionResultHash,
                        "RM.HANDOFF.FUND_NOT_READY");
                    using var timing_authorization_verify_decode_candidate = WorkflowTrace.Start("authorization.verify.decode_candidate", view);
                    var candidate = view.OrderComposition.Result!.ReadCompositionResult().Candidate!;
                    timing_authorization_verify_decode_candidate?.Stop();
                    using var timing_authorization_verify_admission_read = WorkflowTrace.Start("authorization.verify.admission_read", view);
                    var financial = await api.GetFinancialAdmissionSnapshotAsync(scope, new(risk.Authority.DeploymentKey,
                        FinancialScopeKeys.Underlying(candidate.Product.Symbol, candidate.Product.Exchange, candidate.Product.Currency))).ConfigureAwait(false);
                    timing_authorization_verify_admission_read?.Stop();
                    RiskUnitModel.Require(financial.Success && financial.Value is not null, "RM.HANDOFF.AUTHORITY_UNAVAILABLE");
                    now = context.TimeProvider.GetUtcNow().UtcDateTime;
                    if (RiskResizing.Changed(view.RiskExecution!, RiskResizing.Authority(view.RiskExecution!, financial.Value!, now)))
                    {
                        resized = RiskResizing.Next(view, financial.Value!, now);
                        break;
                    }
                    handoff = new()
                    {
                        Phase = RiskFinancialHandoffPhase.ReservePending,
                        ReservationRequest = RiskFinancialHandoff.Reserve(view, risk, financial.Value!, now),
                        FundCommandId = RiskFinancialHandoff.Identity(risk.InvocationId, "Fund"),
                        FundOrderVersion = order.Value!.AggregateVersion
                    };
                    break;
                }
            case RiskFinancialHandoffPhase.ReservePending:
                {
                    using var timing_authorization_verify_reservation_receipt = WorkflowTrace.Start("authorization.verify.reservation_receipt", view);
                    var read = await api.GetPostingReceiptAsync(scope, new(handoff!.ReservationRequest.OperationId)).ConfigureAwait(false);
                    timing_authorization_verify_reservation_receipt?.Stop();
                    var grant = read.Value?.Value?.Reservation;
                    if (read.Success && read.Value is not null && RiskResizing.IsFencedOut(handoff.ReservationRequest, read.Value))
                    {
                        var financial = await api.GetFinancialAdmissionSnapshotAsync(scope,
                            new(risk.Authority.DeploymentKey, view.RiskExecution!.SizingAuthority.UnderlyingId)).ConfigureAwait(false);
                        RiskUnitModel.Require(financial.Success && financial.Value is not null, "RM.HANDOFF.AUTHORITY_UNAVAILABLE");
                        now = context.TimeProvider.GetUtcNow().UtcDateTime;
                        resized = RiskResizing.Next(view, financial.Value!, now, read.Value);
                        break;
                    }
                    RiskUnitModel.Require(read.Success && read.Value?.Status == FinancialReadStatus.Found && grant is not null, "RM.HANDOFF.GRANT_UNAVAILABLE");
                    handoff = handoff with
                    {
                        Phase = RiskFinancialHandoffPhase.FundPending,
                        Reservation = grant,
                        Authorization = RiskFinancialHandoff.Authorize(handoff.ReservationRequest, grant!)
                    };
                    break;
                }
            case RiskFinancialHandoffPhase.FundPending:
                {
                    using var timing_authorization_verify_fund_receipt = WorkflowTrace.Start("authorization.verify.fund_receipt", view);
                    var read = await api.GetFundRiskAuthorizationAsync(scope, new(handoff!.FundCommandId)).ConfigureAwait(false);
                    timing_authorization_verify_fund_receipt?.Stop();
                    var accepted = read.Value?.Value;
                    now = context.TimeProvider.GetUtcNow().UtcDateTime;
                    RiskUnitModel.Require(read.Success && read.Value?.Status == FinancialReadStatus.Found && accepted is not null &&
                        accepted.CommandId == handoff.FundCommandId && accepted.EventId != Guid.Empty && accepted.Authorization == handoff.Authorization &&
                        now < handoff.Authorization!.ValidUntilUtc, "RM.HANDOFF.FUND_AUTHORIZATION");
                    using var timing_authorization_verify_build_order = WorkflowTrace.Start("authorization.verify.build_order", view);
                    var order = RiskFinancialHandoff.Order(view, command.CommandId);
                    timing_authorization_verify_build_order?.Stop();
                    var intent = new CapacityExecutionAcceptance
                    {
                        ExecutionId = command.CommandId,
                        ExecutionRevision = 1,
                        PortfolioId = risk.PortfolioId,
                        FundId = risk.FundId,
                        OrderId = checked((int)risk.OrderId),
                        ReservationId = handoff.Authorization!.ReservationId,
                        SizedOrderHash = risk.SizedOrderHash,
                        RequirementsHash = risk.Requirements!.ContentHash,
                        Environment = risk.Environment,
                        ValidUntilUtc = handoff.Authorization.ValidUntilUtc,
                        ExecutionOrderHash = order.ContentHash
                    };
                    handoff = handoff with { Phase = RiskFinancialHandoffPhase.Authorized, FundAcceptance = accepted, ExecutionAcceptance = intent, Order = order };
                    break;
                }
            // Historical consumption/submission checkpoints remain readable. The execution owner is
            // not implemented, so this workflow must not manufacture a submission or release a hold.
            default: return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        var next = (resized ?? view with { FinancialHandoff = handoff, WorkflowRevision = checked(view.WorkflowRevision + 1), UpdatedAtUtc = now })
            with
        { CausationId = command.CommandId };
        if (resized is null && handoff!.Phase == RiskFinancialHandoffPhase.Authorized)
            next = next with
            {
                Status = WorkflowStrategyMachineStatus.Completed,
                Outcome = StrategyWorkflowOutcome.Completed,
                TerminalAtUtc = now,
                RiskManagement = next.RiskManagement with { ContinuationDecision = StrategyWorkflowContinuationDecision.Proceed }
            };
        using var timing_authorization_verify_state_update = WorkflowTrace.Start("authorization.verify.state_update", view);
        state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = eventId,
            EntityId = command.EntityId,
            WorkflowId = next.WorkflowId,
            WorkflowRevision = next.WorkflowRevision,
            CorrelationId = next.CorrelationId,
            CausationId = command.CommandId,
            PreviousStatus = view.Status,
            WorkflowDefinition = next,
            UpdatedAtUtc = now
        }, command);
        timing_authorization_verify_state_update?.Stop();
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this AdvanceRiskFinancialHandoffCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this AdvanceRiskFinancialHandoffCommand command, WorkflowSnapshotTransition workflowTransition)
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
