using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Specifies a stage in the event-projection workflow.
/// </summary>
/// <remarks>
/// The numeric values describe the normal processing order and are suitable for recording progress
/// and selecting a resume point.
/// </remarks>
public enum EventProjectorStageType : byte
{
    /// <summary>
    /// No workflow stage has been selected or completed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Validate the source event and the projection execution context.
    /// </summary>
    ValidateSourceEvent = 1,

    /// <summary>
    /// Publish an event indicating that projection processing has begun.
    /// </summary>
    PublishProcessingEvent = 2,

    /// <summary>
    /// Apply the source event to the target projection.
    /// </summary>
    ApplyProjection = 3,

    /// <summary>
    /// Publish an event indicating that the projection completed successfully.
    /// </summary>
    PublishCompletedEvent = 4,

    /// <summary>
    /// Publish an event describing a projection failure.
    /// </summary>
    PublishFailedEvent = 5,

    /// <summary>
    /// Persist final workflow-completion state.
    /// </summary>
    PersistCompletion = 6,

    /// <summary>
    /// The entire projection workflow has completed.
    /// </summary>
    Completed = 7
}
