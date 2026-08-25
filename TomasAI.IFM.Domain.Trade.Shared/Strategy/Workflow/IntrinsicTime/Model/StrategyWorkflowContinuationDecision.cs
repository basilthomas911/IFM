namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Describes the workflow-owned continuation decision following a pipeline result.</summary>
public enum StrategyWorkflowContinuationDecision
{
    /// <summary>No continuation decision has been evaluated.</summary>
    None = 0,

    /// <summary>The workflow may start the next pipeline stage.</summary>
    Proceed = 1,

    /// <summary>The workflow must stop without starting another pipeline stage.</summary>
    Stop = 2
}
