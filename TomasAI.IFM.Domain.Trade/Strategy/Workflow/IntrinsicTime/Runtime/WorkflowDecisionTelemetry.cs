using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Decision timings exclude subsequent persistence, entry commitment and broker execution.</summary>
public static class WorkflowDecisionTelemetry
{
    public const string MeterName = "TomasAI.IFM.Strategy.Decisions";
    static readonly Meter Meter = new(MeterName, "1.0");
    static readonly Histogram<double> queue = Meter.CreateHistogram<double>("ifm.strategy.decision.queue", "ms");
    static readonly Histogram<double> calculation = Meter.CreateHistogram<double>("ifm.strategy.decision.calculation", "ms");
    static readonly Histogram<double> preparation = Meter.CreateHistogram<double>("ifm.strategy.decision.preparation", "ms");
    static readonly Histogram<double> stage = Meter.CreateHistogram<double>("ifm.strategy.decision.stage", "ms");
    static readonly Counter<long> outcomes = Meter.CreateCounter<long>("ifm.strategy.decision.outcomes");
    internal static void RecordBoundary(IntrinsicTimeStrategyWorkflowView before, IntrinsicTimeStrategyWorkflowView after, DateTime now)
    {
        var ready = before.CurrentStage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => before.RegimeDiscovery.StartedAtUtc,
            StrategyWorkflowStage.MarketCondition => before.MarketCondition.StartedAtUtc,
            StrategyWorkflowStage.TradeSelection => before.TradeSelection.StartedAtUtc,
            StrategyWorkflowStage.OrderComposition => before.OrderComposition.StartedAtUtc,
            StrategyWorkflowStage.RiskManagement => before.RiskManagement.StartedAtUtc,
            _ => null
        } ?? before.UpdatedAtUtc;
        stage.Record(Math.Max(0, (now - ready).TotalMilliseconds), new System.Diagnostics.TagList
        {
            { "stage", before.CurrentStage.ToString() }, { "timeframe", before.TriggerEvent.EntityId.TimePeriod.ToString() },
            { "family", before.EntityId.Assignment?.Family ?? "Unassigned" },
            { "outcome", after.Status == WorkflowStrategyMachineStatus.Started ? "Proceed" : after.StopReasonCode }
        });
    }

    internal static void Record<T>(IntrinsicTimeStrategyWorkflowView view, DecisionExecution<T> result, double preparationMilliseconds)
    {
        var tags = new System.Diagnostics.TagList
        {
            { "stage", view.CurrentStage.ToString() }, { "timeframe", view.TriggerEvent is { EntityId: { } signal } ? signal.TimePeriod.ToString() : "Unknown" },
            { "family", view.EntityId.Assignment?.Family ?? "Unassigned" }, { "outcome", result.Status.ToString() }
        };
        preparation.Record(Math.Max(0, preparationMilliseconds), tags);
        queue.Record(result.QueueTime.TotalMilliseconds, tags);
        calculation.Record(result.CalculationTime.TotalMilliseconds, tags);
        outcomes.Add(1, tags);
    }
}
