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

    public bool IsCompleted => Volatile.Read(ref _completed) != 0;

    public bool Schedule(ActorThreadId threadId)
    {
        if (IsCompleted || !_channel.Writer.TryWrite(threadId))
            return false;

        ActorRuntimeMetrics.RecordReadyScheduled(threadId.ActorType);
        return true;
    }

    public async IAsyncEnumerable<ActorThreadId> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var threadId in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
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
