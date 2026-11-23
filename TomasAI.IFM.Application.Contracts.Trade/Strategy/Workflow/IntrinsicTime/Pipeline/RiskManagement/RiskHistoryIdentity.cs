using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

public static class RiskHistoryIdentity
{
    public static RiskHistoryRow? Row(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var v = snapshot.State;
        var c = v.OrderComposition.Result?.CompositionResult?.Candidate;
        if (c is null || v.CurrentStage != Model.StrategyWorkflowStage.RiskManagement) return null;
        var result = v.RiskManagement.Result?.RiskResult;
        var entry = v.EntryCommit;
        var at = v.RiskExecution?.EvaluatedAtUtc ?? entry?.Approval.ProducedAtUtc ?? snapshot.UpdatedAtUtc;
        var outcome = entry?.Phase switch
        {
            Model.WorkflowEntryCommitPhase.Approved => "EntryPending",
            Model.WorkflowEntryCommitPhase.OutcomeUnknown => "EntryOutcomeUnknown",
            Model.WorkflowEntryCommitPhase.Rejected => "NoTrade",
            Model.WorkflowEntryCommitPhase.Committed => "Committed",
            _ => result?.Outcome.ToString() ?? v.RiskManagement.ProcessingStatus.ToString()
        };
        return new(v.EntityId.Assignment?.PortfolioId ?? c.PortfolioId, v.EntityId.Assignment?.FundId ?? c.FundId, DateOnly.FromDateTime(at), at, v.WorkflowId.Value,
            entry?.Approval.Request.OperationId ?? v.RiskExecution?.CommandId ?? snapshot.CommandId, v.WorkflowRevision,
            outcome, result?.StrategyUnits ?? 0,
            string.IsNullOrEmpty(v.StopReasonCode) ? entry?.Reason ?? result?.Reasons.FirstOrDefault() ?? v.RiskManagement.Failure?.ErrorMessage ?? "Pending" : v.StopReasonCode,
            c.TargetHorizon.ToString(), c.OrderId, c.VariantKey?.ToString() ?? "Unrecorded");
    }
}
