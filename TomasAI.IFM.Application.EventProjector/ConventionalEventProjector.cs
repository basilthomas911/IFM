using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Supplies the naming and descriptor conventions shared by domain command-actor projectors.
/// </summary>
public abstract class ConventionalEventProjector<TActor>(
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : BaseEventProjector<TActor>(
        durableReplayQueue,
        dbEventSource,
        blackboardService,
        logger,
        reliabilityOptions)
    where TActor : ICommandActor<TActor>
{
    public override string ActorName => typeof(TActor).Name;
    public override string ProjectorName => GetType().Name;
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";

    /// <summary>
    /// Creates a natural-key projection descriptor. Durable replay is the default and must be disabled explicitly
    /// for lifecycle start/stop events.
    /// </summary>
    protected static EventProjectionDescriptor Describe<TEvent, TComplete, TFail, TEntityId>(
        Func<TEvent, Task> applyAsync,
        bool useDurableReplay = true,
        bool publishProcessingAfterApply = false)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                await applyAsync((TEvent)domainEvent).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            domainEvent => ((TEvent)domainEvent).ToCompleteEvent<TComplete, TEntityId>(),
            (domainEvent, exception) => ((TEvent)domainEvent).ToFailEvent<TFail, TEntityId>(exception),
            useDurableReplay: useDurableReplay,
            publishProcessingAfterApply: publishProcessingAfterApply);

    /// <summary>
    /// Creates a natural-key projection descriptor whose target operation needs the stable execution identity.
    /// The context event and stream versions are persisted with the source event and therefore remain unchanged
    /// across retries and durable replay.
    /// </summary>
    protected static EventProjectionDescriptor Describe<TEvent, TComplete, TFail, TEntityId>(
        Func<TEvent, ProjectionExecutionContext, Task> applyAsync,
        bool useDurableReplay = true,
        bool publishProcessingAfterApply = false)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, context) =>
            {
                await applyAsync((TEvent)domainEvent, context).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            domainEvent => ((TEvent)domainEvent).ToCompleteEvent<TComplete, TEntityId>(),
            (domainEvent, exception) => ((TEvent)domainEvent).ToFailEvent<TFail, TEntityId>(exception),
            useDurableReplay: useDurableReplay,
            publishProcessingAfterApply: publishProcessingAfterApply);

    protected static EventProjectionDescriptor Describe<TEvent, TComplete, TFail, TEntityId>(
        Func<TEvent, ValueTask> applyAsync,
        bool useDurableReplay = true,
        bool publishProcessingAfterApply = false)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                await applyAsync((TEvent)domainEvent).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            domainEvent => ((TEvent)domainEvent).ToCompleteEvent<TComplete, TEntityId>(),
            (domainEvent, exception) => ((TEvent)domainEvent).ToFailEvent<TFail, TEntityId>(exception),
            useDurableReplay: useDurableReplay,
            publishProcessingAfterApply: publishProcessingAfterApply);

    /// <summary>
    /// Creates a source-only descriptor. The source event and action execute, but no synthetic complete or failed
    /// event is emitted. Lifecycle start/stop events must explicitly disable durable replay.
    /// </summary>
    protected static EventProjectionDescriptor DescribeNotification<TEvent, TEntityId>(
        Func<TEvent, Task>? applyAsync = null,
        bool useDurableReplay = true,
        bool publishProcessingAfterApply = false)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                if (applyAsync is not null)
                    await applyAsync((TEvent)domainEvent).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            _ => null,
            (_, _) => null,
            useDurableReplay: useDurableReplay,
            publishProcessingAfterApply: publishProcessingAfterApply,
            publishTerminalEvent: false);

    /// <summary>
    /// Creates a source-only descriptor whose target operation needs the stable replay execution identity.
    /// </summary>
    protected static EventProjectionDescriptor DescribeNotification<TEvent, TEntityId>(
        Func<TEvent, ProjectionExecutionContext, Task> applyAsync,
        bool useDurableReplay = true,
        bool publishProcessingAfterApply = false)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, context) =>
            {
                await applyAsync((TEvent)domainEvent, context).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            _ => null,
            (_, _) => null,
            useDurableReplay: useDurableReplay,
            publishProcessingAfterApply: publishProcessingAfterApply,
            publishTerminalEvent: false);

    /// <summary>Projects an already committed snapshot with one target attempt and no source-event lookup or history replay.</summary>
    /// <typeparam name="TEvent">The committed snapshot event.</typeparam><typeparam name="TEntityId">Its business identity.</typeparam>
    /// <param name="applyAsync">The single target-write attempt using the committed payload.</param>
    /// <param name="publishProcessingEvent">Whether to publish the calculated snapshot notification before its write.</param>
    /// <returns>A non-durable descriptor that never emits a synthetic persistence-completion event.</returns>
    protected static EventProjectionDescriptor DescribeSnapshot<TEvent, TEntityId>(
        Func<TEvent, CancellationToken, Task> applyAsync,
        bool publishProcessingEvent = true)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            (_, _) => throw new InvalidOperationException("Single-attempt snapshot projections do not use a durable projection context."),
            _ => null,
            (_, _) => null,
            publishProcessingEvent: publishProcessingEvent,
            useDurableReplay: false,
            publishTerminalEvent: false,
            applySnapshotAsync: async (domainEvent, token) =>
            {
                await applyAsync((TEvent)domainEvent, token).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            });

    /// <summary>
    /// Creates a durable local-only projection for a legacy untyped event. It updates the target and checkpoint but
    /// does not publish the source or a terminal event because an untyped event has no actor delivery contract.
    /// </summary>
    protected static EventProjectionDescriptor DescribeLocal<TEvent>(Func<TEvent, Task> applyAsync)
        where TEvent : class, IEvent
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                await applyAsync((TEvent)domainEvent).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            _ => null,
            (_, _) => null,
            publishProcessingEvent: false,
            useDurableReplay: true,
            publishTerminalEvent: false);
}
