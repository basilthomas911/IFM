using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;

/// <summary>Applies prepared results only to their original intent and expected committed revision.</summary>
public static class AdvanceStrategyExecutionHandoff
{
    extension(AdvanceStrategyExecutionHandoffCommand command)
    {
        public ServiceResult<GuidResult> Execute(StrategyExecutionHandoffCommandState state, DateTime now)
        {
            var current = state.Definition;
            var next = command.Evidence;
            if (current is null || current.Id != command.EntityId || next.Id != current.Id ||
                next.IntentHash != current.IntentHash || BeginStrategyExecutionHandoff.Fingerprint(next) != current.IntentHash)
                return new ServiceFailed<GuidResult>(command.ErrorCode, "EXECUTION.ADVANCE.IDENTITY_CONFLICT");
            if (command.ExpectedRevision != current.Revision)
                return new ServiceOk<GuidResult>(new(command.CommandId));
            if (current.ExecutionRequestedAtUtc is { } requested && next.ExecutionRequestedAtUtc != requested)
                return new ServiceFailed<GuidResult>(command.ErrorCode, $"EXECUTION.ADVANCE.REQUEST_CONFLICT;expected={requested:O};received={next.ExecutionRequestedAtUtc:O}");
            var expired = next.Phase == StrategyExecutionHandoffPhase.Expired && next.Execution is null &&
                next.FinancialReceipt?.ProjectionFacts?.ExecutionCapacity?.Outcome is
                    { UnsubmittedExpiry: true, FilledUnits: 0 } expiry && expiry.ExecutionId == current.Id.ExecutionAttemptId &&
                expiry.FundId == current.Order.Id.FundId && expiry.OrderId == current.Order.Id.OrderId &&
                expiry.CancelledUnits == expiry.ApprovedUnits && next.FundEvidence?.Status == "Cancelled";
            var terminalMatches = next.Phase switch
            {
                StrategyExecutionHandoffPhase.Opened => next.Execution is { CumulativeFilledQuantity: > 0 } &&
                    next.Execution.Status is Shared.OrderExecutionStatus.Filled or Shared.OrderExecutionStatus.Cancelled &&
                    next.FundEvidence?.Status is "Opened" or "Closing" or "Closed",
                StrategyExecutionHandoffPhase.Rejected => next.Execution is { Status: Shared.OrderExecutionStatus.Rejected, Fills.Length: 0 } && next.FundEvidence?.Status == "Rejected",
                StrategyExecutionHandoffPhase.Cancelled => next.Execution is { Status: Shared.OrderExecutionStatus.Cancelled, Fills.Length: 0 } && next.FundEvidence?.Status == "Cancelled",
                _ => false
            };
            if (!Enum.IsDefined(next.Phase) || next.Phase >= StrategyExecutionHandoffPhase.Opened && !expired &&
                (!terminalMatches || next.Execution is null || next.FinancialReceipt?.ProjectionFacts?.ExecutionCapacity?.Outcome.ExecutionHash !=
                    TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(next.Execution) || next.FundEvidence is null))
                return new ServiceFailed<GuidResult>(command.ErrorCode, "EXECUTION.ADVANCE.FINANCIAL_EVIDENCE_REQUIRED");
            var lateFill = current.Phase is StrategyExecutionHandoffPhase.Cancelled or StrategyExecutionHandoffPhase.Rejected &&
                next.Phase == StrategyExecutionHandoffPhase.Opened && terminalMatches;
            if (!lateFill && (next.Phase < current.Phase || next.Phase > current.Phase + 1 && next.Phase < StrategyExecutionHandoffPhase.Rejected))
                return new ServiceFailed<GuidResult>(command.ErrorCode, "EXECUTION.ADVANCE.INVALID_TRANSITION");
            return state.Update(BeginStrategyExecutionHandoff.Snapshot(command,
                next with { Revision = checked(current.Revision + 1), UpdatedAtUtc = now,
                    ExecutionRequestedAtUtc = current.ExecutionRequestedAtUtc ??
                        (next.Phase == StrategyExecutionHandoffPhase.OrderReady ? now : next.ExecutionRequestedAtUtc) }, now), command)
                ? new ServiceOk<GuidResult>(new(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode, "EXECUTION.STATE.APPLY_FAILED");
        }
    }
}
