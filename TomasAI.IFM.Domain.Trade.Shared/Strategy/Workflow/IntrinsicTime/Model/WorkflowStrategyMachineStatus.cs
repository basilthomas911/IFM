namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Defines the authoritative atomic lifecycle of one Strategy Workflow execution.</summary>
public enum WorkflowStrategyMachineStatus : byte
{
    /// <summary>No authoritative workflow snapshot exists.</summary>
    Empty = 0,
    /// <summary>The workflow has an outstanding current pipeline stage.</summary>
    Started = 1,
    /// <summary>Every required pipeline completed successfully.</summary>
    Completed = 2,
    /// <summary>A current pipeline or workflow validation failed.</summary>
    Failed = 3,
    /// <summary>The fixed workflow execution deadline was reached.</summary>
    TimedOut = 4,
    /// <summary>An explicit cancellation closed the workflow.</summary>
    Cancelled = 5
}
