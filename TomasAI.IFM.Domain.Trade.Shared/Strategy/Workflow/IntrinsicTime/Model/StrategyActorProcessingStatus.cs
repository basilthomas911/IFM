namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Describes the processing status recorded for one strategy pipeline actor stage.</summary>
public enum StrategyActorProcessingStatus
{
    /// <summary>The strategy pipeline has not started.</summary>
    NotStarted = 0,

    /// <summary>The strategy pipeline is processing its private state.</summary>
    Processing = 1,

    /// <summary>The strategy pipeline completed and returned a result.</summary>
    Completed = 2,

    /// <summary>The strategy pipeline reported a failure.</summary>
    Failed = 3,

    /// <summary>The strategy pipeline exceeded its workflow deadline.</summary>
    TimedOut = 4,

    /// <summary>Processing ended because the workflow was cancelled.</summary>
    Cancelled = 5
}
