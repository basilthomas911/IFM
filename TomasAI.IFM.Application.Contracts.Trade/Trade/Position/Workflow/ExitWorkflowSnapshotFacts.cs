using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Maps only recorded accepted facts into a complete exit workflow read row.</summary>
public static class ExitWorkflowSnapshotFacts
{
    /// <summary>Uses the recorded stage snapshot, or constructs the initial row from the complete start event.</summary>
    /// <remarks>Never reads earlier rows or calls a calculator, actor, broker or financial service.</remarks>
    public static ExitPositionWorkflowProjection From(IEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source switch
        {
            ExitPositionWorkflowStartedEvent started => new ExitPositionWorkflowProjection
            {
                WorkflowId = started.EntityId, StrategyKind = started.StrategyKind,
                State = ExitPositionWorkflowState.Started, StageRevision = 1,
                UpdatedAtUtc = DateTime.SpecifyKind(started.ReceivedOn, DateTimeKind.Utc),
                SourcePlanEventId = started.SourcePlanEventId, ExitPlan = started.ExitPlan
            },
            ExitOrderCompositionCompletedEvent composed => composed.Projection,
            PositionExitRiskCompletedEvent risk => risk.Projection,
            _ => throw new ArgumentException("Unsupported exit workflow source event: " + source.GetType().Name, nameof(source))
        } ?? throw new InvalidOperationException("The older exit completion lacks recorded projection facts; use its verified migration baseline.");
    }
}
