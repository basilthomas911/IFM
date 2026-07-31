using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Framework.Messaging.Nats;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests.NatsJSDurableQueue;

internal sealed class FakeNatsJSDurableQueueTransport : INatsJSDurableQueueTransport
{
    readonly ConcurrentDictionary<string, QueueState> _queues = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, QueueState> Queues => _queues;
    public int DisposeCount { get; private set; }

    public ValueTask EnsureQueueAsync(
        string eventProjectorName,
        NatsJSDurableQueueSettings settings,
        CancellationToken cancellationToken)
    {
        var queue = _queues.GetOrAdd(eventProjectorName, static _ => new QueueState());
        queue.Settings = settings;
        Interlocked.Increment(ref queue.EnsureCount);
        return ValueTask.CompletedTask;
    }

    public ValueTask PublishProcessAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        Interlocked.Increment(ref queue.ProcessPublishAttempts);
        queue.LastProcessMessageId = messageId;
        if (!queue.ProcessMessageIds.TryAdd(messageId, 0))
            return ValueTask.CompletedTask;

        Interlocked.Increment(ref queue.ProcessPublishCount);
        var message = new FakeMessage(
            payload,
            queue.Process.Writer,
            () => TryConsumeFailure(ref queue.ProcessAckFailuresRemaining));
        queue.LastProcessMessage = message;
        return queue.Process.Writer.WriteAsync(message, cancellationToken);
    }

    public ValueTask PublishReplayAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        Interlocked.Increment(ref queue.ReplayPublishAttempts);
        queue.LastReplayMessageId = messageId;
        if (TryConsumeFailure(ref queue.ReplayPublishFailuresRemaining))
            throw new InvalidOperationException("The replay publication failed.");
        if (!queue.ReplayMessageIds.TryAdd(messageId, 0))
            return ValueTask.CompletedTask;

        Interlocked.Increment(ref queue.ReplayPublishCount);
        var message = new FakeMessage(payload, queue.Replay.Writer);
        queue.LastReplayMessage = message;
        return queue.Replay.Writer.WriteAsync(message, cancellationToken);
    }

    public IAsyncEnumerable<INatsJSDurableMessage> ConsumeProcessAsync(string eventProjectorName, CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        Interlocked.Increment(ref queue.ProcessConsumerStarts);
        return ReadAllAsync(queue.Process.Reader, cancellationToken);
    }

    public IAsyncEnumerable<INatsJSDurableMessage> ConsumeReplayAsync(string eventProjectorName, CancellationToken cancellationToken)
    {
        var queue = GetQueue(eventProjectorName);
        Interlocked.Increment(ref queue.ReplayConsumerStarts);
        return ReadAllAsync(queue.Replay.Reader, cancellationToken);
    }

    static async IAsyncEnumerable<INatsJSDurableMessage> ReadAllAsync(
        ChannelReader<INatsJSDurableMessage> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    QueueState GetQueue(string eventProjectorName) =>
        _queues.TryGetValue(eventProjectorName, out var queue)
            ? queue
            : throw new InvalidOperationException($"Queue '{eventProjectorName}' was not initialized.");

    static bool TryConsumeFailure(ref int failuresRemaining)
    {
        while (true)
        {
            var current = Volatile.Read(ref failuresRemaining);
            if (current <= 0)
                return false;
            if (Interlocked.CompareExchange(ref failuresRemaining, current - 1, current) == current)
                return true;
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    internal sealed class QueueState
    {
        public readonly Channel<INatsJSDurableMessage> Process = Channel.CreateUnbounded<INatsJSDurableMessage>();
        public readonly Channel<INatsJSDurableMessage> Replay = Channel.CreateUnbounded<INatsJSDurableMessage>();
        public readonly ConcurrentDictionary<string, byte> ProcessMessageIds = new(StringComparer.Ordinal);
        public readonly ConcurrentDictionary<string, byte> ReplayMessageIds = new(StringComparer.Ordinal);
        public NatsJSDurableQueueSettings Settings = default!;
        public int EnsureCount;
        public int ProcessPublishAttempts;
        public int ProcessPublishCount;
        public int ReplayPublishAttempts;
        public int ReplayPublishCount;
        public int ReplayPublishFailuresRemaining;
        public int ProcessAckFailuresRemaining;
        public int ProcessConsumerStarts;
        public int ReplayConsumerStarts;
        public string? LastProcessMessageId;
        public string? LastReplayMessageId;
        public FakeMessage? LastProcessMessage;
        public FakeMessage? LastReplayMessage;
    }

    internal sealed class FakeMessage(
        byte[] data,
        ChannelWriter<INatsJSDurableMessage> redeliveryWriter,
        Func<bool>? ackShouldFail = null)
        : INatsJSDurableMessage
    {
        public byte[] Data { get; } = data;
        public ulong DeliveryCount { get; private set; } = 1;
        public int AckAttempts { get; private set; }
        public int AckCount { get; private set; }
        public int NakCount { get; private set; }

        public ValueTask AckAsync(CancellationToken cancellationToken)
        {
            AckAttempts++;
            if (ackShouldFail?.Invoke() == true)
                throw new InvalidOperationException("The acknowledgement failed.");
            AckCount++;
            return ValueTask.CompletedTask;
        }

        public async ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            NakCount++;
            DeliveryCount++;
            await redeliveryWriter.WriteAsync(this, cancellationToken);
        }
    }
}
