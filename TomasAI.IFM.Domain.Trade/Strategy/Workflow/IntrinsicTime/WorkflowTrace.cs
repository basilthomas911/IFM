using System.Diagnostics;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime;

/// <summary>Bounded workflow operation spans. Business identity is a tag, never a fabricated W3C trace ID.</summary>
public static class WorkflowTrace
{
    public const string SourceName = "TomasAI.IFM.StrategyWorkflow";
    public static readonly ActivitySource Source = new(SourceName);

    public static Activity? Start(string operation, IntrinsicTimeStrategyWorkflowView? view)
    {
        var span = Source.StartActivity(operation);
        span?.SetTag("ifm.workflow.id", view?.WorkflowId.ToString());
        span?.SetTag("ifm.correlation.id", view?.CorrelationId.ToString());
        span?.SetTag("ifm.workflow.entity", view?.EntityId.Format());
        span?.SetTag("ifm.workflow.revision", view?.WorkflowRevision);
        span?.SetTag("ifm.workflow.stage", view?.CurrentStage.ToString());
        span?.SetTag("ifm.workflow.phase", view?.FinancialHandoff?.Phase.ToString());
        return span;
    }
}
