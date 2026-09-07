using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
public static class TradeSelectionHandoff
{
    public static void ValidateStart(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.StartOrderCompositionPipelineCommand command,DateTime now)
    {
        var state=command.WorkflowState;
        Require(command.AcceptedSelection is not null && command.SelectionBinding is not null && command.Reservation is not null && state.CompositionHandoff is {Status:CompositionHandoffStatus.Reserved},"TS.HANDOFF.INVALID","Missing accepted selection, binding or committed reservation.");
        var result=ReadResult(command.AcceptedSelection);
        var handoff=state.CompositionHandoff!;
        Require(state.CurrentStage==StrategyWorkflowStage.OrderComposition && state.Status==StrategyWorkflowStatus.Running && command.WorkflowId==state.WorkflowId && command.EntityId==state.EntityId && command.InputWorkflowRevision==state.WorkflowRevision
            && command.SelectionBinding.PayloadSha256==result.DecisionContext.SelectionBinding.PayloadSha256 && command.AcceptedSelection.PayloadSha256==state.TradeSelection.Result?.PayloadSha256
            && result.Outcome==SelectionOutcome.Selected && now<result.ValidUntilUtc && command.ExpectedCompletionAtUtc==handoff.Request.ExpiresAtUtc && now<command.ExpectedCompletionAtUtc,"TS.HANDOFF.INVALID","Composition start scope or validity differs from accepted selection.");
        Require(state.SelectionDispatch is not null && EvidenceHash(TradeSelectionEvaluator.Evaluate(state.SelectionDispatch))==EvidenceHash(result),"TS.HANDOFF.INVALID","Composition result intent was changed.");
        ValidateReservation(handoff,command.Reservation);
    }

    public static WorkflowCompositionHandoffState Pending(TradeSelectionResult result,StrategyStageResultEnvelope envelope,long acceptedRevision,Guid sourceId,DateTime now)
    {
        Require(result.Outcome==SelectionOutcome.Selected && result.SelectedCandidate is not null,"TS.RESULT.INVALID","Only Selected can reserve identities.");
        var intent=result.SelectedCandidate;var binding=result.DecisionContext.SelectionBinding;var authority=binding.PortfolioSnapshot;
        var assignment=authority.Assignments.Single(x=>x.AssignmentVersion==intent.AssignmentVersion && x.TradeStrategyFamily?.CatalogDeployment==intent.DeploymentKey);
        var request=new ReserveFundOrderCompositionRequest
        {
            WorkflowId=result.WorkflowId.Value,WorkflowRevision=authority.WorkflowRevision,TradeSelectionInvocationId=result.InvocationId,TradeSelectionResultId=result.ResultId,TradeSelectionResultSha256=envelope.PayloadSha256,
            PortfolioId=result.PortfolioId,PortfolioVersion=authority.Portfolio.PortfolioVersion,FundId=result.FundId,FundMandateVersion=authority.Fund.FundMandateVersion,
            TradeTemplateId=intent.DeploymentKey.Id,TradeTemplateVersion=intent.DeploymentKey.Version,OrderCompositionProfileId=intent.CompositionPolicyReference.Id,OrderCompositionProfileVersion=intent.CompositionPolicyReference.Version,
            UnderlyingRoot=intent.Product.Symbol,DecisionHorizon=result.DecisionHorizon.ToString(),RequestedTradeDate=binding.RequestedTradeDate,Origin=CompositionOrigin.StrategyWorkflow,IdempotencyKey=result.ResultId,
            RequestedAtUtc=now,ExpiresAtUtc=result.ValidUntilUtc,PortfolioFundStrategySnapshotSha256=authority.PayloadSha256,
            TradeInstructions=[new(){TradeFamily=assignment.TradeFamily,TradeRole="Primary",IsPrimaryTrade=true,DirectionOrBias=intent.Bias=="Balanced"?"Neutral":intent.Bias,TradeAction="Open",
                UnderlyingRoot=intent.Product.Symbol,RequestedTradeDate=binding.RequestedTradeDate,Reference=intent.CandidateHash,CreatedOnUtc=now,CreatedBy="TradeSelection"}]
        };
        return new(){Status=CompositionHandoffStatus.ReservationPending,SelectionSourceEventId=sourceId,AcceptedSelectionRevision=acceptedRevision,Request=request,ReservationRequestSha256=PortfolioCanonicalHash.Compute(request),UpdatedAtUtc=now};
    }
    public static void ValidateReservation(WorkflowCompositionHandoffState handoff,FundCompositionReservationResult reservation)
    {
        var request=handoff.Request;var order=reservation.Order;
        Require(handoff.ReservationRequestSha256==PortfolioCanonicalHash.Compute(request) && reservation.CanonicalRequestSha256==handoff.ReservationRequestSha256 && order.CanonicalRequestHash==handoff.ReservationRequestSha256,
            "TS.RESERVATION.INVALID","Reservation request hash mismatch.");
        Require(reservation.Disposition is ReservationDisposition.Committed or ReservationDisposition.IdempotentReplay && reservation.AggregateVersion>0 && Utc(reservation.CommittedOnUtc)
            && order.OrderId>0 && order.PortfolioId==request.PortfolioId && order.FundId==request.FundId && order.WorkflowId==request.WorkflowId && order.WorkflowRevision==request.WorkflowRevision
            && order.TradeSelectionResultId==request.TradeSelectionResultId && order.TradeSelectionResultHash==request.TradeSelectionResultSha256 && order.IdempotencyKey==request.IdempotencyKey
            && order.TradeTemplateId==request.TradeTemplateId && order.TradeTemplateVersion==request.TradeTemplateVersion && order.OrderCompositionProfileId==request.OrderCompositionProfileId
            && order.OrderCompositionProfileVersion==request.OrderCompositionProfileVersion && order.StrategySnapshotHash==request.PortfolioFundStrategySnapshotSha256 && order.ExpiresAtUtc==request.ExpiresAtUtc,
            "TS.RESERVATION.INVALID","Reservation belongs to different selection/authority.");
        Require(reservation.Trades.Length==1 && request.TradeInstructions.Length==1 && request.TradeInstructions[0].IsPrimaryTrade,"TS.RESERVATION.INVALID","One primary Trade identity is required.");
        var trade=reservation.Trades[0];var instruction=request.TradeInstructions[0];
        Require(trade.OrderId==order.OrderId && trade.TradeId>0 && trade.PortfolioId==request.PortfolioId && trade.FundId==request.FundId && trade.InstructionReference==instruction.Reference
            && trade.TradeFamily==instruction.TradeFamily && trade.DirectionOrBias==instruction.DirectionOrBias && trade.TradeAction==instruction.TradeAction && trade.UnderlyingRoot==request.UnderlyingRoot
            && trade.RequestedTradeDate==request.RequestedTradeDate && trade.RequestedMaturityDate is null && trade.LegOrdinal==1,"TS.RESERVATION.INVALID","Primary instruction was changed during reservation.");
    }
}
