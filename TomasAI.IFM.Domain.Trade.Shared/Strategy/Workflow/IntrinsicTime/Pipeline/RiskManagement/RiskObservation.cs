using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

[MessagePackObject]
public sealed record RiskHistoryRow([property:Key(0)] int PortfolioId, [property:Key(1)] int FundId,
    [property:Key(2)] DateOnly ValueDate, [property:Key(3)] DateTime EvaluatedAtUtc, [property:Key(4)] Guid WorkflowId,
    [property:Key(5)] Guid InvocationId, [property:Key(6)] long Revision, [property:Key(7)] string Outcome,
    [property:Key(8)] int Units, [property:Key(9)] string Reason, [property:Key(10)] string Horizon,
    [property:Key(11)] long OrderId,[property:Key(12)] string Variant="");
[MessagePackObject]
public sealed record RiskHistoryPage([property:Key(0)] RiskHistoryRow[] Items, [property:Key(1)] string? PagingState);
[MessagePackObject]
public sealed record RiskObservation([property:Key(0)] WorkflowStrategyStateUpdatedEvent Snapshot,
    [property:Key(1)] RiskAssessmentResult? Calculation, [property:Key(2)] string CurrentAuthority,
    [property:Key(3)] string FundSynchronization, [property:Key(4)] DateTime CheckedAtUtc,
    [property:Key(5)] FundOrderProjectionReadModel? FundOrder, [property:Key(6)] bool ProjectionBehind, [property:Key(7)] bool CalculationAccepted=false);

public static class RiskHistoryIdentity
{
    public static RiskHistoryRow? Row(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var v = snapshot.State;
        var c = v.OrderComposition.Result?.CompositionResult?.Candidate;
        if (c is null || v.CurrentStage != Model.StrategyWorkflowStage.RiskManagement) return null;
        var result = v.RiskManagement.Result?.RiskResult;
        var at = v.RiskExecution?.EvaluatedAtUtc ?? snapshot.UpdatedAtUtc;
        return new(c.PortfolioId, c.FundId, DateOnly.FromDateTime(at), at, v.WorkflowId.Value,
            v.RiskExecution?.CommandId ?? snapshot.CommandId, v.WorkflowRevision,
            result?.Outcome.ToString() ?? v.RiskManagement.ProcessingStatus.ToString(), result?.StrategyUnits ?? 0,
            string.IsNullOrEmpty(v.StopReasonCode) ? result?.Reasons.FirstOrDefault() ?? v.RiskManagement.Failure?.ErrorMessage ?? "Pending" : v.StopReasonCode,
            c.TargetHorizon.ToString(), c.OrderId,c.VariantKey?.ToString() ?? "Unrecorded");
    }
}
