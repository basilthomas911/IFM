using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Specifies the final outcome of an event-projection execution.
/// </summary>
public enum EventProjectorOutcomeType : byte
{
    /// <summary>
    /// The projection workflow is still in progress.
    /// </summary>
    Processing = 0,

    /// <summary>
    /// The projection workflow completed successfully.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The projection failed but is eligible for automatic retry.
    /// </summary>
    Retrying = 2,

    /// <summary>
    /// The projection failed and should not be retried automatically.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Processing was cancelled before the projection workflow completed.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// The execution was replaced by a newer or otherwise authoritative projection execution.
    /// </summary>
    Superseded = 5,

    /// <summary>
    /// The projection had already completed before this execution attempt.
    /// </summary>
    AlreadyCompleted = 6
}
