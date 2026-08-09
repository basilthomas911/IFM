using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Represents a NATS JetStream consumer that subscribes to and processes actor event messages
/// with durable, at-least-once delivery guarantees.
/// </summary>
/// <remarks>
/// This class manages the lifecycle of a NATS JetStream consumer that subscribes to event messages
/// for a given <see cref="ActorType.Event"/>. Incoming messages are deserialized and dispatched to the
/// appropriate actor's mailbox for processing. Unlike <see cref="NatsActorConsumer"/>, this consumer
/// uses JetStream for durable message delivery and acknowledges each message after successful processing.
/// It is designed exclusively for actor event messages (publish-subscribe pattern).
/// It can be started and stopped asynchronously, and its running state can be queried via the
/// <see cref="IsRunning"/> property.
/// </remarks>
/// <param name="options">The NATS JetStream consumer options containing connection and stream configuration.</param>
/// <param name="logger">The logger instance used to record diagnostic and lifecycle events.</param>
public class NatsJetStreamActorConsumer(
    INatsJetStreamConsumerOptions options,
    ILogger logger,
    NatsConnectionManager? connectionManager = null)
    : IJSActorConsumer
{
    readonly INatsJetStreamConsumerOptions _options = IsArgumentNull.Set(options);
    readonly INatsSerializer<byte[]> _deserializer = new NatsByteArrayMessageSerializer();
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly string _serviceId = "NatsJetStreamActorConsumer";
    readonly NatsConnectionManager _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    IActorSupervisor _supervisor = default!;
    ActorType _actorType;

    NatsClient? _nc;
    NatsJSConsumeOpts _consumerOpts;
    CancellationTokenSource _cts = new();
    Task? _loopTask;
    bool _isRunning;

    // striped dispatch channels for concurrent mailbox delivery with deferred ACK
    Channel<(NatsMsg<byte[]> Msg, ActorSubject Subject, INatsJSMsg<byte[]>? JsMsg, bool IsRoutedMessage)>[]? _stripeChannels;
    Channel<(NatsOwnedEventMessage Msg, ActorSubject Subject, EventFanoutDelivery Delivery)>[]?
        _ownedStripeChannels;
    Task[]? _dispatcherTasks;

    public async ValueTask StartAsync(IActorSupervisor supervisor, ActorType actorType, string consumerName = default!)
        => await StartAsync(supervisor, actorType, consumerName, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StartAsync(
        IActorSupervisor supervisor,
        ActorType actorType,
        string consumerName,
        CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StartCoreAsync(supervisor, actorType, consumerName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Starts the NATS JetStream consumer for the specified actor type and begins processing
    /// actor event messages routed through the given supervisor.
    /// </summary>
    /// <remarks>
    /// Initializes the NATS client connection, creates or updates the JetStream stream for the
    /// actor type, creates a durable consumer, and starts a background message loop.
    /// If the consumer is already started, the call is a no-op.
    /// <para>
    /// This consumer is designed exclusively for event actor types. All incoming messages are
    /// deserialized and posted to the matching actor's mailbox located via
    /// <see cref="IActorSupervisor.Children"/>. Each message is acknowledged after successful processing.
    /// The background loop runs until <see cref="StopAsync"/> is called.
    /// </para>
    /// </remarks>
    /// <param name="supervisor">The actor supervisor whose children contain the target actor mailboxes.</param>
    /// <param name="actorType">The actor type that determines the JetStream stream and subject pattern.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the consumer has been started.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a received message targets an actor identifier that
    /// does not exist in the supervisor's <see cref="IActorSupervisor.Children"/> collection.</exception>
    async ValueTask StartCoreAsync(
        IActorSupervisor supervisor,
        ActorType actorType,
        string consumerName,
        CancellationToken cancellationToken)
    {
        try
        {
            ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
            ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
            _supervisor = IsArgumentNull.Set(supervisor);
            _actorType = actorType;
            if (_nc is not null)
            {
                _logger.LogDebug("NATS JetStream {ActorType} consumer already started.", _actorType);
                return;
            }

            _nc = await _connectionManager.GetClientAsync(_options.Url, cancellationToken).ConfigureAwait(false);
            var js = await _connectionManager.GetJetStreamContextAsync(_options.Url, cancellationToken).ConfigureAwait(false);
            var streamName = string.IsNullOrWhiteSpace(_options.StreamName)
                ? $"{_actorType}Stream"
                : _options.StreamName;

            var durableName = !string.IsNullOrWhiteSpace(consumerName)
                ? $"{_actorType}Consumer-{consumerName}"
                : !string.IsNullOrWhiteSpace(_options.DurableConsumerName)
                    ? _options.DurableConsumerName
                    : $"{_actorType}Consumer";

            var streamSubject = $"{_actorType}.>";
            var consumerSubjectFilter = string.IsNullOrWhiteSpace(_options.FilterSubject)
                ? streamSubject
                : _options.FilterSubject;

            // A subject overlap is a configuration error. Never delete server streams implicitly:
            // another service may own the overlapping stream and its retained messages.
            await js.CreateOrUpdateStreamAsync(new StreamConfig(streamName, [streamSubject]));

            // Create or update the durable consumer...
            var consumer = await js.CreateOrUpdateConsumerAsync(streamName, new ConsumerConfig(durableName)
            {
                FilterSubject = consumerSubjectFilter,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                MaxAckPending = GetOutstandingLimit()
            });

            _consumerOpts = new()
            {
                MaxMsgs = GetMaxMessages(),
                ThresholdMsgs = GetThresholdMessages(),
                DrainOnCancel = true
            };

            _cts.Dispose();
            _cts = new CancellationTokenSource();
            var ctsRequestToken = _cts.Token;

            // create striped dispatch channels and start dispatcher tasks
            var dispatcherCount = Math.Max(1, _options.DispatcherCount);
            _dispatcherTasks = new Task[dispatcherCount];
            if (_options.UseOwnedEventPayloads)
            {
                _ownedStripeChannels =
                    new Channel<(NatsOwnedEventMessage, ActorSubject, EventFanoutDelivery)>[dispatcherCount];
                for (var i = 0; i < dispatcherCount; i++)
                {
                    _ownedStripeChannels[i] = Channel.CreateBounded<(
                        NatsOwnedEventMessage,
                        ActorSubject,
                        EventFanoutDelivery)>(new BoundedChannelOptions(GetDispatcherCapacity())
                        {
                            SingleWriter = true,
                            SingleReader = true,
                            FullMode = BoundedChannelFullMode.Wait
                        });
                    _dispatcherTasks[i] = OwnedDispatchLoopAsync(
                        _ownedStripeChannels[i].Reader);
                }
            }
            else
            {
                _logger.LogWarning(
                    "NATS JetStream event consumer is using the legacy byte[] payload path for diagnostics.");
                _stripeChannels =
                    new Channel<(NatsMsg<byte[]>, ActorSubject, INatsJSMsg<byte[]>?, bool)>[dispatcherCount];
                for (var i = 0; i < dispatcherCount; i++)
                {
                    _stripeChannels[i] = Channel.CreateBounded<(
                        NatsMsg<byte[]>,
                        ActorSubject,
                        INatsJSMsg<byte[]>?,
                        bool)>(new BoundedChannelOptions(GetDispatcherCapacity())
                        {
                            // Routed legacy messages can be written by every dispatcher.
                            SingleWriter = false,
                            SingleReader = true,
                            FullMode = BoundedChannelFullMode.Wait
                        });
                    _dispatcherTasks[i] = DispatchLoopAsync(_stripeChannels[i].Reader);
                }
            }

            _isRunning = true;
            _loopTask = RunMessageLoopAsync(consumer, ctsRequestToken);
            _logger.LogInformationEvent(_serviceId, "NATS JetStream {ActorType} consumer started with {DispatcherCount} dispatch stripes.", _actorType, dispatcherCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NATS JetStream {ActorType} failed during consumer startup.", _actorType);
            throw;
        }
    }

    /// <summary>
    /// Stops the NATS JetStream actor event consumer and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// Cancels the active message loop, disposes the NATS client, and sets <see cref="IsRunning"/> to
    /// <see langword="false"/>. No further actor event messages will be consumed after this method
    /// completes. If the consumer has not been started, the call is a no-op and a debug message is logged.
    /// </remarks>
    /// <returns>A <see cref="ValueTask"/> that completes once the consumer has been stopped and disposed.</returns>
    public async ValueTask StopAsync()
        => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    int GetDispatcherCapacity()
        => _options is NatsJetStreamConsumerOptions concrete
            ? concrete.DispatcherCapacity
            : _options.DispatcherCapacity > 0
                ? _options.DispatcherCapacity
                : NatsJetStreamConsumerOptions.ExistingDispatcherCapacity;

    int GetOutstandingLimit()
        => _options is NatsJetStreamConsumerOptions concrete
            ? concrete.GetOutstandingLimit()
            : _options.MaxAckPending > 0
                ? _options.MaxAckPending
                : checked(GetDispatcherCapacity() * Math.Max(1, _options.DispatcherCount));

    int GetMaxMessages()
        => _options is NatsJetStreamConsumerOptions concrete
            ? concrete.GetMaxMessages()
            : _options.MaxMessages > 0 ? _options.MaxMessages : GetOutstandingLimit();

    int GetThresholdMessages()
        => _options is NatsJetStreamConsumerOptions concrete
            ? concrete.GetThresholdMessages()
            : _options.ThresholdMessages > 0 ? _options.ThresholdMessages : GetDispatcherCapacity();

    async ValueTask StopCoreAsync()
    {
        try
        {
            if (_nc is null)
            {
                _logger.LogDebug("NATS JetStream {ActorType} consumer has not started.", _actorType);
                return;
            }

            _cts.Cancel();

            // Await the background loop so it observes cancellation before we dispose
            // the NATS client it is reading from, preventing use-after-dispose.
            if (_loopTask is not null)
            {
                try { await _loopTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { /* expected */ }
                catch (ObjectDisposedException)
                {
                    // A shared connection can be disposed by the host immediately before
                    // the supervisor reaches this consumer. The loop may therefore have
                    // recorded a transport-disposal fault before its token was canceled.
                    // Once StopAsync has requested cancellation, that completed fault is
                    // an expected, idempotent shutdown outcome.
                }
            }
            _loopTask = null;

            // Complete all stripe writers so dispatchers drain remaining items and exit.
            if (_stripeChannels is not null)
            {
                foreach (var ch in _stripeChannels)
                    ch.Writer.TryComplete();
            }
            if (_ownedStripeChannels is not null)
            {
                foreach (var ch in _ownedStripeChannels)
                    ch.Writer.TryComplete();
            }

            // Await all dispatcher tasks.
            if (_dispatcherTasks is not null)
            {
                try { await Task.WhenAll(_dispatcherTasks).ConfigureAwait(false); }
                catch (OperationCanceledException) { /* expected */ }
            }
            _stripeChannels = null;
            _ownedStripeChannels = null;
            _dispatcherTasks = null;

            _cts.Dispose();
            if (_ownsConnectionManager)
                await _connectionManager.DisposeAsync().ConfigureAwait(false);
            _nc = null;
            _isRunning = false;
            _logger.LogInformation("NATS JetStream {ActorType} consumer has stopped.", _actorType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop NATS JetStream {ActorType} consumer.", _actorType);
            throw;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the NATS JetStream actor event consumer loop is currently running.
    /// </summary>
    /// <value><see langword="true"/> if the consumer is actively processing actor event messages; otherwise, <see langword="false"/>.</value>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Runs the JetStream message loop for actor event messages. Continuously consumes messages
    /// from the JetStream durable consumer and dispatches them to the corresponding actor mailboxes.
    /// Each message is acknowledged after successful processing.
    /// </summary>
    /// <param name="consumer">The JetStream consumer to consume messages from.</param>
    /// <param name="ctsRequestToken">The cancellation token used to signal the loop to stop.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the loop exits.</returns>
    async Task RunMessageLoopAsync(INatsJSConsumer consumer, CancellationToken cancellationToken)
    {
        try
        {
            if (_options.UseOwnedEventPayloads)
                await OwnedJetStreamMessageLoopAsync(consumer, cancellationToken).ConfigureAwait(false);
            else
                await JetStreamMessageLoopAsync(consumer, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // NATS.Net drains buffered JetStream messages before completing cancellation.
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            // Host disposal can release the shared connection immediately after the
            // consumer token is canceled. That race is an expected shutdown outcome.
        }
        finally
        {
            _isRunning = false;
        }
    }

    async ValueTask JetStreamMessageLoopAsync(INatsJSConsumer consumer, CancellationToken ctsRequestToken)
    {
        var stripes = _stripeChannels!;
        var stripeCount = stripes.Length;
        _logger.LogInformationEvent(_serviceId, "JetStream {ActorType} consumer started", _actorType);
        while (!ctsRequestToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS JetStream {ActorType} consumer waiting for messages...", _actorType);
            var messagesRead = 0;
            await foreach (var msg in consumer.ConsumeAsync(opts: _consumerOpts, serializer: _deserializer, cancellationToken: ctsRequestToken))
            {
                try
                {
                    if (msg.Data is null)
                    {
                        await msg.AckAsync(cancellationToken: ctsRequestToken);
                        continue;
                    }
                    messagesRead++;
                    NatsMessagingMetrics.Received.Add(1);
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("NATS JetStream {ActorType} consumer received message for subject={Subject}", _actorType, msg.Subject);

                    // parse subject and route to a dispatch stripe by entity hash.
                    // Same entity always maps to the same stripe, preserving per-entity FIFO ordering.
                    var msgSubject = msg.Subject.ToSubject();
                    var primaryExists = _supervisor.ActorExists(msgSubject.ActorId);
                    var routes = _supervisor.GetEventRoutes(msgSubject.ActorTypeId);
                    if (!primaryExists && routes.IsEmpty)
                    {
                        NatsMessagingMetrics.ListenerOnlyEvents.Add(1);
                        await msg.AckAsync(cancellationToken: CancellationToken.None)
                            .ConfigureAwait(false);
                        continue;
                    }
                    var natsMsg = new NatsMsg<byte[]>(msg.Subject, msg.ReplyTo, default, default, msg.Data, msg.Connection, default);
                    if (!primaryExists)
                    {
                        foreach (var route in routes)
                        {
                            var routedSubject = new ActorSubject(
                                route.ActorType,
                                route.Name,
                                msgSubject.Verb,
                                msgSubject.EntityId);
                            var routedStripe = (routedSubject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;
                            await stripes[routedStripe].Writer.WriteAsync(
                                (natsMsg, routedSubject, null, true),
                                ctsRequestToken).ConfigureAwait(false);
                        }
                        await msg.AckAsync(cancellationToken: CancellationToken.None)
                            .ConfigureAwait(false);
                        continue;
                    }
                    var stripe = (msgSubject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;
                    await stripes[stripe].Writer.WriteAsync((natsMsg, msgSubject, msg, false), ctsRequestToken);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorEvent(_serviceId, ex, "NATS JetStream {ActorType} consumer failed to process message. ", _actorType);
                }
            }
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS JetStream {ActorType} consumer read {MessagesRead} messages.", _actorType, messagesRead);
        }
    }

    async ValueTask OwnedJetStreamMessageLoopAsync(
        INatsJSConsumer consumer,
        CancellationToken cancellationToken)
    {
        var stripes = _ownedStripeChannels!;
        var stripeCount = stripes.Length;
        _logger.LogInformationEvent(
            _serviceId,
            "JetStream {ActorType} consumer started with shared owned event payloads",
            _actorType);

        while (!cancellationToken.IsCancellationRequested)
        {
            var messagesRead = 0;
            await foreach (var msg in consumer.ConsumeAsync(
                opts: _consumerOpts,
                serializer: NatsDefaultSerializer<NatsMemoryOwner<byte>>.Default,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                NatsSharedEventPayload? payload = null;
                var ownerTransferred = false;
                try
                {
                    msg.EnsureSuccess();
                    if (msg.Data.Memory.IsEmpty)
                    {
                        await msg.AckAsync(cancellationToken: CancellationToken.None)
                            .ConfigureAwait(false);
                        continue;
                    }

                    payload = new NatsSharedEventPayload(msg.Data);
                    ownerTransferred = true;
                    messagesRead++;
                    NatsMessagingMetrics.Received.Add(1);

                    var sourceSubject = msg.Subject.ToSubject();
                    var routes = _supervisor.GetEventRoutes(sourceSubject.ActorTypeId);
                    var destinations = EventFanoutRoutes.Build(
                        sourceSubject,
                        routes,
                        _supervisor.ActorExists(sourceSubject.ActorId));

                    if (destinations.Count == 0)
                    {
                        NatsMessagingMetrics.ListenerOnlyEvents.Add(1);
                        await msg.AckAsync(cancellationToken: CancellationToken.None)
                            .ConfigureAwait(false);
                        continue;
                    }

                    var delivery = EventFanoutDelivery.Create(msg, destinations.Count);
                    foreach (var destination in destinations)
                    {
                        await ScheduleOwnedBranchAsync(
                            payload,
                            destination,
                            delivery,
                            stripes,
                            stripeCount,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    NatsMessagingMetrics.DispatchFailures.Add(1);
                    _logger.LogErrorEvent(
                        _serviceId,
                        ex,
                        "NATS JetStream {ActorType} owned event ingress failed.",
                        _actorType);
                }
                finally
                {
                    if (ownerTransferred)
                        payload!.Dispose();
                    else
                        msg.Data.Dispose();
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "NATS JetStream {ActorType} owned consumer read {MessagesRead} messages.",
                    _actorType,
                    messagesRead);
        }
    }

    async ValueTask ScheduleOwnedBranchAsync(
        NatsSharedEventPayload payload,
        ActorSubject destination,
        EventFanoutDelivery delivery,
        Channel<(NatsOwnedEventMessage Msg, ActorSubject Subject, EventFanoutDelivery Delivery)>[] stripes,
        int stripeCount,
        CancellationToken cancellationToken)
    {
        NatsOwnedEventMessage? branch = null;
        var transferred = false;
        try
        {
            branch = payload.CreateBranch(destination);
            var stripe = (destination.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;
            await stripes[stripe].Writer.WriteAsync(
                (branch, destination, delivery),
                cancellationToken).ConfigureAwait(false);
            transferred = true;
        }
        catch (Exception ex)
        {
            if (!transferred)
                branch?.Dispose();
            NatsMessagingMetrics.DispatchFailures.Add(1);
            _logger.LogErrorEvent(
                _serviceId,
                ex,
                "Failed to schedule owned event branch for {ActorId}.",
                destination.ActorId);
            try
            {
                await delivery.CompleteHandoffAsync(false).ConfigureAwait(false);
            }
            catch (Exception acknowledgementException)
            {
                _logger.LogErrorEvent(
                    _serviceId,
                    acknowledgementException,
                    "Failed to negatively acknowledge event after scheduling failure for {ActorId}.",
                    destination.ActorId);
            }
        }
    }

    async Task OwnedDispatchLoopAsync(
        ChannelReader<(NatsOwnedEventMessage Msg, ActorSubject Subject, EventFanoutDelivery Delivery)> reader)
    {
        await foreach (var (message, subject, delivery) in reader.ReadAllAsync().ConfigureAwait(false))
        {
            var accepted = false;
            try
            {
                var actor = _supervisor.Children.GetValueOrDefault(subject.ActorId)
                    ?? throw new InvalidOperationException(
                        $"Actor not found in context children for mailbox {subject.ActorId}");
                var admission = await actor.Mailbox.ThreadQueues.TryAdmitAsync(
                    message,
                    subject,
                    CancellationToken.None).ConfigureAwait(false);
                accepted = admission.Accepted;
                if (!admission.Accepted)
                    throw new InvalidOperationException(
                        $"Mailbox rejected owned JetStream event for {subject.ActorId}: "
                        + $"{admission.Reason.ToStringFast()}.");
            }
            catch (Exception ex)
            {
                if (!accepted)
                    message.Dispose();
                NatsMessagingMetrics.DispatchFailures.Add(1);
                _logger.LogErrorEvent(
                    _serviceId,
                    ex,
                    "Owned event dispatch failed for {ActorId}.",
                    subject.ActorId);
            }

            try
            {
                await delivery.CompleteHandoffAsync(accepted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                NatsMessagingMetrics.DispatchFailures.Add(1);
                _logger.LogErrorEvent(
                    _serviceId,
                    ex,
                    "JetStream ACK/NAK finalization failed for {ActorId}.",
                    subject.ActorId);
            }
        }
    }

    /// <summary>
    /// Dispatches messages from a stripe channel to the corresponding actor mailboxes. Each message is acknowledged after successful processing.
    /// </summary>
    /// <param name="reader">The channel reader for this stripe.</param>
    /// <param name="cancellationToken">The cancellation token used to signal the loop to stop.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    async Task DispatchLoopAsync(ChannelReader<(NatsMsg<byte[]> Msg, ActorSubject Subject, INatsJSMsg<byte[]>? JsMsg, bool IsRoutedMessage)> reader)
    {
        await foreach (var (msg, msgSubject, jsMsg, isRoutedMessage) in reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                var actor = _supervisor.Children.GetValueOrDefault(msgSubject.ActorId)
                    ?? throw new InvalidOperationException($"Actor not found in context children for mailbox {msgSubject.ActorId}");
                var actorMessage = new NatsActorMessage(msg);
                var admission = await actor.Mailbox.ThreadQueues.TryAdmitAsync(
                    actorMessage,
                    msgSubject,
                    CancellationToken.None).ConfigureAwait(false);
                if (!admission.Accepted)
                {
                    actorMessage.Dispose();
                    throw new InvalidOperationException(
                        $"Mailbox rejected JetStream message for {msgSubject.ActorId}: "
                        + $"{admission.Reason.ToStringFast()}.");
                }
                if (!isRoutedMessage)
                {
                    await _supervisor.RouteEventToAsync(msg).ConfigureAwait(false);
                    await jsMsg!.AckAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                NatsMessagingMetrics.DispatchFailures.Add(1);
                _logger.LogErrorEvent(_serviceId, ex, "Dispatch stripe failed to deliver JetStream message for {ActorId}.", msgSubject.ActorId);
            }
        }
    }

    /// <summary>
    /// Drains a single dispatch stripe channel and delivers each message to the routed actor's mailbox.
    /// </summary>
    /// <param name="routeToSubject">The subject to which the event should be routed.</param>
    /// <param name="msg">The NATS message containing the event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask RouteEventToAsync(ActorSubject routeToSubject, NatsMsg<byte[]> msg)
    {
        try
        {
            var stripe = (routeToSubject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % _stripeChannels.Length;
            await _stripeChannels[stripe].Writer.WriteAsync((msg, routeToSubject, null, true), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "NATS JetStream {Subject} routed event failed to process. ", routeToSubject);
        }
    }

}
