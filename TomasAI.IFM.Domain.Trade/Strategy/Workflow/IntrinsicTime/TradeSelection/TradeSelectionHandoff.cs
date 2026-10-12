using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
public static class TradeSelectionHandoff
{
    public static void ValidateStart(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.StartOrderCompositionPipelineCommand command, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = command.WorkflowState;
        Require(command.AcceptedSelection is not null && command.SelectionBinding is not null, "TS.HANDOFF.INVALID", "Missing accepted selection or binding.");
        var result = ReadResult(command.AcceptedSelection);
        if (command.SelectionBinding.SchemaVersion == 2)
        {
            Require(command.Reservation is null && state.CompositionHandoff is null && state.CurrentStage == StrategyWorkflowStage.OrderComposition && state.Status == StrategyWorkflowStatus.Running && command.WorkflowId == state.WorkflowId && command.EntityId == state.EntityId && command.InputWorkflowRevision == state.WorkflowRevision && string.Equals(command.SelectionBinding.PayloadSha256, result.DecisionContext.SelectionBinding.PayloadSha256, StringComparison.Ordinal) && string.Equals(command.AcceptedSelection.PayloadSha256, state.TradeSelection.Result?.PayloadSha256, StringComparison.Ordinal) && result.Outcome == SelectionOutcome.Selected && now < result.ValidUntilUtc && command.ExpectedCompletionAtUtc is { } deadline && now < deadline, "TS.HANDOFF.INVALID", "Portfolio-neutral composition start is invalid.");
            Require(state.SelectionDispatch is not null && string.Equals(EvidenceHash(TradeSelectionEvaluator.Evaluate(state.SelectionDispatch)), EvidenceHash(result), StringComparison.Ordinal), "TS.HANDOFF.INVALID", "Composition result intent was changed.");
            return;
        }

        Require(command.Reservation is not null && state.CompositionHandoff is { Status: CompositionHandoffStatus.Reserved }, "TS.HANDOFF.INVALID", "Legacy composition start requires a committed reservation.");
        var handoff = state.CompositionHandoff!;
        Require(state.CurrentStage == StrategyWorkflowStage.OrderComposition && state.Status == StrategyWorkflowStatus.Running && command.WorkflowId == state.WorkflowId && command.EntityId == state.EntityId && command.InputWorkflowRevision == state.WorkflowRevision && string.Equals(command.SelectionBinding.PayloadSha256, result.DecisionContext.SelectionBinding.PayloadSha256, StringComparison.Ordinal) && string.Equals(command.AcceptedSelection.PayloadSha256, state.TradeSelection.Result?.PayloadSha256, StringComparison.Ordinal) && result.Outcome == SelectionOutcome.Selected && now < result.ValidUntilUtc && command.ExpectedCompletionAtUtc == handoff.Request.ExpiresAtUtc && now < command.ExpectedCompletionAtUtc, "TS.HANDOFF.INVALID", "Composition start scope or validity differs from accepted selection.");
        Require(state.SelectionDispatch is not null && string.Equals(EvidenceHash(TradeSelectionEvaluator.Evaluate(state.SelectionDispatch)), EvidenceHash(result), StringComparison.Ordinal), "TS.HANDOFF.INVALID", "Composition result intent was changed.");
        ValidateReservation(handoff, command.Reservation!);
    }

