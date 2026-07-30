using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats;

internal sealed record NatsJSDurableQueueNames(
    string ProcessStream,
    string ProcessSubject,
    string ProcessConsumer,
    string ReplayStream,
    string ReplaySubject,
    string ReplayConsumer);

internal sealed record NatsJSDurableQueueSettings(
    NatsJSDurableQueueNames Names,
    TimeSpan ReplayInterval,
    int MaxReplayAttempts,
    IReadOnlyList<TimeSpan> Backoff);

internal interface INatsJSDurableMessage
{
    byte[] Data { get; }
    ulong DeliveryCount { get; }
    ValueTask AckAsync(CancellationToken cancellationToken);
    ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken);
}

internal interface INatsJSDurableQueueTransport : IAsyncDisposable
{
    ValueTask EnsureQueueAsync(string eventProjectorName, NatsJSDurableQueueSettings settings, CancellationToken cancellationToken);
    ValueTask PublishProcessAsync(string eventProjectorName, byte[] payload, CancellationToken cancellationToken);
    ValueTask PublishReplayAsync(string eventProjectorName, byte[] payload, CancellationToken cancellationToken);
    IAsyncEnumerable<INatsJSDurableMessage> ConsumeProcessAsync(string eventProjectorName, CancellationToken cancellationToken);
    IAsyncEnumerable<INatsJSDurableMessage> ConsumeReplayAsync(string eventProjectorName, CancellationToken cancellationToken);
}

internal sealed class NatsJSDurableQueueTransport(INatsJetStreamConsumerOptions options)
    : INatsJSDurableQueueTransport
{
    readonly INatsJetStreamConsumerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    readonly NatsByteArrayMessageSerializer _serializer = new();
    readonly SemaphoreSlim _connectionGate = new(1, 1);
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, QueueConsumers> _consumers = new(StringComparer.Ordinal);
    NatsClient? _client;
    INatsJSContext? _jetStream;

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
                _client = new NatsClient(_options.Url);
                await _client.ConnectAsync().ConfigureAwait(false);
                _jetStream = _client.CreateJetStreamContext();
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
                    MaxDeliver = 1
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

    public async ValueTask PublishProcessAsync(string eventProjectorName, byte[] payload, CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        var ack = await _jetStream!.PublishAsync(
            queue.Settings.Names.ProcessSubject,
            payload,
            serializer: _serializer,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        ack.EnsureSuccess();
    }

    public async ValueTask PublishReplayAsync(string eventProjectorName, byte[] payload, CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        var ack = await _jetStream!.PublishAsync(
            queue.Settings.Names.ReplaySubject,
            payload,
            serializer: _serializer,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        ack.EnsureSuccess();
    }

    public async IAsyncEnumerable<INatsJSDurableMessage> ConsumeProcessAsync(
        string eventProjectorName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        await foreach (var message in queue.ProcessConsumer.ConsumeAsync(
            serializer: _serializer,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return new NatsJSDurableMessage(message);
        }
    }

    public async IAsyncEnumerable<INatsJSDurableMessage> ConsumeReplayAsync(
        string eventProjectorName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        await foreach (var message in queue.ReplayConsumer.ConsumeAsync(
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

    static bool SettingsMatch(NatsJSDurableQueueSettings first, NatsJSDurableQueueSettings second) =>
        first.Names == second.Names
        && first.ReplayInterval == second.ReplayInterval
        && first.MaxReplayAttempts == second.MaxReplayAttempts;

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync().ConfigureAwait(false);
        _connectionGate.Dispose();
    }

    sealed record QueueConsumers(
        NatsJSDurableQueueSettings Settings,
        INatsJSConsumer ProcessConsumer,
        INatsJSConsumer ReplayConsumer);

    sealed class NatsJSDurableMessage(NatsJSMsg<byte[]> message) : INatsJSDurableMessage
    {
        public byte[] Data => message.Data ?? [];
        public ulong DeliveryCount => message.Metadata?.NumDelivered ?? 1;
        public ValueTask AckAsync(CancellationToken cancellationToken) =>
            message.AckAsync(cancellationToken: cancellationToken);
        public ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            message.NakAsync(delay: delay, cancellationToken: cancellationToken);
    }
}
