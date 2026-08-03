using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Provides a NATS-backed compatibility base for the domain event producers that implement
/// <see cref="IEventProducer"/>.
/// </summary>
/// <remarks>
/// The parameterless constructor intentionally leaves transport publishing disabled for BDD tests.
/// The standard constructor publishes through core NATS. The JetStream-enabled constructor also
/// publishes entity-scoped events through JetStream so legacy producers can retain the same durable
/// event path as actor event contexts.
/// </remarks>
public abstract class NatsEventProducer : IEventProducer
{
    private static readonly ConcurrentDictionary<(Type EventType, Type EntityIdType), MethodInfo>
        _jetStreamSendMethods = [];

    private static readonly MethodInfo _jetStreamSendMethod = typeof(NatsJetStreamActorProducer)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method =>
            method.Name == nameof(NatsJetStreamActorProducer.SendAsync) &&
            method.IsGenericMethodDefinition &&
            method.GetGenericArguments().Length == 2 &&
            method.GetParameters().Length == 2);

    private readonly NatsActorProducer? _producer;
    private readonly NatsJetStreamActorProducer? _jetStreamProducer;
    private readonly ILogger? _logger;
    private readonly string _producerName;
    private readonly SemaphoreSlim _producerStartLock = new(1, 1);
    private readonly SemaphoreSlim _jetStreamStartLock = new(1, 1);

    /// <summary>
    /// Initializes a transport-free producer for BDD and unit-test use.
    /// </summary>
    public NatsEventProducer()
    {
        _producerName = GetType().Name;
    }

    /// <summary>
    /// Initializes a compatibility producer that publishes events through core NATS.
    /// </summary>
    /// <param name="options">Core NATS producer configuration.</param>
    /// <param name="logger">Logger used for transport diagnostics.</param>
    public NatsEventProducer(INatsProducerOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerName = GetType().Name;
        _producer = new NatsActorProducer(options, logger);
        _logger.LogInformationEvent(_producerName, "successfully initialized");
    }

    /// <summary>
    /// Initializes a compatibility producer that publishes through both core NATS and NATS JetStream.
    /// </summary>
    /// <param name="options">Core NATS producer configuration.</param>
    /// <param name="jetStreamOptions">NATS JetStream producer configuration.</param>
    /// <param name="loggerFactory">Factory used to create transport-specific loggers.</param>
    public NatsEventProducer(
        INatsProducerOptions options,
        INatsJetStreamProducerOptions jetStreamOptions,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jetStreamOptions);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _producerName = GetType().Name;
        _logger = loggerFactory.CreateLogger(_producerName);
        _producer = new NatsActorProducer(options, loggerFactory.CreateLogger<NatsActorProducer>());
        _jetStreamProducer = new NatsJetStreamActorProducer(
            jetStreamOptions,
            loggerFactory.CreateLogger<NatsJetStreamActorProducer>());
        _logger.LogInformationEvent(_producerName, "successfully initialized");
    }

    /// <summary>
    /// Posts an event to its NATS actor route.
    /// </summary>
    /// <param name="event">Event to publish.</param>
    /// <returns>A task representing the publish operation.</returns>
    public abstract Task PostEventAsync(IEvent @event);

    /// <summary>
    /// Sends an event to the route supplied by the event or derived from its public
    /// <c>Actor</c>/<c>Verb</c> constants and the supplied event key.
    /// </summary>
    /// <typeparam name="TKey">Event key type.</typeparam>
    /// <typeparam name="TEvent">Concrete event type.</typeparam>
    /// <param name="eventKey">Key used as the actor subject entity identifier.</param>
    /// <param name="eventValue">Event to publish.</param>
    /// <returns>A task representing the publish operation.</returns>
    protected Task SendEventAsync<TKey, TEvent>(TKey eventKey, TEvent eventValue)
        where TEvent : class, IEvent
        => PublishEventAsync("SendEventAsync", eventKey, eventValue);

    /// <summary>
    /// Streams an event to the route supplied by the event or derived from its public
    /// <c>Actor</c>/<c>Verb</c> constants and the supplied event key.
    /// </summary>
    /// <typeparam name="TKey">Event key type.</typeparam>
    /// <typeparam name="TEvent">Concrete event type.</typeparam>
    /// <param name="eventKey">Key used as the actor subject entity identifier.</param>
    /// <param name="eventValue">Event to publish.</param>
    /// <returns>A task representing the publish operation.</returns>
    protected Task StreamEventAsync<TKey, TEvent>(TKey eventKey, TEvent eventValue)
        where TEvent : class, IEvent
        => PublishEventAsync("StreamEventAsync", eventKey, eventValue);

    private async Task PublishEventAsync<TKey, TEvent>(string operation, TKey eventKey, TEvent eventValue)
        where TEvent : class, IEvent
    {
        // Parameterless construction is the established transport-free BDD test mode.
        if (_producer is null)
            return;

        ActorSubject subject = default;
        try
        {
            subject = PrepareEvent(operation, eventKey, eventValue);
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent(
                _producerName,
                ex,
                "{Operation}: failed to prepare event {EventName}",
                operation,
                eventValue?.GetType().Name ?? "Unknown");
            return;
        }

        try
        {
            await EnsureProducerStartedAsync(subject.ActorId).ConfigureAwait(false);
            await _producer.SendAsync<TEvent>(subject, eventValue).ConfigureAwait(false);
            _logger?.LogInformationEvent(_producerName, "produce: {Subject}", subject);
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent(
                _producerName,
                ex,
                "{Operation}: failed to publish event {EventName} to core NATS subject {Subject}",
                operation,
                eventValue.EventName ?? eventValue.GetType().Name,
                subject);
        }

        if (_jetStreamProducer is null)
            return;

        try
        {
            if (!TryGetEntityIdType(eventValue.GetType(), out var entityIdType))
            {
                _logger?.LogDebug(
                    "{ProducerName}: event {EventName} is not entity-scoped and was published to core NATS only.",
                    _producerName,
                    eventValue.GetType().Name);
                return;
            }

            await EnsureJetStreamProducerStartedAsync(subject.ActorId).ConfigureAwait(false);

            var sendMethod = _jetStreamSendMethods.GetOrAdd(
                (eventValue.GetType(), entityIdType),
                static key => _jetStreamSendMethod.MakeGenericMethod(key.EventType, key.EntityIdType));
            var sendTask = (ValueTask)sendMethod.Invoke(
                _jetStreamProducer,
                [subject, eventValue])!;
            await sendTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is TargetInvocationException { InnerException: not null } invocationException)
                ex = invocationException.InnerException;

            _logger?.LogErrorEvent(
                _producerName,
                ex,
                "{Operation}: failed to publish event {EventName} to JetStream subject {Subject}",
                operation,
                eventValue.EventName ?? eventValue.GetType().Name,
                subject);
        }
    }

    private async ValueTask EnsureProducerStartedAsync(ActorMailboxId mailboxId)
    {
        if (_producer!.IsRunning)
            return;

        await _producerStartLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_producer.IsRunning)
                await _producer.StartAsync(mailboxId).ConfigureAwait(false);
        }
        finally
        {
            _producerStartLock.Release();
        }
    }

    private async ValueTask EnsureJetStreamProducerStartedAsync(ActorMailboxId mailboxId)
    {
        if (_jetStreamProducer!.IsRunning)
            return;

        await _jetStreamStartLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_jetStreamProducer.IsRunning)
                await _jetStreamProducer.StartAsync(mailboxId).ConfigureAwait(false);
        }
        finally
        {
            _jetStreamStartLock.Release();
        }
    }

    internal static ActorSubject PrepareEvent<TKey>(string operation, TKey eventKey, IEvent eventValue)
    {
        ArgumentNullException.ThrowIfNull(eventValue);
        if (string.IsNullOrWhiteSpace(eventValue.EventSource))
        {
            throw new InvalidOperationException(
                $"{operation}: {eventValue.GetType().Name}.EventSource is empty");
        }

        eventValue.CheckForEmptyCommandId();
        var subject = ResolveSubject(eventValue, eventKey);
        EventInitHelper.SetProperty(eventValue, nameof(IEvent.Subject), subject);
        if (eventValue.Id == Guid.Empty)
            EventInitHelper.SetProperty(eventValue, nameof(IEvent.Id), Guid.NewGuid());
        if (eventValue.ReceivedOn == default)
            EventInitHelper.SetProperty(eventValue, nameof(IEvent.ReceivedOn), DateTime.UtcNow);
        return subject;
    }

    internal static ActorSubject ResolveSubject<TKey>(IEvent @event, TKey eventKey)
    {
        if (IsValid(@event.Subject, requireEntityId: true))
            return @event.Subject;

        var eventType = @event.GetType();
        var actor = GetPublicStaticRouteValue(eventType, "Actor");
        var verb = GetPublicStaticRouteValue(eventType, "Verb");
        var entityId = eventKey is IActorEntityId actorEntityId
            ? actorEntityId.Format()
            : Convert.ToString(eventKey, CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(actor))
            throw new InvalidOperationException($"{eventType.Name} does not expose a valid public static Actor route.");
        if (string.IsNullOrWhiteSpace(verb))
            throw new InvalidOperationException($"{eventType.Name} does not expose a valid public static Verb route.");
        if (string.IsNullOrWhiteSpace(entityId))
            throw new InvalidOperationException($"{eventType.Name} event key is empty.");

        return new ActorSubject(ActorType.Event, actor, verb, entityId);
    }

    internal static bool IsValid(ActorSubject subject, bool requireEntityId)
        => subject.ActorType != ActorType.Default &&
           !string.IsNullOrWhiteSpace(subject.Name) &&
           !subject.Name.Equals("none", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(subject.Verb) &&
           !subject.Verb.Equals("none", StringComparison.OrdinalIgnoreCase) &&
           (!requireEntityId ||
            (!string.IsNullOrWhiteSpace(subject.EntityId) &&
             !subject.EntityId.Equals("none", StringComparison.OrdinalIgnoreCase)));

    internal static string? GetPublicStaticRouteValue(Type eventType, string memberName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        if (eventType.GetField(memberName, flags)?.GetValue(null) is string fieldValue)
            return fieldValue;
        if (eventType.GetProperty(memberName, flags)?.GetValue(null) is string propertyValue)
            return propertyValue;
        return null;
    }

    private static bool TryGetEntityIdType(Type eventType, out Type entityIdType)
    {
        var entityEventContract = eventType.GetInterfaces().FirstOrDefault(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEvent<>));
        entityIdType = entityEventContract?.GetGenericArguments()[0] ?? default!;
        return entityEventContract is not null;
    }
}
