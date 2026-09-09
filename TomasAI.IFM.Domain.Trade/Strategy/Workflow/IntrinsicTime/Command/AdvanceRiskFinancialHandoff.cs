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

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Verifies authoritative receipts and persists the next exact request. It never submits financial mutations.</summary>
public static class AdvanceRiskFinancialHandoff
{
    public static List<ValidationError> ValidateRiskFinancialHandoff(this List<ValidationError> errors,AdvanceRiskFinancialHandoffCommand command)
        =>errors.ValidateCommandId(command.CommandId,command.CommandName).ValidateEntityId(command.EntityId,command.CommandName)
            .CaptureCommandValidation(()=>
            {
                if(command.WorkflowId.Value==Guid.Empty || command.InputWorkflowRevision<1 || !Enum.IsDefined(command.ExpectedPhase))
                    throw new ArgumentException("Exact financial handoff identity and checkpoint are required.");
            });

    public static ValueTask<bool> ResumeAfterAuditAsync(this AdvanceRiskFinancialHandoffCommand command,CancellationToken token)
    { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(true); }

    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this AdvanceRiskFinancialHandoffCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,IntrinsicTimeStrategyWorkflowCommandState state)
    {
        using var timing_authorization_verify_current_view = WorkflowTrace.Start("authorization.verify.current_view", null);
        var view=state.CurrentView;
        timing_authorization_verify_current_view?.Stop();
        using var trace = WorkflowTrace.Start("risk.verify_handoff", view);
        if(view is not { Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.RiskManagement }
            || view.WorkflowId!=command.WorkflowId || view.WorkflowRevision!=command.InputWorkflowRevision
            || view.RiskManagement.ProcessingStatus!=StrategyActorProcessingStatus.Completed
            || (view.FinancialHandoff?.Phase ?? RiskFinancialHandoffPhase.None)!=command.ExpectedPhase)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        using var timing_authorization_verify_build_identity = WorkflowTrace.Start("authorization.verify.build_identity", view);
        var expected=RiskFinancialHandoff.Advance(view);
        timing_authorization_verify_build_identity?.Stop();
        RiskUnitModel.Require(command.CommandId==expected.CommandId,"RM.HANDOFF.IDENTITY");
        var risk=view.RiskManagement.Result?.RiskResult ?? throw new RiskCalculationException("RM.HANDOFF.NO_RESULT");
        RiskUnitModel.Require(risk.Outcome==RiskAssessmentOutcome.Approved && view.RiskExecution is not null,"RM.HANDOFF.NOT_APPROVED");
        var owner=(IIntrinsicTimeStrategyWorkflowCommandContext)context;
        var api=owner.FinancialApi;
        var scope=new FinancialReadScope { PortfolioId=risk.PortfolioId,FundId=risk.FundId,
            Access=new("IntrinsicTimeStrategyWorkflow",["LedgerRead"],[risk.PortfolioId]) };
        var now=context.TimeProvider.GetUtcNow().UtcDateTime;
        var eventId=Guid.CreateVersion7(new DateTimeOffset(now));
        var handoff=view.FinancialHandoff;
        IntrinsicTimeStrategyWorkflowView? resized = null;
        switch(command.ExpectedPhase)
        {
            case RiskFinancialHandoffPhase.None:
            {
                using var timing_authorization_verify_order_read = WorkflowTrace.Start("authorization.verify.order_read", view);
                var order=await owner.PortfolioQueries.GetOrderAsync(checked((int)risk.OrderId)).ConfigureAwait(false);
                timing_authorization_verify_order_read?.Stop();
                RiskUnitModel.Require(order.Success && order.Value is { Status:"RiskPending" } && order.Value.PortfolioId==risk.PortfolioId &&
                    order.Value.FundId==risk.FundId && order.Value.WorkflowId==risk.WorkflowId.Value && order.Value.CompositionResultHash==risk.CompositionResultHash,
                    "RM.HANDOFF.FUND_NOT_READY");
                using var timing_authorization_verify_decode_candidate = WorkflowTrace.Start("authorization.verify.decode_candidate", view);
                var candidate=view.OrderComposition.Result!.ReadCompositionResult().Candidate!;
                timing_authorization_verify_decode_candidate?.Stop();
                using var timing_authorization_verify_admission_read = WorkflowTrace.Start("authorization.verify.admission_read", view);
                var financial=await api.GetFinancialAdmissionSnapshotAsync(scope,new(risk.Authority.DeploymentKey,
                    FinancialScopeKeys.Underlying(candidate.Product.Symbol,candidate.Product.Exchange,candidate.Product.Currency))).ConfigureAwait(false);
                timing_authorization_verify_admission_read?.Stop();
                RiskUnitModel.Require(financial.Success && financial.Value is not null,"RM.HANDOFF.AUTHORITY_UNAVAILABLE");
                now=context.TimeProvider.GetUtcNow().UtcDateTime;
                if (RiskResizing.Changed(view.RiskExecution!, RiskResizing.Authority(view.RiskExecution!, financial.Value!, now)))
                {
                    resized = RiskResizing.Next(view, financial.Value!, now);
                    break;
                }
                handoff=new()
                {
                    Phase=RiskFinancialHandoffPhase.ReservePending,
                    ReservationRequest=RiskFinancialHandoff.Reserve(view,risk,financial.Value!,now),
                    FundCommandId=RiskFinancialHandoff.Identity(risk.InvocationId,"Fund"),FundOrderVersion=order.Value!.AggregateVersion
                };
                break;
            }
            case RiskFinancialHandoffPhase.ReservePending:
            {
                using var timing_authorization_verify_reservation_receipt = WorkflowTrace.Start("authorization.verify.reservation_receipt", view);
                var read=await api.GetPostingReceiptAsync(scope,new(handoff!.ReservationRequest.OperationId)).ConfigureAwait(false);
                timing_authorization_verify_reservation_receipt?.Stop();
                var grant=read.Value?.Value?.Reservation;
                if (read.Success && read.Value is not null && RiskResizing.IsFencedOut(handoff.ReservationRequest, read.Value))
                {
                    var financial = await api.GetFinancialAdmissionSnapshotAsync(scope,
                        new(risk.Authority.DeploymentKey, view.RiskExecution!.SizingAuthority.UnderlyingId)).ConfigureAwait(false);
                    RiskUnitModel.Require(financial.Success && financial.Value is not null, "RM.HANDOFF.AUTHORITY_UNAVAILABLE");
                    now = context.TimeProvider.GetUtcNow().UtcDateTime;
                    resized = RiskResizing.Next(view, financial.Value!, now, read.Value);
                    break;
                }
                RiskUnitModel.Require(read.Success && read.Value?.Status==FinancialReadStatus.Found && grant is not null,"RM.HANDOFF.GRANT_UNAVAILABLE");
                handoff=handoff with { Phase=RiskFinancialHandoffPhase.FundPending,Reservation=grant,
                    Authorization=RiskFinancialHandoff.Authorize(handoff.ReservationRequest,grant!) };
                break;
            }
            case RiskFinancialHandoffPhase.FundPending:
            {
                using var timing_authorization_verify_fund_receipt = WorkflowTrace.Start("authorization.verify.fund_receipt", view);
                var read=await api.GetFundRiskAuthorizationAsync(scope,new(handoff!.FundCommandId)).ConfigureAwait(false);
                timing_authorization_verify_fund_receipt?.Stop();
                var accepted=read.Value?.Value;
                now=context.TimeProvider.GetUtcNow().UtcDateTime;
                RiskUnitModel.Require(read.Success && read.Value?.Status==FinancialReadStatus.Found && accepted is not null &&
                    accepted.CommandId==handoff.FundCommandId && accepted.EventId!=Guid.Empty && accepted.Authorization==handoff.Authorization &&
                    now<handoff.Authorization!.ValidUntilUtc,"RM.HANDOFF.FUND_AUTHORIZATION");
                using var timing_authorization_verify_build_order = WorkflowTrace.Start("authorization.verify.build_order", view);
                var order=RiskFinancialHandoff.Order(view,command.CommandId);
                timing_authorization_verify_build_order?.Stop();
                var intent=new CapacityExecutionAcceptance
                {
                    ExecutionId=command.CommandId,ExecutionRevision=1,PortfolioId=risk.PortfolioId,FundId=risk.FundId,
                    OrderId=checked((int)risk.OrderId),ReservationId=handoff.Authorization!.ReservationId,SizedOrderHash=risk.SizedOrderHash,
                    RequirementsHash=risk.Requirements!.ContentHash,Environment=risk.Environment,ValidUntilUtc=handoff.Authorization.ValidUntilUtc,
                    ExecutionOrderHash=order.ContentHash
                };
                handoff=handoff with { Phase=RiskFinancialHandoffPhase.Authorized,FundAcceptance=accepted,ExecutionAcceptance=intent,Order=order };
                break;
            }
            // Historical consumption/submission checkpoints remain readable. The execution owner is
            // not implemented, so this workflow must not manufacture a submission or release a hold.
            default:return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        var next=(resized ?? view with { FinancialHandoff=handoff,WorkflowRevision=checked(view.WorkflowRevision+1),UpdatedAtUtc=now })
            with { CausationId=command.CommandId };
        if(resized is null && handoff!.Phase==RiskFinancialHandoffPhase.Authorized)
            next=next with { Status=WorkflowStrategyMachineStatus.Completed,Outcome=StrategyWorkflowOutcome.Completed,TerminalAtUtc=now,
                RiskManagement=next.RiskManagement with { ContinuationDecision=StrategyWorkflowContinuationDecision.Proceed } };
        using var timing_authorization_verify_state_update = WorkflowTrace.Start("authorization.verify.state_update", view);
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Id=eventId,CommandId=command.CommandId,EntityId=command.EntityId,
            Subject=new(ActorType.Event,WorkflowStrategyStateUpdatedEvent.Actor,WorkflowStrategyStateUpdatedEvent.Verb,command.EntityId.Format()),
            AggregateId=command.EntityId.Format(),EventSource=command.EventSource,ReceivedOn=now,WorkflowId=next.WorkflowId,
            WorkflowRevision=next.WorkflowRevision,CorrelationId=next.CorrelationId,CausationId=command.CommandId,
            PreviousStatus=view.Status,State=next,UpdatedAtUtc=now
        },command);
        timing_authorization_verify_state_update?.Stop();
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
}
