using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector.Realtime.Contracts;

/// <summary>
/// Defines the immutable source, update, complete, and failure operations for one
/// no-replay realtime projection.
/// </summary>
public sealed record RealtimeProjectionDescriptor
{
    public RealtimeProjectionDescriptor(
        Type sourceEventType,
        Func<IEvent, CancellationToken, ValueTask> applyAsync,
        Func<IEvent, ICompleteEvent> completedEventFactory,
        Func<IEvent, Exception, IErrorEvent> failedEventFactory)
    {
        ArgumentNullException.ThrowIfNull(sourceEventType);
        if (!typeof(IEvent).IsAssignableFrom(sourceEventType))
        {
            throw new ArgumentException(
                $"{sourceEventType.FullName} does not implement {nameof(IEvent)}.",
                nameof(sourceEventType));
        }

        SourceEventType = sourceEventType;
        ApplyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
        CompletedEventFactory = completedEventFactory
            ?? throw new ArgumentNullException(nameof(completedEventFactory));
        FailedEventFactory = failedEventFactory
            ?? throw new ArgumentNullException(nameof(failedEventFactory));
    }

    public Type SourceEventType { get; }
    public Func<IEvent, CancellationToken, ValueTask> ApplyAsync { get; }
    public Func<IEvent, ICompleteEvent> CompletedEventFactory { get; }
    public Func<IEvent, Exception, IErrorEvent> FailedEventFactory { get; }
}
