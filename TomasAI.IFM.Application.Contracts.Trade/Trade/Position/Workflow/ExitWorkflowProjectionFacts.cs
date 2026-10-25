namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Validates accepted exit-stage facts without coordinating persistence or executing an exit.</summary>
public static class ExitWorkflowProjectionFacts
{
    /// <summary>Requires a structurally consistent recorded workflow stage.</summary>
    /// <param name="workflow">The accepted workflow snapshot.</param>
    public static void RequireValid(ExitPositionWorkflowProjection workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
            if (workflow.SchemaVersion != 1
                || !workflow.WorkflowId.IsValid
                || workflow.StrategyKind is not (
                    TradeStrategyKind.IronCondor
                    or TradeStrategyKind.VerticalSpread
                    or TradeStrategyKind.FuturesOutright)
                || workflow.State is < ExitPositionWorkflowState.Started
                    or > ExitPositionWorkflowState.Completed
                || workflow.StageRevision < 1
                || workflow.UpdatedAtUtc.Kind != DateTimeKind.Utc
                || workflow.SourcePlanEventId == Guid.Empty
                || workflow.ExitPlan.Position.Id != workflow.WorkflowId.Position
                || workflow.ExitPlan.ValueDate != workflow.WorkflowId.ValueDate
                || workflow.ExitPlan.Position.StrategyKind != workflow.StrategyKind)
            {
                throw new ArgumentException(
                    "Valid exit-workflow projection is required.",
                    nameof(workflow));
            }
    }
}
