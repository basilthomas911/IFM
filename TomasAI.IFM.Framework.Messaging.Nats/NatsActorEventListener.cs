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
    NatsConnectionManager? connectionManager = null) : IActorEventListener, IAsyncDisposable
{
    readonly INatsEventListenerOptions _options = IsArgumentNull.Set(options);
    readonly INatsSerializer<byte[]> _deserializer = new NatsByteArrayMessageSerializer();
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly string _serviceId = "NatsActorEventListener";
    NatsConnectionManager? _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    int _disposeStarted;

    /// <summary>Stops owned transport activity before releasing the host lifetime gate.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;
        try { await StopAsync().ConfigureAwait(false); }
        finally { _lifecycleGate.Dispose(); }
        GC.SuppressFinalize(this);
    }

    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    NatsClient? _nc;
    CancellationTokenSource _cts = default!;
    EventListenerState _state = EventListenerState.Unknown;
    string _eventListenerId = string.Empty;
    Func<string, NatsMsg<byte[]>, ValueTask> _eventHandler = (verb, msg) => { return ValueTask.CompletedTask; };
    INatsSub<byte[]>?[] _subscriptions = [];
    Task[] _subscriptionTasks = [];
    /// <summary>Optional catch-up after transport reconnection. The initial read remains the subscriber's responsibility.</summary>
    public Func<ValueTask>? Reconnected { get; set; }
    int _messageCount;

    ValueTask IActorEventListener.StartAsync(string id, Dictionary<ActorMailboxId, List<string>> routes,
        Func<string, IActorMessage, ValueTask> handler)
        => StartAsync(id, routes, async (subject, message) =>
        {
            using var adapter = new NatsActorMessage(message);
            await handler(subject, adapter).ConfigureAwait(false);
        });

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "This transport boundary keeps ownership transfer, cancellation, and cleanup in one lexical scope so admission cannot outlive its cleanup contract.")]
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

            eventMap = IsArgumentNull.Set(eventMap);
            _eventHandler = IsArgumentNull.Set(eventHandler);

            // validate arguments...
            if (eventMap.Count == 0)
                throw new ArgumentException("Event map cannot be empty.", nameof(eventMap));
            ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
            global::TomasAI.IFM.Framework.Messaging.Nats.NatsActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
            if (_nc is not null)
            {
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write17(_logger, null, nameof(NatsActorEventListener), nameof(StartCoreAsync), eventListenerId);
                return;
            }

            // Create a new CancellationTokenSource for this start cycle
            _cts = new CancellationTokenSource();

            _connectionManager ??= new NatsConnectionManager();
            _nc = await _connectionManager.GetClientAsync(_options.Url).ConfigureAwait(false);
            if (Reconnected is not null)
                _nc.Connection.ConnectionOpened += async (_, _) =>
                {
                    if (_state != EventListenerState.Started) return;
                    try { if (Reconnected is { } catchUp) await catchUp().ConfigureAwait(false); }
                    catch (Exception error) { _logger.LogError(error, "Event listener reconnect catch-up failed for {Listener}", _eventListenerId); }
                };
            var requestOptions = new NatsSubOpts()
            {
                ChannelOpts = new NatsSubChannelOpts
                {
                    Capacity = 4096,
                    FullMode = BoundedChannelFullMode.Wait
                }
            };
            _state = EventListenerState.Started;
            Interlocked.Exchange(ref _messageCount, 0);
            _logger.LogInformationEvent(_serviceId,"{Component}.{Method} "+"NATS Event Listener: {eventListenerId} started.",nameof(NatsActorEventListener),nameof(StartCoreAsync),eventListenerId);
            _subscriptions = new INatsSub<byte[]>?[eventMap.Count];
            _subscriptionTasks = new Task[eventMap.Count];
            var taskIndex = 0;
            foreach (var e in eventMap)
            {
                var subscription = await _nc.Connection.SubscribeCoreAsync(
                    $"{e.Key}.>",
                    queueGroup: null,
                    serializer: _deserializer,
                    opts: requestOptions,
                    cancellationToken: _cts.Token).ConfigureAwait(false);
                _subscriptions[taskIndex] = subscription;
                _subscriptionTasks[taskIndex] = PubSubMessageLoopAsync(
                    $"{e.Key}",
                    e.Value,
                    subscription,
                    _cts.Token);
                taskIndex++;
            }
        }
        catch (Exception ex)
        {
            NatsAdditionalDiagnosticLog.StartFailed(_logger, ex, _serviceId, eventListenerId);
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
                global::TomasAI.IFM.Framework.Messaging.NatsJetStream.PersistenceDiagnosticLog.Write18(_logger, null, nameof(NatsActorEventListener), nameof(StopCoreAsync), _eventListenerId);
                return;
            }

            await _cts.CancelAsync().ConfigureAwait(false);
            foreach (var subscription in _subscriptions.Where(subscription => subscription is not null))
            {
                await subscription!.UnsubscribeAsync().ConfigureAwait(false);
            }
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
            foreach (var subscription in _subscriptions.Where(subscription => subscription is not null))
            {
                await subscription!.DisposeAsync().ConfigureAwait(false);
            }
            _subscriptions = [];
            _cts.Dispose();
            _cts = default!;
            if (_ownsConnectionManager)
            {
                await _connectionManager!.DisposeAsync().ConfigureAwait(false);
                _connectionManager = null;
            }
            _nc = default!;
            _state = EventListenerState.Stopped;
            _logger.LogInformationEvent(_serviceId,"{Component}.{Method} "+"NATS Event Listener: {eventListenerId} stopped.",nameof(NatsActorEventListener),nameof(StopCoreAsync),_eventListenerId);
        }
        catch (Exception ex)
        {
            NatsAdditionalDiagnosticLog.StopFailed(_logger, ex, _serviceId, _eventListenerId);
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The transport boundary records delivery failures or requests redelivery; one failed delivery or shutdown cleanup must not terminate unrelated messaging.")]
    async Task PubSubMessageLoopAsync(
        string actorMailboxId,
        List<string> eventVerbs,
        INatsSub<byte[]> subscription,
        CancellationToken ctsRequestToken)
    {
        _state = EventListenerState.Running;
        _logger.LogInformationEvent(_serviceId,"{Component}.{Method} "+"NATS Event Listener: {eventListenerId}  - {actorMailboxId} running.",nameof(NatsActorEventListener),nameof(PubSubMessageLoopAsync),_eventListenerId,actorMailboxId);
        var acceptedVerbs = new HashSet<string>(eventVerbs, StringComparer.OrdinalIgnoreCase);
        try
        {
            await foreach (var msg in subscription.Msgs.ReadAllAsync(ctsRequestToken).ConfigureAwait(false))
            {
                try
                {
                    if (msg.Data is null)
                        continue;
                    msg.EnsureSuccess();
                    Interlocked.Increment(ref _messageCount);
                    NatsMessagingMetrics.Received.Add(1);
                    NatsMessagingMetrics.RecordLegacyPayloadCopy(msg.Data.Length);
                    var msgSubject = msg.Subject.ToSubject();
                    if (acceptedVerbs.Contains(msgSubject.Verb))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                            NatsAdditionalDiagnosticLog.ListenerReceived(_logger,nameof(NatsActorEventListener),nameof(PubSubMessageLoopAsync),_eventListenerId,msg.Subject);
                        await _eventHandler(msgSubject.Verb, msg).ConfigureAwait(false);
                    }

                }
                catch (Exception ex)
                {
                    NatsMessagingMetrics.DispatchFailures.Add(1);
                    _logger.LogErrorEvent(_serviceId,ex,"{Component}.{Method} "+"NATS Event Listener: {eventListenerId} failed to process message. ",nameof(NatsActorEventListener),nameof(PubSubMessageLoopAsync),_eventListenerId);
                }
            }
        }
        catch (OperationCanceledException) when (ctsRequestToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        catch (Exception) when (ctsRequestToken.IsCancellationRequested)
        {
            // Explicit unsubscribe completes the NATS channel and may surface its terminal
            // exception instead of OperationCanceledException. It is still expected shutdown.
        }
        _logger.LogInformationEvent(_serviceId,"{Component}.{Method} "+"NATS Event Listener: {eventListenerId} read {MessagesRead} messages.",nameof(NatsActorEventListener),nameof(PubSubMessageLoopAsync),_eventListenerId,MessageCount);
    }
}
