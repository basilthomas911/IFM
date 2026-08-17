using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.EventProjector.Realtime;

/// <summary>
/// Provides the source/update/complete/fail lifecycle for a domain realtime
/// projector without creating any replayable projection state.
/// </summary>
/// <typeparam name="TActor">The realtime actor that owns the projector.</typeparam>
/// <remarks>
/// The owning actor mailbox provides admission, ordering, and backpressure. This
/// projector does not use an event store, JetStream process or replay queue,
/// checkpoint, outbox, retry, recovery worker, or startup replay.
/// </remarks>
public abstract class BaseRealtimeProjector<TActor>(ILogger logger)
    : IRealtimeProjector<TActor>
    where TActor : IEventActor<TActor>
{
    static readonly ConcurrentDictionary<
        Type,
        Func<IEventActorContext, IEvent, ValueTask>> EventPublishers = new();

    readonly object _descriptorLock = new();
    FrozenDictionary<Type, RealtimeProjectionDescriptor>? _descriptorMap;
    IEventActorContext? _context;

    public abstract string ActorName { get; }
    public abstract string ProjectorName { get; }
    public abstract IReadOnlyCollection<Type> ProjectedEventTypes { get; }
    public abstract IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors { get; }

    public ILogger Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));

    public IEventActorContext Context => Volatile.Read(ref _context)
        ?? throw new InvalidOperationException(
            $"Realtime projector '{ProjectorName}' has not been started.");

    public ValueTask StartAsync(
        IEventActorContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ActorId.ActorType != ActorType.Realtime)
        {
            throw new InvalidOperationException(
                $"Realtime projector '{ProjectorName}' must be owned by an "
                + $"{ActorType.Realtime} actor; received '{context.ActorId}'.");
        }

        _ = GetDescriptorMap();
        var current = Interlocked.CompareExchange(ref _context, context, null);
        if (current is not null && !ReferenceEquals(current, context))
        {
            throw new InvalidOperationException(
                $"Realtime projector '{ProjectorName}' is already started by '{current.ActorId}'.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _context, null);
        return ValueTask.CompletedTask;
    }

    public async ValueTask<bool> ProcessRealtimeEventAsync(
        IEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var context = Context;
        if (!GetDescriptorMap().TryGetValue(domainEvent.GetType(), out var descriptor))
        {
            throw new InvalidOperationException(
                $"Realtime projector '{ProjectorName}' has no descriptor for "
                + $"'{domainEvent.GetType().FullName}'.");
        }

        try
        {
            await PublishRealtimeEventAsync(context, domainEvent, cancellationToken)
                .ConfigureAwait(false);
            await descriptor.ApplyAsync(domainEvent, cancellationToken).ConfigureAwait(false);

            var completedEvent = descriptor.CompletedEventFactory(domainEvent)
                ?? throw new InvalidOperationException(
                    $"Realtime projector '{ProjectorName}' returned no complete event for "
                    + $"'{domainEvent.EventName}'.");
            await PublishRealtimeEventAsync(context, completedEvent, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await PublishFailureAsync(context, descriptor, domainEvent, exception)
                .ConfigureAwait(false);
            Logger.LogError(
                exception,
                "Realtime projection {ProjectorName} failed for event {EventName} on "
                + "{EntityId}; the observation will not be retried or replayed.",
                ProjectorName,
                domainEvent.EventName,
                domainEvent.Subject.EntityId);
            return false;
        }
    }

    /// <summary>
    /// Creates the conventional source/update/complete/fail descriptor used by a
    /// derived domain realtime projector.
    /// </summary>
    protected static RealtimeProjectionDescriptor Describe<
        TEvent,
        TComplete,
        TFail,
        TEntityId>(Func<TEvent, CancellationToken, ValueTask> applyAsync)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(applyAsync);
        return new(
            typeof(TEvent),
            (domainEvent, cancellationToken) =>
                applyAsync((TEvent)domainEvent, cancellationToken),
            domainEvent => ((TEvent)domainEvent)
                .ToCompleteEvent<TComplete, TEntityId>(),
            (domainEvent, exception) => ((TEvent)domainEvent)
                .ToFailEvent<TFail, TEntityId>(exception));
    }

    protected static RealtimeProjectionDescriptor Describe<
        TEvent,
        TComplete,
        TFail,
        TEntityId>(Func<TEvent, ValueTask> applyAsync)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(applyAsync);
        return Describe<TEvent, TComplete, TFail, TEntityId>(
            (domainEvent, _) => applyAsync(domainEvent));
    }

    protected static RealtimeProjectionDescriptor Describe<
        TEvent,
        TComplete,
        TFail,
        TEntityId>(Func<TEvent, Task> applyAsync)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(applyAsync);
        return Describe<TEvent, TComplete, TFail, TEntityId>(
            async (domainEvent, _) =>
                await applyAsync(domainEvent).ConfigureAwait(false));
    }

    async ValueTask PublishFailureAsync(
        IEventActorContext context,
        RealtimeProjectionDescriptor descriptor,
        IEvent sourceEvent,
        Exception projectionException)
    {
        try
        {
            var failedEvent = descriptor.FailedEventFactory(sourceEvent, projectionException)
                ?? throw new InvalidOperationException(
                    $"Realtime projector '{ProjectorName}' returned no failure event for "
                    + $"'{sourceEvent.EventName}'.");
            await PublishRealtimeEventAsync(context, failedEvent, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception publicationException)
        {
            Logger.LogError(
                publicationException,
                "Realtime projector {ProjectorName} could not publish the failure event "
                + "for {EventName}; the observation will not be retried or replayed.",
                ProjectorName,
                sourceEvent.EventName);
        }
    }

    FrozenDictionary<Type, RealtimeProjectionDescriptor> GetDescriptorMap()
    {
        var current = Volatile.Read(ref _descriptorMap);
        if (current is not null)
            return current;

        lock (_descriptorLock)
        {
            current = _descriptorMap;
            if (current is not null)
                return current;

            var descriptors = ProjectionDescriptors?.ToArray()
                ?? throw new InvalidOperationException(
                    $"Realtime projector '{ProjectorName}' returned null descriptors.");
            if (descriptors.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Realtime projector '{ProjectorName}' has no projection descriptors.");
            }

            var duplicate = descriptors
                .GroupBy(descriptor => descriptor.SourceEventType)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
            {
                throw new InvalidOperationException(
                    $"Realtime projector '{ProjectorName}' registers "
                    + $"'{duplicate.Key.FullName}' more than once.");
            }

            var advertisedTypes = ProjectedEventTypes?.ToHashSet()
                ?? throw new InvalidOperationException(
                    $"Realtime projector '{ProjectorName}' returned null event types.");
            var descriptorTypes = descriptors
                .Select(descriptor => descriptor.SourceEventType)
                .ToHashSet();
            if (!advertisedTypes.SetEquals(descriptorTypes))
            {
                throw new InvalidOperationException(
                    $"Realtime projector '{ProjectorName}' event types do not match "
                    + "its immutable descriptors.");
            }

            current = descriptors.ToFrozenDictionary(
                descriptor => descriptor.SourceEventType);
            Volatile.Write(ref _descriptorMap, current);
            return current;
        }
    }

    static async ValueTask PublishRealtimeEventAsync(
        IEventActorContext context,
        IEvent domainEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        domainEvent.CheckForEmptyCommandId();
        EventInitHelper.SetProperty(
            domainEvent,
            nameof(IEvent.Subject),
            domainEvent.Subject.SetActorType(ActorType.Realtime));

        var publisher = EventPublishers.GetOrAdd(
            domainEvent.GetType(),
            CreateEventPublisher);
        await publisher(context, domainEvent).ConfigureAwait(false);
    }

    static Func<IEventActorContext, IEvent, ValueTask> CreateEventPublisher(
        Type eventType)
    {
        var eventInterface = eventType.GetInterfaces().SingleOrDefault(type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEvent<>))
            ?? throw new InvalidOperationException(
                $"Event type '{eventType.FullName}' has no typed IEvent contract.");
        var entityIdType = eventInterface.GetGenericArguments()[0];
        var method = typeof(BaseRealtimeProjector<TActor>)
            .GetMethod(
                nameof(PublishTypedEventAsync),
                BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType, entityIdType);
        return (Func<IEventActorContext, IEvent, ValueTask>)method.CreateDelegate(
            typeof(Func<IEventActorContext, IEvent, ValueTask>));
    }

    static ValueTask PublishTypedEventAsync<TEvent, TEntityId>(
        IEventActorContext context,
        IEvent domainEvent)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => context.SendAsync<TEvent, TEntityId>((TEvent)domainEvent);
}
