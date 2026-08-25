namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Describes the terminal business outcome of a strategy workflow execution.</summary>
public enum StrategyWorkflowOutcome
{
    /// <summary>No terminal outcome has been recorded.</summary>
    None = 0,

    /// <summary>All workflow stages completed successfully.</summary>
    Completed = 1,

    /// <summary>A strategy pipeline reported a failure.</summary>
    PipelineFailed = 2,

    /// <summary>A strategy pipeline returned an invalid result.</summary>
    InvalidResult = 3,

    /// <summary>The active strategy pipeline timed out.</summary>
    TimedOut = 4,

    /// <summary>The workflow was cancelled.</summary>
    Cancelled = 5,

    /// <summary>Conflicting duplicate data violated workflow consistency.</summary>
    ConsistencyFault = 6
}
