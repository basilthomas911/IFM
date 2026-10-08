using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector.Contracts;

/// <summary>
/// Immutable reliability contract for one source event type handled by a projector.
/// </summary>
public sealed record EventProjectionDescriptor
{
    public EventProjectionDescriptor(
        Type sourceEventType,
        EventProjectionIdempotencyStrategy idempotencyStrategy,
        Func<IEvent, ProjectionExecutionContext, ValueTask<EventProjectionApplyResult>> applyAsync,
        Func<IEvent, ICompleteEvent?> completedEventFactory,
        Func<IEvent, Exception, IErrorEvent?> failedEventFactory,
        bool publishProcessingEvent = true,
        bool useDurableReplay = true,
        bool publishProcessingAfterApply = false,
        bool publishTerminalEvent = true,
        Func<IEvent, CancellationToken, ValueTask<EventProjectionApplyResult>>? applySnapshotAsync = null)
    {
        ArgumentNullException.ThrowIfNull(sourceEventType);
        if (!typeof(IEvent).IsAssignableFrom(sourceEventType))
            throw new ArgumentException($"{sourceEventType.FullName} does not implement {nameof(IEvent)}.", nameof(sourceEventType));
        if (idempotencyStrategy == EventProjectionIdempotencyStrategy.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(idempotencyStrategy));

        if (applySnapshotAsync is not null && (useDurableReplay || publishTerminalEvent))
            throw new ArgumentException("Single-attempt snapshot projections cannot use durable replay or claim persistence completion.", nameof(applySnapshotAsync));
        ApplySnapshotAsync = applySnapshotAsync;
        SourceEventType = sourceEventType;
        IdempotencyStrategy = idempotencyStrategy;
        ApplyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
        CompletedEventFactory = completedEventFactory ?? throw new ArgumentNullException(nameof(completedEventFactory));
        FailedEventFactory = failedEventFactory ?? throw new ArgumentNullException(nameof(failedEventFactory));
        PublishProcessingEvent = publishProcessingEvent;
        UseDurableReplay = useDurableReplay;
        PublishProcessingAfterApply = publishProcessingAfterApply;
        PublishTerminalEvent = publishTerminalEvent;
    }

    public Type SourceEventType { get; }
    public EventProjectionIdempotencyStrategy IdempotencyStrategy { get; }
    public Func<IEvent, ProjectionExecutionContext, ValueTask<EventProjectionApplyResult>> ApplyAsync { get; }
    public Func<IEvent, ICompleteEvent?> CompletedEventFactory { get; }
    public Func<IEvent, Exception, IErrorEvent?> FailedEventFactory { get; }
    /// <summary>Gets a one-attempt snapshot projection action that uses the already committed source payload directly.</summary>
    /// <remarks>The repository commits the source event before submission. Projection does not reread or validate that event or reconcile it with target history.</remarks>
    public Func<IEvent, CancellationToken, ValueTask<EventProjectionApplyResult>>? ApplySnapshotAsync { get; }
    public bool PublishProcessingEvent { get; }
    /// <summary>
    /// Gets whether this event type uses the durable JetStream process/replay workflow. When false, the event is
    /// executed once through the projector's non-durable in-memory queue.
    /// </summary>
    public bool UseDurableReplay { get; init; }
    /// <summary>Gets whether the source event is published only after its target mutation succeeds.</summary>
    public bool PublishProcessingAfterApply { get; init; }
    /// <summary>
    /// Gets whether a corresponding complete or failed event is published. Source-only notifications become
    /// terminal immediately after their action completes.
    /// </summary>
    public bool PublishTerminalEvent { get; init; }
}
