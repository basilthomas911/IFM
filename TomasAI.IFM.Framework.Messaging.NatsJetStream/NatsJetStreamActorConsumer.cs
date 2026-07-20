using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging.Nats.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats;

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
public class NatsJetStreamActorConsumer(INatsJetStreamConsumerOptions options, ILogger logger)
    : IJSActorConsumer
{
    const int DefaultStripeCapacity = 4096;

    readonly INatsJetStreamConsumerOptions _options = IsArgumentNull.Set(options);
    readonly INatsSerializer<byte[]> _deserializer = new NatsByteArrayMessageSerializer();
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly string _serviceId = "NatsJetStreamActorConsumer";
    IActorSupervisor _supervisor = default!;
    ActorType _actorType;

    NatsClient? _nc;
    NatsJSConsumeOpts _consumerOpts;
    CancellationTokenSource _cts = new();
    Task? _loopTask;
    bool _isRunning;

    // striped dispatch channels for concurrent mailbox delivery with deferred ACK
    Channel<(NatsMsg<byte[]> Msg, ActorSubject Subject, NatsJSMsg<byte[]> JsMsg, bool IsRoutedMessage)>[]? _stripeChannels;
    Task[]? _dispatcherTasks;

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
    public async ValueTask StartAsync(IActorSupervisor supervisor, ActorType actorType, string consumerName = default!)
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

            _nc = new NatsClient(_options.Url);
            await _nc.ConnectAsync().ConfigureAwait(false);

            var js = _nc.CreateJetStreamContext();
            var streamName = string.IsNullOrWhiteSpace(_options.StreamName)
                ? $"{_actorType}Stream"
                : _options.StreamName;

            var durableName = !string.IsNullOrWhiteSpace(consumerName)
               ? $"{_actorType}Consumer-{consumerName}"
               : $"{_actorType}Consumer";

            var subjectFilter = $"{_actorType}.>";

            // Create or update the JetStream stream for this actor type.
            // If an old stream with a different name owns the same subjects (e.g. from a
            // previous run that used a random suffix), delete it first to avoid overlap.
            try
            {
                await js.CreateOrUpdateStreamAsync(new StreamConfig(streamName, [subjectFilter]));
            }
            catch (NatsJSApiException ex) when (ex.Error.Description is not null
                && ex.Error.Description.Contains("subjects overlap", StringComparison.OrdinalIgnoreCase))
            {
                // Find and delete the conflicting stream, then retry.
                await foreach (var stream in js.ListStreamsAsync())
                {
                    _logger.LogWarning(
                           "Deleting conflicting JetStream stream '{ConflictingStream}' that owns subject '{Subject}'.",
                           stream.Info.Config.Name, subjectFilter);
                    await js.DeleteStreamAsync(stream.Info.Config.Name);
                }
                await js.CreateOrUpdateStreamAsync(new StreamConfig(streamName, [subjectFilter]));
            }

            // Create or update the durable consumer...
            var consumer = await js.CreateOrUpdateConsumerAsync(streamName, new ConsumerConfig(durableName)
            {
                FilterSubject = subjectFilter,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All
            });

            _consumerOpts = new()
            {

            };

            var ctsRequestToken = _cts.Token;

            // create striped dispatch channels and start dispatcher tasks
            var dispatcherCount = Math.Max(1, _options.DispatcherCount);
            _stripeChannels = new Channel<(NatsMsg<byte[]>, ActorSubject, NatsJSMsg<byte[]>, bool)>[dispatcherCount];
            _dispatcherTasks = new Task[dispatcherCount];
            for (var i = 0; i < dispatcherCount; i++)
            {
                _stripeChannels[i] = Channel.CreateBounded<(NatsMsg<byte[]>, ActorSubject, NatsJSMsg<byte[]>, bool)>(
                    new BoundedChannelOptions(DefaultStripeCapacity)
                    {
                        SingleWriter = true,
                        SingleReader = true,
                        FullMode = BoundedChannelFullMode.Wait
                    });
                var reader = _stripeChannels[i].Reader;
                _dispatcherTasks[i] = Task.Run(() => DispatchLoopAsync(reader, ctsRequestToken));
            }

            _loopTask = Task.Run(async () =>
            {
                await JetStreamMessageLoopAsync(consumer, ctsRequestToken);
            });
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
            }
            _loopTask = null;

            // Complete all stripe writers so dispatchers drain remaining items and exit.
            if (_stripeChannels is not null)
            {
                foreach (var ch in _stripeChannels)
                    ch.Writer.TryComplete();
            }

            // Await all dispatcher tasks.
            if (_dispatcherTasks is not null)
            {
                try { await Task.WhenAll(_dispatcherTasks).ConfigureAwait(false); }
                catch (OperationCanceledException) { /* expected */ }
            }
            _stripeChannels = null;
            _dispatcherTasks = null;

            _cts.Dispose();
            await _nc.DisposeAsync().ConfigureAwait(false);
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
    async ValueTask JetStreamMessageLoopAsync(INatsJSConsumer consumer, CancellationToken ctsRequestToken)
    {
        _isRunning = true;
        var stripes = _stripeChannels!;
        var stripeCount = stripes.Length;
        _logger.LogInformationEvent(_serviceId, "JetStream {ActorType} consumer started", _actorType);
        while (!ctsRequestToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS JetStream {ActorType} consumer waiting for messages...", _actorType);
            var messagesRead = 0;
            await foreach (var msg in consumer.ConsumeAsync(serializer: _deserializer, cancellationToken: ctsRequestToken))
            {
                try
                {
                    if (msg.Data is null)
                    {
                        await msg.AckAsync(cancellationToken: ctsRequestToken);
                        continue;
                    }
                    messagesRead++;
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("NATS JetStream {ActorType} consumer received message for subject={Subject}", _actorType, msg.Subject);

                    // parse subject and route to a dispatch stripe by entity hash.
                    // Same entity always maps to the same stripe, preserving per-entity FIFO ordering.
                    var msgSubject = msg.Subject.ToSubject();
                    var natsMsg = new NatsMsg<byte[]>(msg.Subject, msg.ReplyTo, default, default, msg.Data, msg.Connection, default);
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
        _isRunning = false;
    }

    /// <summary>
    /// Dispatches messages from a stripe channel to the corresponding actor mailboxes. Each message is acknowledged after successful processing.
    /// </summary>
    /// <param name="reader">The channel reader for this stripe.</param>
    /// <param name="cancellationToken">The cancellation token used to signal the loop to stop.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    async Task DispatchLoopAsync(ChannelReader<(NatsMsg<byte[]> Msg, ActorSubject Subject, NatsJSMsg<byte[]> JsMsg, bool IsRoutedMessage)> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var (msg, msgSubject, jsMsg, isRoutedMessage) in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var actor = _supervisor.Children.GetValueOrDefault(msgSubject.ActorId)
                        ?? throw new InvalidOperationException($"Actor not found in context children for mailbox {msgSubject.ActorId}");
                    actor.Mailbox.ThreadQueues.Write(msg, msgSubject, cancellationToken);
                    if (!isRoutedMessage)
                    {
                        await jsMsg.AckAsync(cancellationToken: cancellationToken);
                        await _supervisor.RouteEventToAsync(msg);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogErrorEvent(_serviceId, ex, "Dispatch stripe failed to deliver JetStream message for {ActorId}.", msgSubject.ActorId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
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
            var natsMsg = new NatsJSMsg<byte[]>();
            var stripe = (routeToSubject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % _stripeChannels.Length;
            await _stripeChannels[stripe].Writer.WriteAsync((msg, routeToSubject, natsMsg, true), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent(_serviceId, ex, "NATS JetStream {Subject} routed event failed to process. ", routeToSubject);
        }
    }

}
