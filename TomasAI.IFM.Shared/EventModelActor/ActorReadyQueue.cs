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
        => !IsCompleted && _channel.Writer.TryWrite(threadId);

    public IAsyncEnumerable<ActorThreadId> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public void Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
            _channel.Writer.TryComplete();
    }
}
