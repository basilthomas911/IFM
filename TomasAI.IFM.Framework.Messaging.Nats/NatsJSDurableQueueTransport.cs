using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Identifies the JetStream resources that make up a projector's process and replay queues.
/// </summary>
/// <param name="ProcessStream">The stream that stores newly enqueued process messages.</param>
/// <param name="ProcessSubject">The subject to which process messages are published.</param>
/// <param name="ProcessConsumer">The durable consumer that reads process messages.</param>
/// <param name="ReplayStream">The stream that stores messages awaiting replay.</param>
/// <param name="ReplaySubject">The subject to which failed process messages are published.</param>
/// <param name="ReplayConsumer">The durable consumer that reads replay messages.</param>
internal sealed record NatsJSDurableQueueNames(
    string ProcessStream,
    string ProcessSubject,
    string ProcessConsumer,
    string ReplayStream,
    string ReplaySubject,
    string ReplayConsumer);

/// <summary>
/// Describes the JetStream resources and replay policy for one projector queue.
/// </summary>
/// <param name="Names">The names of the streams, subjects, and durable consumers.</param>
/// <param name="ReplayInterval">The acknowledgement wait and initial replay delay.</param>
/// <param name="MaxReplayAttempts">The maximum number of replay message deliveries.</param>
/// <param name="Backoff">The server-side redelivery delays, ordered by delivery attempt.</param>
internal sealed record NatsJSDurableQueueSettings(
    NatsJSDurableQueueNames Names,
    TimeSpan ReplayInterval,
    int MaxReplayAttempts,
    IReadOnlyList<TimeSpan> Backoff);

/// <summary>
/// Represents a process or replay message received from a durable queue transport.
/// </summary>
internal interface INatsJSDurableMessage
{
    /// <summary>Gets the serialized durable event envelope.</summary>
    byte[] Data { get; }

    /// <summary>Gets the number of times the message has been delivered, including the current delivery.</summary>
    ulong DeliveryCount { get; }

    /// <summary>Acknowledges successful or terminal processing of the message.</summary>
    /// <param name="cancellationToken">A token that cancels the acknowledgement.</param>
    /// <returns>A value task that completes when the acknowledgement has been sent.</returns>
    ValueTask AckAsync(CancellationToken cancellationToken);

    /// <summary>Negatively acknowledges the message and requests redelivery after a delay.</summary>
    /// <param name="delay">The minimum delay before the message is eligible for redelivery.</param>
    /// <param name="cancellationToken">A token that cancels the negative acknowledgement.</param>
    /// <returns>A value task that completes when the negative acknowledgement has been sent.</returns>
    ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>
/// Defines the JetStream operations required by <see cref="NatsJSDurableReplayQueue"/>.
/// </summary>
internal interface INatsJSDurableQueueTransport : IAsyncDisposable
{
    /// <summary>Creates or updates the streams and durable consumers for a projector queue.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="settings">The resource names and replay policy to apply.</param>
    /// <param name="cancellationToken">A token that cancels initialization.</param>
    /// <returns>A value task that completes when the resources and local consumer handles are ready.</returns>
    ValueTask EnsureQueueAsync(string eventProjectorName, NatsJSDurableQueueSettings settings, CancellationToken cancellationToken);

    /// <summary>Publishes a serialized event to an initialized projector's process subject.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="payload">The serialized durable event envelope.</param>
    /// <param name="messageId">The stable JetStream message identifier used for duplicate suppression.</param>
    /// <param name="cancellationToken">A token that cancels publication.</param>
    /// <returns>A value task that completes after the server acknowledges the publication.</returns>
    ValueTask PublishProcessAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken);

    /// <summary>Publishes a serialized failed event to an initialized projector's replay subject.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="payload">The serialized durable event envelope, including failure details.</param>
    /// <param name="messageId">The stable JetStream message identifier used for duplicate suppression.</param>
    /// <param name="cancellationToken">A token that cancels publication.</param>
    /// <returns>A value task that completes after the server acknowledges the publication.</returns>
    ValueTask PublishReplayAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken);

    /// <summary>Consumes messages from an initialized projector's process consumer.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="cancellationToken">A token that stops the asynchronous enumeration.</param>
    /// <returns>An asynchronous sequence of process messages.</returns>
    IAsyncEnumerable<INatsJSDurableMessage> ConsumeProcessAsync(string eventProjectorName, CancellationToken cancellationToken);

    /// <summary>Consumes messages from an initialized projector's replay consumer.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="cancellationToken">A token that stops the asynchronous enumeration.</param>
    /// <returns>An asynchronous sequence of replay messages.</returns>
    IAsyncEnumerable<INatsJSDurableMessage> ConsumeReplayAsync(string eventProjectorName, CancellationToken cancellationToken);
}

/// <summary>
/// Implements durable process and replay queue transport operations with NATS JetStream.
/// </summary>
/// <param name="options">The consumer options that provide the NATS server URL.</param>
/// <remarks>
/// The connection is established lazily on the first call to <see cref="EnsureQueueAsync"/> and is shared
/// by every projector managed by this instance. Queue initialization is serialized, while lookups and message
/// operations are safe for concurrent use across projector names. Process consumers use explicit acknowledgements
/// and unlimited redelivery so a process-to-replay handoff failure cannot strand an event. Replay consumers use
/// explicit acknowledgements, the configured delivery limit, and
/// server-side backoff. Disposing this type closes the client but leaves server-side resources intact.
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
                new StreamConfig(settings.Names.ProcessStream, [settings.Names.ProcessSubject]),
                cancellationToken).ConfigureAwait(false);
            await js.CreateOrUpdateStreamAsync(
                new StreamConfig(settings.Names.ReplayStream, [settings.Names.ReplaySubject]),
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
                    MaxDeliver = settings.MaxReplayAttempts,
                    MaxAckPending = 4096,
                    Backoff = settings.Backoff.ToList()
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

    static NatsHeaders CreateMessageHeaders(string messageId) => new()
    {
        ["Nats-Msg-Id"] = messageId
    };

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
        public ulong DeliveryCount => message.Metadata?.NumDelivered ?? 1;
        public ValueTask AckAsync(CancellationToken cancellationToken) =>
            message.AckAsync(cancellationToken: cancellationToken);
        public ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            message.NakAsync(delay: delay, cancellationToken: cancellationToken);
    }
}
