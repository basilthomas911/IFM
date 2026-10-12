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
        var at = v.RiskExecution?.EvaluatedAtUtc ?? snapshot.UpdatedAtUtc;
        return new(c.PortfolioId, c.FundId, DateOnly.FromDateTime(at), at, v.WorkflowId.Value,
            v.RiskExecution?.CommandId ?? snapshot.CommandId, v.WorkflowRevision,
            result?.Outcome.ToString() ?? v.RiskManagement.ProcessingStatus.ToString(), result?.StrategyUnits ?? 0,
            string.IsNullOrEmpty(v.StopReasonCode) ? result?.Reasons.FirstOrDefault() ?? v.RiskManagement.Failure?.ErrorMessage ?? "Pending" : v.StopReasonCode,
            c.TargetHorizon.ToString(), c.OrderId, c.VariantKey?.ToString() ?? "Unrecorded");
    }
}
