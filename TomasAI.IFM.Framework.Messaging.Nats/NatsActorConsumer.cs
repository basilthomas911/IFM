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
/// Represents a Core NATS backend actor consumer for exactly one command, query, or realtime actor type.
/// </summary>
/// <remarks>
/// This class permanently binds each instance to its first <see cref="ActorType"/> and subscribes to that
/// type's subject namespace. Incoming messages are deserialized and dispatched to the appropriate backend
/// actor mailbox. <see cref="ActorType.Notify"/> is reserved for application-facing NATS event listeners;
/// durable events use the JetStream actor consumer.
/// </remarks>
/// <param name="options">The NATS consumer options containing connection configuration such as the server URL.</param>
/// <param name="logger">The logger instance used to record diagnostic and lifecycle events.</param>
public class NatsActorConsumer(
    INatsConsumerOptions options,
    ILogger logger,
    NatsConnectionManager? connectionManager = null,
    ActorAdmissionOptions? admissionOptions = null)
    : IActorConsumer
{
    readonly INatsConsumerOptions _options = IsArgumentNull.Set(options);
    readonly INatsSerializer<byte[]> _deserializer = new NatsByteArrayMessageSerializer();
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    readonly string _serviceId = "NatsActorConsumer";
    readonly NatsConnectionManager _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    readonly ActorAdmissionOptions _admissionOptions = admissionOptions ?? new ActorAdmissionOptions();
    readonly SemaphoreSlim _lifecycleGate = new(1, 1);
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
    Channel<(IActorMessage Msg, ActorSubject Subject)>[]? _stripeChannels;
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
            _actorType = BindActorType(_actorType, actorType);
            await StartCoreAsync(supervisor, actorType, consumerName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Starts the NATS consumer for the specified actor type and begins processing all actor messages
    /// routed through the given supervisor.
    /// </summary>
    /// <remarks>
    /// Initializes the NATS client connection and subscribes to all messages matching the subject pattern
    /// <c>{actorType}.&gt;</c>. If the consumer is already started, the call is a no-op.
    /// <para>
    /// The <paramref name="actorType"/> determines the Core NATS messaging pattern used by the background loop:
    /// <list type="bullet">
    ///   <item><description><see cref="ActorType.Realtime"/> uses a publish-subscribe loop for
    ///   non-durable actor delivery.</description></item>
    ///   <item><description><see cref="ActorType.Query"/> and <see cref="ActorType.Command"/> use request-reply
    ///   loops to support response-based interactions.</description></item>
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
            ValidateEnforcedTrafficClass(actorType);
            _subscriptionSubject = string.Concat(_actorType.ToStringFast(), ".>");
            if (_nc is not null)
            {
                _logger.LogDebug("NATS {ActorType} consumer already started.", _actorType);
                return;
            }

            _nc = await _connectionManager.GetClientAsync(_options.Url, cancellationToken).ConfigureAwait(false);
            _cts = new CancellationTokenSource();
            var ctsRequestToken = _cts.Token;
            _requestOptions = new()
            {
                ChannelOpts = new NatsSubChannelOpts
                {
                    Capacity = GetSubscriptionCapacity(),
                    FullMode = BoundedChannelFullMode.Wait
                }
            };

            // create striped dispatch channels and start dispatcher tasks
            var dispatcherCount = Math.Max(1, _options.DispatcherCount);
            _stripeChannels = new Channel<(IActorMessage, ActorSubject)>[dispatcherCount];
            _dispatcherTasks = new Task[dispatcherCount];
            for (var i = 0; i < dispatcherCount; i++)
            {
                _stripeChannels[i] = Channel.CreateBounded<(IActorMessage, ActorSubject)>(
                    new BoundedChannelOptions(GetDispatcherCapacity())
                    {
                        SingleWriter = true,
                        SingleReader = true,
                        FullMode = BoundedChannelFullMode.Wait
                    });
                var reader = _stripeChannels[i].Reader;
                _dispatcherTasks[i] = DispatchLoopAsync(reader);
            }

            _isRunning = true;
            _loopTask = RunMessageLoopAsync(ctsRequestToken);
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
    /// <see langword="false"/>. No further messages for the bound actor type will be consumed after this
    /// method completes. If the consumer has not been started, the call is a no-op and a debug message is logged.
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

    /// <summary>
    /// Permanently binds one backend actor consumer instance to one supported actor type.
    /// Notify subjects are consumed by application-facing NATS event listeners and never by
    /// the backend actor dispatcher.
    /// </summary>
    internal static ActorType BindActorType(ActorType boundActorType, ActorType requestedActorType)
    {
        if (requestedActorType == ActorType.Notify)
        {
            throw new InvalidOperationException(
                "ActorType.Notify is reserved for UI, console, and external NATS event listeners; "
                + "NatsActorConsumer cannot subscribe to Notify.>.");
        }

        requestedActorType.EnsureDeliveryType(
            ActorDeliveryType.NatsCore,
            nameof(NatsActorConsumer));
        if (boundActorType != ActorType.Unknown && boundActorType != requestedActorType)
        {
            throw new InvalidOperationException(
                $"This NatsActorConsumer is already bound to actor type '{boundActorType}' "
                + $"and cannot be reused for '{requestedActorType}'.");
        }

        return requestedActorType;
    }

    int GetDispatcherCapacity()
        => _options is NatsConsumerOptions concrete
            ? concrete.DispatcherCapacity
            : _options.DispatcherCapacity > 0
                ? _options.DispatcherCapacity
                : NatsConsumerOptions.ExistingDispatcherCapacity;

    int GetSubscriptionCapacity()
        => _options is NatsConsumerOptions concrete
            ? concrete.GetSubscriptionCapacity()
            : _options.SubscriptionCapacity > 0
                ? _options.SubscriptionCapacity
                : checked(GetDispatcherCapacity() * Math.Max(1, _options.DispatcherCount));

    CoreNatsTrafficClass GetFireAndForgetTrafficClass(ActorType actorType)
        => _options.FireAndForgetTraffic?.TryGetValue(actorType, out var trafficClass) == true
            ? trafficClass
            : CoreNatsTrafficClass.Unknown;

    void ValidateEnforcedTrafficClass(ActorType actorType)
    {
        if (_admissionOptions.Mode != ActorAdmissionMode.Enforce)
            return;

        var trafficClass = GetFireAndForgetTrafficClass(actorType);
        if (trafficClass is CoreNatsTrafficClass.Unknown or CoreNatsTrafficClass.RequiredNonDurable)
        {
            throw new InvalidOperationException(
                $"Enforced Core NATS consumer {actorType} cannot start with traffic class {trafficClass}.");
        }
        if (actorType is ActorType.Command or ActorType.Query
            && trafficClass != CoreNatsTrafficClass.RequestReplyOnly)
        {
            throw new InvalidOperationException(
                $"Enforced Core NATS {actorType} traffic must be {CoreNatsTrafficClass.RequestReplyOnly}; "
                + $"configured class is {trafficClass}.");
        }
    }

    async ValueTask StopCoreAsync()
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
            if (_ownsConnectionManager)
                await _connectionManager.DisposeAsync().ConfigureAwait(false);
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
    /// Runs the Core NATS message loop for request/reply and non-durable actor message types. Continuously reads messages
    /// from the NATS subscription and dispatches them to the corresponding actor mailboxes.
    /// </summary>
    /// <param name="ctsRequestToken">The cancellation token used to signal the loop to stop.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the loop exits.</returns>
    async Task RunMessageLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            switch (_actorType)
            {
                case ActorType.Realtime:
                    await PubSubMessageLoopAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case ActorType.Command:
                    if (_options.UseOwnedCommandPayloads)
                        await CommandMessageLoopAsync(cancellationToken).ConfigureAwait(false);
                    else
                    {
                        _logger.LogWarning(
                            "NATS command consumer is using the legacy byte[] payload path for diagnostics.");
                        await PubSubMessageLoopAsync(cancellationToken).ConfigureAwait(false);
                    }
                    break;
                case ActorType.Query:
                    if (_options.UseOwnedQueryPayloads)
                        await QueryMessageLoopAsync(cancellationToken).ConfigureAwait(false);
                    else
                    {
                        _logger.LogWarning(
                            "NATS query consumer is using the legacy byte[] payload path for diagnostics.");
                        await ReqReplMessageLoopAsync(cancellationToken).ConfigureAwait(false);
                    }
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        finally
        {
            _isRunning = false;
        }
    }

    async ValueTask PubSubMessageLoopAsync(CancellationToken ctsRequestToken)
    {
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
                    NatsMessagingMetrics.Received.Add(1);
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("NATS {ActorType} consumer received message for subject={Subject}", _actorType, msg.Subject);

                    // parse subject and route to a dispatch stripe by entity hash.
                    // Same entity always maps to the same stripe, preserving per-entity FIFO ordering.
                    var msgSubject = msg.Subject.ToSubject();
                    var destinations = BuildPubSubDestinations(_supervisor, _actorType, msgSubject);
                    if (destinations.Count == 0)
                    {
                        NatsMessagingMetrics.DispatchFailures.Add(1);
                        _logger.LogErrorEvent(
                            _serviceId,
                            "NATS realtime message rejected because its primary actor {ActorId} is not registered.",
                            msgSubject.ActorId);
                        continue;
                    }

                    foreach (var destination in destinations)
                    {
                        var stripe = (destination.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;
                        await stripes[stripe].Writer.WriteAsync(
                            (new NatsActorMessage(msg, destination), destination),
                            ctsRequestToken).ConfigureAwait(false);
                    }
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
    }

    /// <summary>
    /// Resolves Core publish-subscribe destinations. Realtime events include their
    /// required primary actor and all registered realtime routes. Legacy command
    /// diagnostics retain their single explicitly addressed destination.
    /// </summary>
    internal static IReadOnlyList<ActorSubject> BuildPubSubDestinations(
        IActorSupervisor supervisor,
        ActorType actorType,
        ActorSubject source)
    {
        if (actorType != ActorType.Realtime)
            return [source];
        if (!supervisor.ActorExists(source.ActorId))
            return [];

        return EventFanoutRoutes.Build(
            source,
            supervisor.GetRealtimeRoutes(source.ActorTypeId),
            includePrimary: true);
    }

    async ValueTask CommandMessageLoopAsync(CancellationToken cancellationToken)
    {
        var stripes = _stripeChannels!;
        var stripeCount = stripes.Length;
        _logger.LogInformationEvent(_serviceId, "NATS command consumer started with owned pooled payloads");

        await foreach (NatsMsg<NatsMemoryOwner<byte>> msg in _nc!.SubscribeAsync<NatsMemoryOwner<byte>>(
            _subscriptionSubject,
            serializer: NatsDefaultSerializer<NatsMemoryOwner<byte>>.Default,
            opts: _requestOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var owner = msg.Data;

            var transferred = false;
            try
            {
                msg.EnsureSuccess();
                var subject = msg.Subject.ToSubject();
                var actorMessage = new NatsOwnedCommandMessage(msg, subject);
                var stripe = (subject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;

                await stripes[stripe].Writer.WriteAsync(
                    (actorMessage, subject),
                    cancellationToken).ConfigureAwait(false);

                transferred = true;
                NatsMessagingMetrics.Received.Add(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                NatsMessagingMetrics.DispatchFailures.Add(1);
                _logger.LogErrorEvent(_serviceId, ex, "NATS command consumer failed before ownership transfer.");
            }
            finally
            {
                if (!transferred)
                    owner.Dispose();
            }
        }
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
                    NatsMessagingMetrics.Received.Add(1);
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("NATS {ActorType} consumer received message for subject={Subject}", _actorType, msg.Subject);

                    // parse subject and route to a dispatch stripe by entity hash.
                    var msgSubject = msg.Subject.ToSubject();
                    var stripe = (msgSubject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;
                    await stripes[stripe].Writer.WriteAsync((new NatsActorMessage(msg), msgSubject), ctsRequestToken);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorEvent(_serviceId, ex, "NATS {ActorType} consumer failed to process message. ", _actorType);
                }
            }
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("NATS {ActorType} consumer read {MessagesRead} messages.", _actorType, messagesRead);
        }
    }

    /// <summary>
    /// Drains a single dispatch stripe channel and delivers each message to the target actor's mailbox.
    /// One instance runs per stripe, enabling concurrent delivery across entities on different stripes
    /// while preserving per-entity FIFO ordering within each stripe.
    /// </summary>
    /// <param name="reader">The channel reader for this stripe.</param>
    /// <param name="cancellationToken">The cancellation token used to signal the loop to stop.</param>
    async Task DispatchLoopAsync(ChannelReader<(IActorMessage Msg, ActorSubject Subject)> reader)
    {
        try
        {
            await foreach (var (msg, msgSubject) in reader.ReadAllAsync().ConfigureAwait(false))
            {
                var transferred = false;
                var settled = false;
                try
                {
                    var actor = _supervisor.Children.GetValueOrDefault(msgSubject.ActorId)
                        ?? throw new InvalidOperationException(string.Concat("Actor not found in context children for mailbox ", msgSubject.ActorId.ToString()));
                    var admission = await actor.Mailbox.ThreadQueues.TryAdmitAsync(
                        msg,
                        msgSubject,
                        CancellationToken.None).ConfigureAwait(false);
                    if (!admission.Accepted)
                    {
                        settled = true;
                        await NatsTransportOverload.SettleCoreRejectionAsync(
                            msg,
                            _actorType,
                            admission.Reason,
                            _admissionOptions.OverloadErrorCode,
                            GetFireAndForgetTrafficClass(_actorType),
                            _logger).ConfigureAwait(false);
                        continue;
                    }
                    transferred = true;
                }
                catch (Exception ex)
                {
                    NatsMessagingMetrics.DispatchFailures.Add(1);
                    _logger.LogErrorEvent(_serviceId, ex, "Dispatch stripe failed to deliver message for {ActorId}.", msgSubject.ActorId);
                }
                finally
                {
                    if (!transferred && !settled)
                        msg.Dispose();
                }
            }
        }
        finally
        {
            while (reader.TryRead(out var pending))
                pending.Msg.Dispose();
        }
    }

    async ValueTask QueryMessageLoopAsync(CancellationToken cancellationToken)
    {
        var stripes = _stripeChannels!;
        var stripeCount = stripes.Length;
        _logger.LogInformationEvent(
            _serviceId,
            "NATS query consumer started with owned pooled payloads");

        await foreach (NatsMsg<NatsMemoryOwner<byte>> msg in _nc!.SubscribeAsync<NatsMemoryOwner<byte>>(
            _subscriptionSubject,
            serializer: NatsDefaultSerializer<NatsMemoryOwner<byte>>.Default,
            opts: _requestOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var owner = msg.Data;
            var transferred = false;
            try
            {
                msg.EnsureSuccess();
                var subject = msg.Subject.ToSubject();
                var actorMessage = new NatsOwnedQueryMessage(msg, subject);
                var stripe = (subject.ThreadId.GetHashCode() & 0x7FFF_FFFF) % stripeCount;

                await stripes[stripe].Writer.WriteAsync(
                    (actorMessage, subject),
                    cancellationToken).ConfigureAwait(false);

                transferred = true;
                NatsMessagingMetrics.Received.Add(1);
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
                    "NATS query consumer failed before ownership transfer.");
            }
            finally
            {
                if (!transferred)
                    owner.Dispose();
            }
        }
    }
}
