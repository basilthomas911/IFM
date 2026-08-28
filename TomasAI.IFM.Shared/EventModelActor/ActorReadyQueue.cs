using System.Threading.Channels;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Shared ready-mailbox queue consumed by the fixed actor worker set.</summary>
sealed class ActorReadyQueue
{
    readonly Channel<ActorThreadId> _channel = Channel.CreateUnbounded<ActorThreadId>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    int _completed;
    int _scheduledCount;

    public bool IsCompleted => Volatile.Read(ref _completed) != 0;
    public int ScheduledCount => Math.Max(0, Volatile.Read(ref _scheduledCount));

    public bool Schedule(ActorThreadId threadId)
    {
        if (IsCompleted)
            return false;

        Interlocked.Increment(ref _scheduledCount);
        if (!_channel.Writer.TryWrite(threadId))
        {
            Interlocked.Decrement(ref _scheduledCount);
            return false;
        }

        ActorRuntimeMetrics.RecordReadyScheduled(threadId.ActorType);
        return true;
    }

    public async IAsyncEnumerable<ActorThreadId> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var threadId in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            Interlocked.Decrement(ref _scheduledCount);
            ActorRuntimeMetrics.RecordReadyDequeued(threadId.ActorType);
            yield return threadId;
        }
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
            _channel.Writer.TryComplete();
    }
}
