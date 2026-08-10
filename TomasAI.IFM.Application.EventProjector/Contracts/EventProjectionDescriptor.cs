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
        bool publishProcessingEvent = true)
    {
        ArgumentNullException.ThrowIfNull(sourceEventType);
        if (!typeof(IEvent).IsAssignableFrom(sourceEventType))
            throw new ArgumentException($"{sourceEventType.FullName} does not implement {nameof(IEvent)}.", nameof(sourceEventType));
        if (idempotencyStrategy == EventProjectionIdempotencyStrategy.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(idempotencyStrategy));

        SourceEventType = sourceEventType;
        IdempotencyStrategy = idempotencyStrategy;
        ApplyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
        CompletedEventFactory = completedEventFactory ?? throw new ArgumentNullException(nameof(completedEventFactory));
        FailedEventFactory = failedEventFactory ?? throw new ArgumentNullException(nameof(failedEventFactory));
        PublishProcessingEvent = publishProcessingEvent;
    }

    public Type SourceEventType { get; }
    public EventProjectionIdempotencyStrategy IdempotencyStrategy { get; }
    public Func<IEvent, ProjectionExecutionContext, ValueTask<EventProjectionApplyResult>> ApplyAsync { get; }
    public Func<IEvent, ICompleteEvent?> CompletedEventFactory { get; }
    public Func<IEvent, Exception, IErrorEvent?> FailedEventFactory { get; }
    public bool PublishProcessingEvent { get; }
}
