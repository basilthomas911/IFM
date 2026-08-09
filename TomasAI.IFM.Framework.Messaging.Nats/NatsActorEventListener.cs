using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

public class NatsActorEventListener(
    INatsEventListenerOptions options,
    ILogger logger,
    NatsConnectionManager? connectionManager = null) : IActorEventListener
{
    readonly INatsEventListenerOptions _options = IsArgumentNull.Set(options);
    readonly INatsSerializer<byte[]> _deserializer = new NatsByteArrayMessageSerializer();
    readonly ILogger  _logger = IsArgumentNull.Set(logger);
    readonly string _serviceId = "NatsActorEventListener";
    NatsConnectionManager? _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    NatsSubOpts _requestOptions = default!;
    NatsClient? _nc;
    CancellationTokenSource _cts = default!;
    EventListenerState _state = EventListenerState.Unknown;
    string _eventListenerId = string.Empty;
    Dictionary<ActorMailboxId, List<string>> _eventMap = [];
    Func<string, NatsMsg<byte[]>, ValueTask> _eventHandler = (verb, msg) => { return ValueTask.CompletedTask; };
    Task[] _subscriptionTasks = [];
    int _messageCount;

    public async ValueTask StartAsync(
        string eventListenerId,
        Dictionary<ActorMailboxId, List<string>> eventMap,
        Func<string, NatsMsg<byte[]>, ValueTask> eventHandler)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StartCoreAsync(eventListenerId, eventMap, eventHandler).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Gets the current state of the event listener.
    /// </summary>
    public EventListenerState State => _state;

    /// <summary>
    /// Gets the number of messages currently stored.
    /// </summary>
    public int MessageCount => Volatile.Read(ref _messageCount);

    /// <summary>
    /// Starts the event listener asynchronously, subscribing to the specified event map and handling incoming events
    /// using the provided event handler.
    /// </summary>
    /// <param name="eventListenerId">The unique identifier for the event listener instance. Cannot be null.</param>
    /// <param name="eventMap">A dictionary mapping each actor mailbox to a list of event verbs to subscribe to. Must contain at least one entry.</param>
    /// <param name="eventHandler">A delegate that processes incoming NATS messages. Invoked for each received event. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous start operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="eventMap"/> is empty or contains multiple mailbox keys that are not the same.</exception>
    async ValueTask StartCoreAsync(
     string eventListenerId,
     Dictionary<ActorMailboxId, List<string>> eventMap,
     Func<string, NatsMsg<byte[]>, ValueTask> eventHandler)
    {
        try
        {
            _eventListenerId = IsArgumentNull.Set(eventListenerId);

            // Add validation for empty string
            if (string.IsNullOrWhiteSpace(_eventListenerId))
            {
                throw new ArgumentException("Event listener ID cannot be empty or whitespace.", nameof(eventListenerId));
            }

            _eventMap = IsArgumentNull.Set(eventMap);
            _eventHandler = IsArgumentNull.Set(eventHandler);

            // validate arguments...
            if (_eventMap.Count == 0)
                throw new ArgumentException("Event map cannot be empty.", nameof(eventMap));
            ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
            ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
            if (_nc is not null)
            {
                _logger.LogDebug(_serviceId, "NATS Event Listener: {eventListenerId} already started.", eventListenerId);
                return;
            }

            // Create a new CancellationTokenSource for this start cycle
            _cts = new CancellationTokenSource();

            _connectionManager ??= new NatsConnectionManager();
            _nc = await _connectionManager.GetClientAsync(_options.Url).ConfigureAwait(false);
            _requestOptions = new()
            {
                ChannelOpts = new NatsSubChannelOpts
                {
                    Capacity = 4096,
                    FullMode = BoundedChannelFullMode.Wait
                }
            };
            _state = EventListenerState.Started;
            Interlocked.Exchange(ref _messageCount, 0);
            _logger.LogInformationEvent(_serviceId, "NATS Event Listener: {eventListenerId} started.", eventListenerId);
            _subscriptionTasks = new Task[_eventMap.Count];
            var taskIndex = 0;
            foreach (var e in _eventMap)
            {
                _subscriptionTasks[taskIndex++] = PubSubMessageLoopAsync($"{e.Key}", e.Value, _cts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _serviceId, "NATS Event Listener: {eventListenerId} failed during startup.", eventListenerId);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously stops the event listener and releases associated resources.
    /// </summary>
    /// <remarks>If the event listener has not been started, this method completes without performing any
    /// action. After calling this method, the event listener cannot be used until it is started again.</remarks>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async ValueTask StopAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    async ValueTask StopCoreAsync()
    {
        try
        {
            if (_nc is null)
            {
                _logger.LogDebug(_serviceId, "NATS Event Listener: {eventListenerId}  has not started.", _eventListenerId);
                return;
            }

            _cts.Cancel();
            if (_subscriptionTasks.Length > 0)
            {
                try
                {
                    await Task.WhenAll(_subscriptionTasks).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected while stopping active subscriptions.
                }
            }
            _subscriptionTasks = [];
            _cts.Dispose();
            _cts = default!;
            if (_ownsConnectionManager)
            {
                await _connectionManager!.DisposeAsync().ConfigureAwait(false);
                _connectionManager = null;
            }
            _nc = default!;
            _state = EventListenerState.Stopped;
            _logger.LogInformationEvent(_serviceId, "NATS Event Listener: {eventListenerId} stopped.", _eventListenerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _serviceId, "Failed to stop NATS Event Listener: {eventListenerId}.", _eventListenerId);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously processes incoming messages from the NATS subscription for the specified actor mailbox and event verbs.
    /// </summary>
    /// <param name="actorMailboxId">The ID of the actor mailbox to process messages for.</param>
    /// <param name="eventVerbs">The list of event verbs to listen for.</param>
    /// <param name="ctsRequestToken">A cancellation token that can be used to request termination of the message processing loop.</param>
    /// <returns>A task that represents the asynchronous operation of the message processing loop.</returns>
    async Task PubSubMessageLoopAsync(string actorMailboxId, List<string> eventVerbs, CancellationToken ctsRequestToken)
    {
        _state = EventListenerState.Running;
        _logger.LogInformationEvent(_serviceId, "NATS Event Listener: {eventListenerId}  - {actorMailboxId} running.", _eventListenerId, actorMailboxId);
        var acceptedVerbs = new HashSet<string>(eventVerbs, StringComparer.OrdinalIgnoreCase);
        try
        {
            await foreach (var msg in _nc!.SubscribeAsync($"{actorMailboxId}.>", serializer: _deserializer, opts: _requestOptions, cancellationToken: ctsRequestToken))
            {
                try
                {
                    if (msg.Data is null)
                        continue;
                    msg.EnsureSuccess();
                    Interlocked.Increment(ref _messageCount);
                    NatsMessagingMetrics.Received.Add(1);
                    var msgSubject = msg.Subject.ToSubject();
                    if (acceptedVerbs.Contains(msgSubject.Verb))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                            _logger.LogDebug("NATS Event Listener: {EventListenerId} received event for subject={Subject}", _eventListenerId, msg.Subject);
                        await _eventHandler(msgSubject.Verb, msg).ConfigureAwait(false);
                    }
                   
                }
                catch (Exception ex)
                {
                    NatsMessagingMetrics.DispatchFailures.Add(1);
                    _logger.LogErrorEvent(_serviceId, ex, "NATS Event Listener: {eventListenerId} failed to process message. ", _eventListenerId);
                }
            }
        }
        catch (OperationCanceledException) when (ctsRequestToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        _logger.LogInformationEvent(_serviceId, "NATS Event Listener: {eventListenerId} read {MessagesRead} messages.", _eventListenerId, MessageCount);
    }
}
