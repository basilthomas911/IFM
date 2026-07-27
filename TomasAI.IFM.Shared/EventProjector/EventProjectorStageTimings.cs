using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Contains elapsed-time measurements for the stages of an event-projection execution.
/// </summary>
/// <remarks>
/// A duration of <see cref="TimeSpan.Zero"/> can mean that the stage did not run or that no timing
/// was recorded. Interpret each value together with the corresponding effect and stage fields on
/// <see cref="EventProjectorResult"/>.
/// </remarks>
public sealed record EventProjectorStageTimings
{
    /// <summary>
    /// Gets the time spent validating the source event and projection context.
    /// </summary>
    public TimeSpan ValidationDuration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the time spent publishing the projection-processing event.
    /// </summary>
    public TimeSpan ProcessingEventPublishDuration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the time spent applying the event projection.
    /// </summary>
    public TimeSpan ProjectionDuration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the time spent publishing the projection-completed event.
    /// </summary>
    public TimeSpan CompletedEventPublishDuration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the time spent publishing the projection-failed event.
    /// </summary>
    public TimeSpan FailedEventPublishDuration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the time spent persisting final workflow-completion state.
    /// </summary>
    public TimeSpan CompletionPersistenceDuration { get; init; } = TimeSpan.Zero;
}