    public static WorkflowCompositionHandoffState Pending(TradeSelectionResult result, StrategyStageResultEnvelope envelope, long acceptedRevision, Guid sourceId, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(result);
        Require(result.Outcome == SelectionOutcome.Selected && result.SelectedCandidate is not null, "TS.RESULT.INVALID", "Only Selected can reserve identities.");
        var intent = result.SelectedCandidate;
        var binding = result.DecisionContext.SelectionBinding;
        var authority = binding.PortfolioSnapshot;
        var assignment = authority.Assignments.Single(x => x.AssignmentVersion == intent.AssignmentVersion && x.TradeStrategyFamily?.CatalogDeployment == intent.DeploymentKey);
        var request = new ReserveFundOrderCompositionRequest
        {
            WorkflowId = result.WorkflowId.Value,
            WorkflowRevision = authority.WorkflowRevision,
            TradeSelectionInvocationId = result.InvocationId,
            TradeSelectionResultId = result.ResultId,
            TradeSelectionResultSha256 = envelope.PayloadSha256,
            PortfolioId = result.PortfolioId,
            PortfolioVersion = authority.Portfolio.PortfolioVersion,
            FundId = result.FundId,
            FundMandateVersion = authority.Fund.FundMandateVersion,
            TradeTemplateId = intent.DeploymentKey.Id,
            TradeTemplateVersion = intent.DeploymentKey.Version,
            OrderCompositionProfileId = intent.CompositionPolicyReference.Id,
            OrderCompositionProfileVersion = intent.CompositionPolicyReference.Version,
            UnderlyingRoot = intent.Product.Symbol,
            DecisionHorizon = result.DecisionHorizon.ToString(),
            RequestedTradeDate = binding.RequestedTradeDate,
            Origin = CompositionOrigin.StrategyWorkflow,
            IdempotencyKey = result.ResultId,
            RequestedAtUtc = now,
            ExpiresAtUtc = result.ValidUntilUtc,
            PortfolioFundStrategySnapshotSha256 = authority.PayloadSha256,
            TradeInstructions = [new()
            {
                TradeFamily = assignment.TradeFamily,
                TradeRole = "Primary",
                IsPrimaryTrade = true,
                DirectionOrBias = string.Equals(intent.Bias, "Balanced", StringComparison.Ordinal) ? "Neutral" : intent.Bias,
                TradeAction = "Open",
                UnderlyingRoot = intent.Product.Symbol,
                RequestedTradeDate = binding.RequestedTradeDate,
                Reference = intent.CandidateHash,
                CreatedOnUtc = now,
                CreatedBy = "TradeSelection"
            }

            ]
        };
        return new()
        {
            Status = CompositionHandoffStatus.ReservationPending,
            SelectionSourceEventId = sourceId,
            AcceptedSelectionRevision = acceptedRevision,
            Request = request,
            ReservationRequestSha256 = PortfolioCanonicalHash.Compute(request),
            UpdatedAtUtc = now
        };
    }

    public static void ValidateReservation(WorkflowCompositionHandoffState handoff, FundCompositionReservationResult reservation)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(reservation);
        var request = handoff.Request;
        var order = reservation.Order;
        Require(string.Equals(handoff.ReservationRequestSha256, PortfolioCanonicalHash.Compute(request), StringComparison.Ordinal) && string.Equals(reservation.CanonicalRequestSha256, handoff.ReservationRequestSha256, StringComparison.Ordinal) && string.Equals(order.CanonicalRequestHash, handoff.ReservationRequestSha256, StringComparison.Ordinal), "TS.RESERVATION.INVALID", "Reservation request hash mismatch.");
        Require(reservation.Disposition is ReservationDisposition.Committed or ReservationDisposition.IdempotentReplay && reservation.AggregateVersion > 0 && Utc(reservation.CommittedOnUtc) && order.OrderId > 0 && order.PortfolioId == request.PortfolioId && order.FundId == request.FundId && order.WorkflowId == request.WorkflowId && order.WorkflowRevision == request.WorkflowRevision && order.TradeSelectionResultId == request.TradeSelectionResultId && string.Equals(order.TradeSelectionResultHash, request.TradeSelectionResultSha256, StringComparison.Ordinal) && order.IdempotencyKey == request.IdempotencyKey && order.TradeTemplateId == request.TradeTemplateId && order.TradeTemplateVersion == request.TradeTemplateVersion && order.OrderCompositionProfileId == request.OrderCompositionProfileId && order.OrderCompositionProfileVersion == request.OrderCompositionProfileVersion && string.Equals(order.StrategySnapshotHash, request.PortfolioFundStrategySnapshotSha256, StringComparison.Ordinal) && order.ExpiresAtUtc == request.ExpiresAtUtc, "TS.RESERVATION.INVALID", "Reservation belongs to different selection/authority.");
        Require(reservation.Trades.Length == 1 && request.TradeInstructions.Length == 1 && request.TradeInstructions[0].IsPrimaryTrade, "TS.RESERVATION.INVALID", "One primary Trade identity is required.");
        var trade = reservation.Trades[0];
        var instruction = request.TradeInstructions[0];
        Require(trade.OrderId == order.OrderId && trade.TradeId > 0 && trade.PortfolioId == request.PortfolioId && trade.FundId == request.FundId && string.Equals(trade.InstructionReference, instruction.Reference, StringComparison.Ordinal) && string.Equals(trade.TradeFamily, instruction.TradeFamily, StringComparison.Ordinal) && string.Equals(trade.DirectionOrBias, instruction.DirectionOrBias, StringComparison.Ordinal) && string.Equals(trade.TradeAction, instruction.TradeAction, StringComparison.Ordinal) && string.Equals(trade.UnderlyingRoot, request.UnderlyingRoot, StringComparison.Ordinal) && trade.RequestedTradeDate == request.RequestedTradeDate && trade.RequestedMaturityDate is null && trade.LegOrdinal == 1, "TS.RESERVATION.INVALID", "Primary instruction was changed during reservation.");
    }
}
