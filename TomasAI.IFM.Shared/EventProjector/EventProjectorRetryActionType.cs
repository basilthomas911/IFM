using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Specifies the recovery action to take after an event-projection execution does not complete
/// normally.
/// </summary>
public enum EventProjectorRetryActionType : byte
{
    /// <summary>
    /// No retry or recovery action is required.
    /// </summary>
    None = 0,

    /// <summary>
    /// Retry the stage at which the previous execution failed.
    /// </summary>
    RetryCurrentStage = 1,

    /// <summary>
    /// Resume processing at the stage immediately following the last successfully completed stage.
    /// </summary>
    ResumeFromNextStage = 2,

    /// <summary>
    /// Restart the projection workflow from its initial stage.
    /// </summary>
    RestartWorkflow = 3,

    /// <summary>
    /// Move the source event to a durable replay queue for later processing.
    /// </summary>
    MoveToReplayQueue = 4,

    /// <summary>
    /// Suspend automated processing until an operator resolves the failure.
    /// </summary>
    AwaitManualResolution = 5,

    /// <summary>
    /// Do not retry because a newer execution or event has superseded this work.
    /// </summary>
    DiscardAsSuperseded = 6
}
