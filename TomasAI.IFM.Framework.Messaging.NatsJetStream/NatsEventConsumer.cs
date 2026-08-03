using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Provides a NATS-backed compatibility base for legacy domain event consumers.
/// </summary>
/// <remarks>
/// Derived consumers register event prototypes in <see cref="ConnectEvents"/>. Registrations are grouped into
/// a single NATS listener map, allowing one consumer to subscribe to any number of actor event mailboxes.
/// </remarks>
public abstract class NatsEventConsumer : NatsActorEventListener, IEventConsumer
{
    private const int DuplicateEventMapLimit = 1000;

    private static readonly ConcurrentDictionary<Guid, string> _duplicateEventMap = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> _asEventMethods = [];
    private static readonly MethodInfo _asEventMethod = typeof(ActorExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
            method.Name == nameof(ActorExtensions.AsEvent) &&
            method.IsGenericMethodDefinition &&
            method.GetGenericArguments().Length == 1 &&
            method.GetParameters().Length == 1 &&
            method.GetParameters()[0].ParameterType == typeof(NatsMsg<byte[]>));

    private readonly ILogger _logger;
    private readonly Dictionary<EventRoute, List<EventRegistration>> _registrations = [];
    private string? _listenerId;

    /// <summary>
    /// Initializes a NATS event consumer.
    /// </summary>
    /// <param name="options">NATS event-listener configuration.</param>
    /// <param name="logger">Logger used for listener and dispatch diagnostics.</param>
    protected NatsEventConsumer(INatsEventListenerOptions options, ILogger logger)
        : base(options, logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the consumer after collecting the derived consumer's event registrations.
    /// </summary>
    /// <returns>A task representing startup of the NATS subscriptions.</returns>
    public async ValueTask StartAsync()
    {
        try
        {
            _registrations.Clear();
            _listenerId = null;
            ConnectEvents();

            if (_registrations.Count == 0)
            {
                _logger.LogWarning("{ConsumerName} did not register any NATS events.", GetType().Name);
                return;
            }

            var eventMap = _registrations.Keys
                .GroupBy(route => route.MailboxId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(route => route.Verb)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList());

            await base.StartAsync(
                _listenerId ?? GetType().Name,
                eventMap,
                DispatchEventAsync).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(
                GetType().Name,
                ex,
                "StartAsync: failed to start NATS event subscriptions");
        }
    }

    /// <summary>
    /// Stops all NATS subscriptions owned by this consumer.
    /// </summary>
    /// <returns>A task representing shutdown of the NATS listener.</returns>
    public new async ValueTask StopAsync()
    {
        try
        {
            await base.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(
                GetType().Name,
                ex,
                "StopAsync: failed to stop NATS event subscriptions");
        }
    }

    /// <summary>
    /// Registers the events consumed by a derived consumer.
    /// </summary>
    protected abstract void ConnectEvents();

    /// <summary>
    /// Subscribes a synchronous callback to the supplied event prototypes.
    /// </summary>
    /// <param name="siteName">Logical listener identifier.</param>
    /// <param name="consumeEvents">Concrete event prototypes that identify actor/verb routes and payload types.</param>
    /// <param name="consumerAction">Callback invoked for each unique event.</param>
    protected void Subscribe(
        string siteName,
        ICollection<IEvent> consumeEvents,
        Action<IEvent> consumerAction)
    {
        ArgumentNullException.ThrowIfNull(consumerAction);
        Subscribe(
            siteName,
            consumeEvents,
            @event =>
            {
                consumerAction(@event);
                return ValueTask.CompletedTask;
            });
    }

    /// <summary>
    /// Subscribes an asynchronous callback to the supplied event prototypes.
    /// </summary>
    /// <param name="siteName">Logical listener identifier.</param>
    /// <param name="consumeEvents">Concrete event prototypes that identify actor/verb routes and payload types.</param>
    /// <param name="consumerAction">Asynchronous callback invoked for each unique event.</param>
    protected void Subscribe(
        string siteName,
        ICollection<IEvent> consumeEvents,
        Func<IEvent, ValueTask> consumerAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        ArgumentNullException.ThrowIfNull(consumeEvents);
        ArgumentNullException.ThrowIfNull(consumerAction);

        _listenerId ??= siteName;
        if (!string.Equals(_listenerId, siteName, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "{ConsumerName} combines subscription site {SiteName} into listener {ListenerId}.",
                GetType().Name,
                siteName,
                _listenerId);
        }

        foreach (var prototype in consumeEvents)
        {
            ArgumentNullException.ThrowIfNull(prototype);
            var route = ResolveRoute(prototype);
            if (!_registrations.TryGetValue(route, out var routeRegistrations))
            {
                routeRegistrations = [];
                _registrations.Add(route, routeRegistrations);
            }

            var eventType = prototype.GetType();
            if (routeRegistrations.Count > 0 && routeRegistrations.Any(item => item.EventType != eventType))
            {
                throw new InvalidOperationException(
                    $"NATS route '{route.MailboxId}.{route.Verb}' is ambiguous between " +
                    $"'{routeRegistrations[0].EventType.Name}' and '{eventType.Name}'.");
            }

            routeRegistrations.Add(new EventRegistration(eventType, consumerAction));
        }
    }

    private async ValueTask DispatchEventAsync(string eventVerb, NatsMsg<byte[]> eventMessage)
    {
        var subject = eventMessage.Subject.ToSubject();
        var route = new EventRoute(subject.ActorId, eventVerb);
        if (!_registrations.TryGetValue(route, out var routeRegistrations))
            return;

        var consumedEvent = DeserializeEvent(routeRegistrations[0].EventType, eventMessage);
        if (consumedEvent is null)
            return;

        // An empty identifier cannot reliably identify a duplicate. Legacy ingress events may
        // omit it, so process those messages instead of treating every subsequent one as the
        // same event.
        if (consumedEvent.Id != Guid.Empty)
        {
            if (!_duplicateEventMap.TryAdd(consumedEvent.Id, consumedEvent.GetType().Name))
                return;

            if (_duplicateEventMap.Count >= DuplicateEventMapLimit)
                _duplicateEventMap.Clear();
        }

        foreach (var registration in routeRegistrations)
            await registration.ConsumerAction(consumedEvent).ConfigureAwait(false);
    }

    private static IEvent? DeserializeEvent(Type eventType, NatsMsg<byte[]> eventMessage)
    {
        try
        {
            var deserializeMethod = _asEventMethods.GetOrAdd(
                eventType,
                static type => _asEventMethod.MakeGenericMethod(type));
            return deserializeMethod.Invoke(null, [eventMessage]) as IEvent;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static EventRoute ResolveRoute(IEvent prototype)
    {
        if (NatsEventProducer.IsValid(prototype.Subject, requireEntityId: false))
            return new EventRoute(prototype.Subject.ActorId, prototype.Subject.Verb);

        var eventType = prototype.GetType();
        var actor = NatsEventProducer.GetPublicStaticRouteValue(eventType, "Actor");
        var verb = NatsEventProducer.GetPublicStaticRouteValue(eventType, "Verb");
        if (string.IsNullOrWhiteSpace(actor))
            throw new InvalidOperationException($"{eventType.Name} does not expose a valid public static Actor route.");
        if (string.IsNullOrWhiteSpace(verb))
            throw new InvalidOperationException($"{eventType.Name} does not expose a valid public static Verb route.");

        return new EventRoute(new ActorMailboxId(ActorType.Event, actor), verb);
    }

    private readonly record struct EventRoute(ActorMailboxId MailboxId, string Verb)
    {
        public bool Equals(EventRoute other)
            => MailboxId == other.MailboxId &&
               string.Equals(Verb, other.Verb, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
            => HashCode.Combine(MailboxId, StringComparer.OrdinalIgnoreCase.GetHashCode(Verb));
    }

    private sealed record EventRegistration(
        Type EventType,
        Func<IEvent, ValueTask> ConsumerAction);
}
