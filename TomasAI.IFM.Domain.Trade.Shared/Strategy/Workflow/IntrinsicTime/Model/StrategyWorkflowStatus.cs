namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Describes the lifecycle status of a strategy workflow execution.</summary>
public enum StrategyWorkflowStatus
{
    /// <summary>The workflow has not started.</summary>
    None = 0,

    /// <summary>The workflow has an active pipeline stage.</summary>
    Running = 1,

    /// <summary>The workflow completed all required stages.</summary>
    Completed = 2,

    /// <summary>The workflow stopped before successful completion.</summary>
    Stopped = 3
}
