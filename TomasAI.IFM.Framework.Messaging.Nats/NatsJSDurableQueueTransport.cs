using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using System.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Implements durable process and replay queue transport operations with NATS JetStream.
/// </summary>
/// <param name="options">The consumer options that provide the NATS server URL.</param>
/// <remarks>
/// The connection is established lazily on the first call to <see cref="EnsureQueueAsync"/> and is shared
/// by every projector managed by this instance. Queue initialization is serialized, while lookups and message
/// operations are safe for concurrent use across projector names. Process consumers use explicit acknowledgements
/// and unlimited redelivery so a process-to-replay handoff failure cannot strand an event. Replay consumers also use
/// unlimited server delivery; the queue applies the configured application failure limit so same-stream ordering
/// deferrals do not exhaust the failure budget. Disposing this type closes the client but leaves server-side resources intact.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
internal sealed class NatsJSDurableQueueTransport(
    INatsJetStreamConsumerOptions options,
    NatsConnectionManager? connectionManager = null)
    : INatsJSDurableQueueTransport
{
    readonly INatsJetStreamConsumerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    readonly NatsByteArrayMessageSerializer _serializer = new();
    readonly NatsJSConsumeOpts _consumeOptions = new()
    {
        MaxMsgs = 4096,
        ThresholdMsgs = 1024,
        DrainOnCancel = true
    };
    readonly SemaphoreSlim _connectionGate = new(1, 1);
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, QueueConsumers> _consumers = new(StringComparer.Ordinal);
    readonly NatsConnectionManager _connectionManager = connectionManager ?? new NatsConnectionManager();
    readonly bool _ownsConnectionManager = connectionManager is null;
    NatsClient? _client;
    INatsJSContext? _jetStream;

    /// <inheritdoc />
    /// <remarks>
    /// Repeated calls with matching resource names, replay interval, and delivery limit return without making
    /// server requests. Otherwise, both streams and both durable consumers are created or updated and their
    /// local handles replace the previous handles for the projector key.
    /// </remarks>
    public async ValueTask EnsureQueueAsync(
        string eventProjectorName,
        NatsJSDurableQueueSettings settings,
        CancellationToken cancellationToken)
    {
        if (_consumers.TryGetValue(eventProjectorName, out var existing)
            && SettingsMatch(existing.Settings, settings))
            return;

        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_consumers.TryGetValue(eventProjectorName, out existing)
                && SettingsMatch(existing.Settings, settings))
                return;

            if (_client is null)
            {
                _client = await _connectionManager.GetClientAsync(_options.Url, cancellationToken).ConfigureAwait(false);
                _jetStream = await _connectionManager.GetJetStreamContextAsync(_options.Url, cancellationToken).ConfigureAwait(false);
            }

            var js = _jetStream!;
            await js.CreateOrUpdateStreamAsync(
                JetStreamTransportRetention.CreateQueue(settings.Names.ProcessStream, settings.Names.ProcessSubject),
                cancellationToken).ConfigureAwait(false);
            await js.CreateOrUpdateStreamAsync(
                JetStreamTransportRetention.CreateQueue(settings.Names.ReplayStream, settings.Names.ReplaySubject),
                cancellationToken).ConfigureAwait(false);

            var processConsumer = await js.CreateOrUpdateConsumerAsync(
                settings.Names.ProcessStream,
                new ConsumerConfig(settings.Names.ProcessConsumer)
                {
                    FilterSubject = settings.Names.ProcessSubject,
                    AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                    MaxDeliver = -1,
                    MaxAckPending = 4096
                },
                cancellationToken).ConfigureAwait(false);

            var replayConsumer = await js.CreateOrUpdateConsumerAsync(
                settings.Names.ReplayStream,
                new ConsumerConfig(settings.Names.ReplayConsumer)
                {
                    FilterSubject = settings.Names.ReplaySubject,
                    AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                    AckWait = settings.ReplayInterval,
                    MaxDeliver = -1,
                    MaxAckPending = 4096
                },
                cancellationToken).ConfigureAwait(false);

            _consumers[eventProjectorName] = new QueueConsumers(settings, processConsumer, replayConsumer);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// No queue has been initialized for <paramref name="eventProjectorName"/>.
    /// </exception>
    public async ValueTask PublishProcessAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        var ack = await _jetStream!.PublishAsync(
            queue.Settings.Names.ProcessSubject,
            payload,
            serializer: _serializer,
            headers: CreateMessageHeaders(messageId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsurePublishAccepted(ack);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// No queue has been initialized for <paramref name="eventProjectorName"/>.
    /// </exception>
    public async ValueTask PublishReplayAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        var ack = await _jetStream!.PublishAsync(
            queue.Settings.Names.ReplaySubject,
            payload,
            serializer: _serializer,
            headers: CreateMessageHeaders(messageId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsurePublishAccepted(ack);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// No queue has been initialized for <paramref name="eventProjectorName"/>.
    /// </exception>
    public async IAsyncEnumerable<INatsJSDurableMessage> ConsumeProcessAsync(
        string eventProjectorName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        await foreach (var message in queue.ProcessConsumer.ConsumeAsync(
            opts: _consumeOptions,
            serializer: _serializer,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return new NatsJSDurableMessage(message);
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// No queue has been initialized for <paramref name="eventProjectorName"/>.
    /// </exception>
    public async IAsyncEnumerable<INatsJSDurableMessage> ConsumeReplayAsync(
        string eventProjectorName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        await foreach (var message in queue.ReplayConsumer.ConsumeAsync(
            opts: _consumeOptions,
            serializer: _serializer,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return new NatsJSDurableMessage(message);
        }
    }

    QueueConsumers GetQueue(string eventProjectorName) =>
        _consumers.TryGetValue(eventProjectorName, out var queue)
            ? queue
            : throw new InvalidOperationException($"The durable queue for projector '{eventProjectorName}' has not been initialized.");

    static NatsHeaders CreateMessageHeaders(string messageId)
    {
        var headers = ActorTrace.Headers() ?? new NatsHeaders();
        headers["Nats-Msg-Id"] = messageId;
        return headers;
    }

    static void EnsurePublishAccepted(PubAckResponse acknowledgement)
    {
        if (!acknowledgement.Duplicate)
            acknowledgement.EnsureSuccess();
    }

    static bool SettingsMatch(NatsJSDurableQueueSettings first, NatsJSDurableQueueSettings second) =>
        first.Names == second.Names
        && first.ReplayInterval == second.ReplayInterval
        && first.MaxReplayAttempts == second.MaxReplayAttempts;

    /// <summary>
    /// Disposes the shared NATS client and the synchronization resources owned by the transport.
    /// </summary>
    /// <returns>A value task that completes after the NATS client has closed.</returns>
    /// <remarks>Server-side streams and consumers are not deleted.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (_ownsConnectionManager)
            await _connectionManager.DisposeAsync().ConfigureAwait(false);
        _connectionGate.Dispose();
    }

    sealed record QueueConsumers(
        NatsJSDurableQueueSettings Settings,
        INatsJSConsumer ProcessConsumer,
        INatsJSConsumer ReplayConsumer);

    sealed class NatsJSDurableMessage(INatsJSMsg<byte[]> message) : INatsJSDurableMessage
    {
        public byte[] Data => message.Data ?? [];
        public ActivityContext TraceContext => ActorTrace.Extract(message.Headers);
        public ulong DeliveryCount => message.Metadata?.NumDelivered ?? 1;
        public ValueTask AckAsync(CancellationToken cancellationToken) =>
            message.AckAsync(cancellationToken: cancellationToken);
        public ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            message.NakAsync(delay: delay, cancellationToken: cancellationToken);
    }
}
