using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Represents a general-purpose NATS consumer that subscribes to and processes all types of actor messages,
/// including commands, events, queries, notifications, and denormalizer messages.
/// </summary>
/// <remarks>
/// This class manages the lifecycle of a NATS client that subscribes to messages for a given
/// <see cref="ActorType"/>. Incoming messages are deserialized and dispatched to the appropriate actor's
/// mailbox for processing. The consumer supports both publish-subscribe and request-reply messaging
/// patterns, selected automatically based on the actor type. It can be started and stopped asynchronously,
/// and its running state can be queried via the <see cref="IsRunning"/> property.
/// </remarks>
/// <param name="options">The NATS consumer options containing connection configuration such as the server URL.</param>
/// <param name="logger">The logger instance used to record diagnostic and lifecycle events.</param>
public class NatsActorConsumer(INatsConsumerOptions options, ILogger logger) 
    : IActorConsumer
{
    const int DefaultStripeCapacity = 4096;

    readonly INatsConsumerOptions _options = IsArgumentNull.Set(options);
    readonly INatsSerializer<byte[]> _deserializer = new NatsByteArrayMessageSerializer();
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly string _serviceId = "NatsActorConsumer";
    IActorSupervisor _supervisor = default!;
    ActorType _actorType;
    string _subscriptionSubject = default!;
    NatsSubOpts _requestOptions = default!;

    // command consumer fields...
    NatsClient? _nc;
    CancellationTokenSource _cts = new();
    Task? _loopTask;
    bool _isRunning;

    // striped dispatch channels for concurrent mailbox delivery
    Channel<(NatsMsg<byte[]> Msg, ActorSubject Subject)>[]? _stripeChannels;
    Task[]? _dispatcherTasks;

    /// <summary>
    /// Starts the NATS consumer for the specified actor type and begins processing all actor messages
    /// routed through the given supervisor.
    /// </summary>
    /// <remarks>
    /// Initializes the NATS client connection and subscribes to all messages matching the subject pattern
    /// <c>{actorType}.&gt;</c>. If the consumer is already started, the call is a no-op.
    /// <para>
    /// The <paramref name="actorType"/> determines the messaging pattern used by the background loop:
    /// <list type="bullet">
    ///   <item><description><see cref="ActorType.Supervisor"/>, <see cref="ActorType.Command"/>, <see cref="ActorType.Event"/>,
    ///   <see cref="ActorType.Notify"/>, and <see cref="ActorType.Denormalizer"/> use a publish-subscribe loop
    ///   for fire-and-forget message delivery.</description></item>
    ///   <item><description><see cref="ActorType.Query"/> and <see cref="ActorType.CommandRequest"/> use a request-reply loop
    ///   to support response-based interactions.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Regardless of actor type, all incoming messages are deserialized and posted to the matching actor's
    /// mailbox located via <see cref="IActorSupervisor.Children"/>. The background loop runs until
    /// <see cref="StopAsync"/> is called.
    /// </para>
    /// </remarks>
    /// <param name="supervisor">The actor supervisor whose children contain the target actor mailboxes.</param>
    /// <param name="actorType">The actor type that determines the NATS subject pattern and messaging pattern.</param>
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
            _subscriptionSubject = string.Concat(_actorType.ToStringFast(), ".>");
            if (_nc is not null)
            {
                _logger.LogDebug("NATS {ActorType} consumer already started.", _actorType);
                return;
            }

            _nc = new NatsClient(_options.Url);
            await _nc.ConnectAsync().ConfigureAwait(false);
            _cts = new CancellationTokenSource();
            var ctsRequestToken = _cts.Token;
            _requestOptions = new()
            {
               IdleTimeout = TimeSpan.FromSeconds(30)
            };

            // create striped dispatch channels and start dispatcher tasks
            var dispatcherCount = Math.Max(1, _options.DispatcherCount);
            _stripeChannels = new Channel<(NatsMsg<byte[]>, ActorSubject)>[dispatcherCount];
            _dispatcherTasks = new Task[dispatcherCount];
            for (var i = 0; i < dispatcherCount; i++)
            {
                _stripeChannels[i] = Channel.CreateBounded<(NatsMsg<byte[]>, ActorSubject)>(
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
                switch(_actorType)
                {
                    case ActorType.Supervisor:
                    case ActorType.Command:
                    case ActorType.Event:
                    case ActorType.Notify:
                        await PubSubMessageLoopAsync(ctsRequestToken);
                        break;
                    case ActorType.Query:
                        await ReqReplMessageLoopAsync(ctsRequestToken);
                        break;
                }
            });
            _logger.LogInformationEvent(_serviceId, "NATS {ActorType} consumer started with {DispatcherCount} dispatch stripes.", _actorType, dispatcherCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NATS {ActorType} failed during consumer startup.", _actorType);
            throw;
        }
    }

    /// <summary>
    /// Stops the NATS actor message consumer and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// Cancels the active message loop, disposes the NATS client, and sets <see cref="IsRunning"/> to
    /// <see langword="false"/>. No further actor messages of any type will be consumed after this method
    /// completes. If the consumer has not been started, the call is a no-op and a debug message is logged.
    /// </remarks>
    /// <returns>A <see cref="ValueTask"/> that completes once the consumer has been stopped and disposed.</returns>
    public async ValueTask StopAsync()
    {
        try
        {
            if (_nc is null)
            {
                _logger.LogDebug("NATS {ActorType} consumer has not started.", _actorType);
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
            _logger.LogInformation("NATS {ActorType} consumer has stopped.", _actorType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop NATS {ActorType} consumer.", _actorType);
            throw;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the NATS actor message consumer loop is currently running.
    /// </summary>
    /// <value><see langword="true"/> if the consumer is actively processing actor messages; otherwise, <see langword="false"/>.</value>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Runs the publish-subscribe message loop for fire-and-forget actor message types, including
    /// supervisor, command, event, notification, and denormalizer messages. Continuously reads messages
    /// from the NATS subscription and dispatches them to the corresponding actor mailboxes.
    /// </summary>
    /// <param name="ctsRequestToken">The cancellation token used to signal the loop to stop.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the loop exits.</returns>
    async ValueTask PubSubMessageLoopAsync(CancellationToken ctsRequestToken)
    {
        _isRunning = true;
        var stripes = _stripeChannels!;
        var stripeCount = stripes.Length;
        _logger.LogInformationEvent(_serviceId, "{ActorType} consumer started", _actorType);
        while (!ctsRequestToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS {ActorType} consumer waiting for messages...", _actorType);
            var messagesRead = 0;
            await foreach (var msg in _nc!.SubscribeAsync(_subscriptionSubject, serializer: _deserializer, opts: _requestOptions, cancellationToken: ctsRequestToken))
            {
                try
                {
                    if (msg.Data is null)
                        continue;
                    msg.EnsureSuccess();
                    messagesRead++;
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("NATS {ActorType} consumer received message for subject={Subject}", _actorType, msg.Subject);

                    // parse subject and route to a dispatch stripe by entity hash.
                    // Same entity always maps to the same stripe, preserving per-entity FIFO ordering.
                    var msgSubject = msg.Subject.ToSubject();
                    var stripe = (msgSubject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;
                    await stripes[stripe].Writer.WriteAsync((msg, msgSubject), ctsRequestToken);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogErrorEvent(_serviceId, ex, "NATS {ActorType} consumer cancellation requested, stopping message loop.", _actorType);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorEvent(_serviceId, ex, "NATS {ActorType} consumer failed to process message. ", _actorType);
                }
            }
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS {ActorType} consumer read {MessagesRead} messages.", _actorType, messagesRead);
        }
        _isRunning = false;
    }

    /// <summary>
    /// Runs the request-reply message loop for response-based actor message types, including query and
    /// command-request messages. Continuously reads messages from the NATS subscription and dispatches
    /// them to the corresponding actor mailboxes for request-reply processing.
    /// </summary>
    /// <param name="ctsRequestToken">The cancellation token used to signal the loop to stop.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the loop exits.</returns>
    async ValueTask ReqReplMessageLoopAsync(CancellationToken ctsRequestToken)
    {
        _isRunning = true;
        var stripes = _stripeChannels!;
        var stripeCount = stripes.Length;
        _logger.LogInformationEvent(_serviceId, "{ActorType} consumer started", _actorType);
        while (!ctsRequestToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS {ActorType} consumer waiting for messages...", _actorType);
            var messagesRead = 0;
            await foreach (var msg in _nc!.SubscribeAsync(_subscriptionSubject, serializer: _deserializer, opts: _requestOptions, cancellationToken: ctsRequestToken))
            {
                try
                {
                    if (msg.Data is null)
                        continue;
                    msg.EnsureSuccess();
                    messagesRead++;
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("NATS {ActorType} consumer received message for subject={Subject}", _actorType, msg.Subject);

                    // parse subject and route to a dispatch stripe by entity hash.
                    var msgSubject = msg.Subject.ToSubject();
                    var stripe = (msgSubject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;
                    await stripes[stripe].Writer.WriteAsync((msg, msgSubject), ctsRequestToken);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorEvent(_serviceId, ex, "NATS {ActorType} consumer failed to process message. ", _actorType);
                }
            }
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS {ActorType} consumer read {MessagesRead} messages.", _actorType, messagesRead);
        }
        _isRunning = false;
    }

    /// <summary>
    /// Drains a single dispatch stripe channel and delivers each message to the target actor's mailbox.
    /// One instance runs per stripe, enabling concurrent delivery across entities on different stripes
    /// while preserving per-entity FIFO ordering within each stripe.
    /// </summary>
    /// <param name="reader">The channel reader for this stripe.</param>
    /// <param name="cancellationToken">The cancellation token used to signal the loop to stop.</param>
    async Task DispatchLoopAsync(ChannelReader<(NatsMsg<byte[]> Msg, ActorSubject Subject)> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var (msg, msgSubject) in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var actor = _supervisor.Children.GetValueOrDefault(msgSubject.ActorId)
                        ?? throw new InvalidOperationException(string.Concat("Actor not found in context children for mailbox ", msgSubject.ActorId.ToString()));
                    //await actor.Mailbox.ThreadQueues.WriteAsync(msg, msgSubject, cancellationToken);
                    actor.Mailbox.ThreadQueues.Write(msg, msgSubject, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogErrorEvent(_serviceId, ex, "Dispatch stripe failed to deliver message for {ActorId}.", msgSubject.ActorId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
    }
}
