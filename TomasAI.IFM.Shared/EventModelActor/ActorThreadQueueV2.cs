using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Bounded MPSC actor mailbox with one scheduled consumer at a time.
/// </summary>
/// <remarks>
/// Producers receive real asynchronous backpressure when the mailbox is full. The scheduling bit guarantees that
/// an actor/entity can occur only once in the ready queue, while <see cref="CompleteDrain"/> closes the enqueue versus
/// idle race without per-message semaphore signals.
/// </remarks>
public sealed class ActorThreadQueueV2 : IActorThreadQueue, IScheduledActorThreadQueue, IDisposable
{
    const int DefaultCapacity = 8192;
    const int Created = 0;
    const int Active = 1;
    const int Retiring = 2;
    const int Retired = 3;

    readonly object _startLock = new();
    readonly SemaphoreSlim _slots;
    Channel<QueuedActorMessage>? _channel;
    ActorThreadId _id;
    int _lifecycle;
    int _scheduled;
    int _writers;
    int _count;
    int _mailboxMetricActive;

    public ActorThreadQueueV2(int capacity = DefaultCapacity, int spinEnqueue = 32, int spinDequeue = 32)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _slots = new SemaphoreSlim(capacity, capacity);
        _ = spinEnqueue;
        _ = spinDequeue;
    }

    public ActorThreadId Id => _id;
    public int Count => Volatile.Read(ref _count);
    public bool IsStarted => Volatile.Read(ref _lifecycle) == Active;
    bool IScheduledActorThreadQueue.IsRetired => Volatile.Read(ref _lifecycle) == Retired;

    public IActorThreadQueue SetId(ActorThreadId id)
    {
        _id = IsArgumentNull.Set(id);
        return this;
    }

    public async IAsyncEnumerable<IActorMessage> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reader = _channel?.Reader;
        if (reader is null)
            yield break;

        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var queued))
            {
                Interlocked.Decrement(ref _count);
                _slots.Release();
                ActorRuntimeMetrics.RecordDequeued(queued.EnqueuedTimestamp, _id.ActorType);
                yield return queued.Message;
            }
        }
    }

    public IEnumerable<IActorMessage> ReadAll(CancellationToken cancellationToken = default)
    {
        var reader = _channel?.Reader;
        if (reader is null)
            yield break;

        while (!cancellationToken.IsCancellationRequested && TryReadCore(reader, out var message))
            yield return message;
    }

    public bool Write(IActorMessage message, CancellationToken cancellationToken = default)
        => ((IScheduledActorThreadQueue)this).TryWrite(message, cancellationToken);

    bool IScheduledActorThreadQueue.TryWrite(IActorMessage message, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(message);
        if (!TryAcquireWriter())
            return false;

        try
        {
            var enqueueStarted = ActorRuntimeMetrics.StartEnqueueWait();
            _slots.Wait(cancellationToken);
            ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
            if (!_channel!.Writer.TryWrite(new QueuedActorMessage(message, ActorRuntimeMetrics.StartQueueWait())))
            {
                _slots.Release();
                throw new ChannelClosedException();
            }
            Interlocked.Increment(ref _count);
            ActorRuntimeMetrics.RecordAccepted(_id.ActorType);
            return true;
        }
        finally
        {
            Interlocked.Decrement(ref _writers);
        }
    }

    public ValueTask EnqueueAsync(IActorMessage message, CancellationToken cancellationToken = default)
    {
        var pending = ((IScheduledActorThreadQueue)this).TryWriteAsync(message, cancellationToken);
        if (pending.IsCompletedSuccessfully)
            return pending.Result
                ? ValueTask.CompletedTask
                : ValueTask.FromException(new ObjectDisposedException(nameof(ActorThreadQueueV2)));

        return AwaitAccepted(pending);
    }

    ValueTask<bool> IScheduledActorThreadQueue.TryWriteAsync(
        IActorMessage message,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(message);
        if (!TryAcquireWriter())
            return ValueTask.FromResult(false);

        try
        {
            var enqueueStarted = ActorRuntimeMetrics.StartEnqueueWait();
            var pending = _slots.WaitAsync(cancellationToken);
            if (pending.IsCompletedSuccessfully)
            {
                ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
                if (!_channel!.Writer.TryWrite(new QueuedActorMessage(message, ActorRuntimeMetrics.StartQueueWait())))
                {
                    _slots.Release();
                    Interlocked.Decrement(ref _writers);
                    return ValueTask.FromException<bool>(new ChannelClosedException());
                }
                Interlocked.Increment(ref _count);
                ActorRuntimeMetrics.RecordAccepted(_id.ActorType);
                Interlocked.Decrement(ref _writers);
                return ValueTask.FromResult(true);
            }

            return AwaitSlotAndWrite(pending, message, enqueueStarted);
        }
        catch
        {
            Interlocked.Decrement(ref _writers);
            throw;
        }
    }

    async ValueTask<bool> AwaitSlotAndWrite(Task waitForSlot, IActorMessage message, long enqueueStarted)
    {
        try
        {
            await waitForSlot.ConfigureAwait(false);
            ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
            if (!_channel!.Writer.TryWrite(new QueuedActorMessage(message, ActorRuntimeMetrics.StartQueueWait())))
            {
                _slots.Release();
                throw new ChannelClosedException();
            }
            Interlocked.Increment(ref _count);
            ActorRuntimeMetrics.RecordAccepted(_id.ActorType);
            return true;
        }
        finally
        {
            Interlocked.Decrement(ref _writers);
        }
    }

    static async ValueTask AwaitAccepted(ValueTask<bool> pending)
    {
        if (!await pending.ConfigureAwait(false))
            throw new ObjectDisposedException(nameof(ActorThreadQueueV2));
    }

    bool TryAcquireWriter()
    {
        while (Volatile.Read(ref _lifecycle) == Active)
        {
            Interlocked.Increment(ref _writers);
            if (Volatile.Read(ref _lifecycle) == Active)
                return true;

            Interlocked.Decrement(ref _writers);
        }

        return false;
    }

    bool IScheduledActorThreadQueue.TryRead(out IActorMessage? message)
    {
        var reader = _channel?.Reader;
        if (reader is not null)
            return TryReadCore(reader, out message);

        message = null;
        return false;
    }

    bool TryReadCore(ChannelReader<QueuedActorMessage> reader, out IActorMessage? message)
    {
        if (!reader.TryRead(out var queued))
        {
            message = null;
            return false;
        }
        Interlocked.Decrement(ref _count);
        _slots.Release();
        ActorRuntimeMetrics.RecordDequeued(queued.EnqueuedTimestamp, _id.ActorType);
        message = queued.Message;
        return true;
    }

    bool IScheduledActorThreadQueue.TrySchedule()
        => Volatile.Read(ref _lifecycle) == Active
           && Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0;

    bool IScheduledActorThreadQueue.CompleteDrain() => CompleteDrain();

    bool CompleteDrain()
    {
        if (Count != 0)
            return true;

        Volatile.Write(ref _scheduled, 0);

        // A producer that observed scheduled == 1 does not publish another ready item. Recheck after clearing and
        // reclaim scheduling responsibility if that producer enqueued during the transition.
        return Count != 0 && Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0;
    }

    bool IScheduledActorThreadQueue.TryRetire()
    {
        if (Volatile.Read(ref _scheduled) != 0 || Count != 0)
            return false;

        if (Interlocked.CompareExchange(ref _lifecycle, Retiring, Active) != Active)
            return false;

        if (Volatile.Read(ref _writers) != 0 || Volatile.Read(ref _scheduled) != 0 || Count != 0)
        {
            Volatile.Write(ref _lifecycle, Active);
            return false;
        }

        Volatile.Write(ref _lifecycle, Retired);
        RecordMailboxStopped();
        return true;
    }

    public void Start()
    {
        if (Volatile.Read(ref _lifecycle) == Active)
            return;

        lock (_startLock)
        {
            if (_lifecycle == Active)
                return;
            if (_lifecycle == Retired)
                throw new ObjectDisposedException(nameof(ActorThreadQueueV2));

            _channel = Channel.CreateUnbounded<QueuedActorMessage>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            Volatile.Write(ref _lifecycle, Active);
            if (Interlocked.Exchange(ref _mailboxMetricActive, 1) == 0)
                ActorRuntimeMetrics.RecordMailboxStarted(_id.ActorType);
        }
    }

    public void Stop()
    {
        var previous = Interlocked.Exchange(ref _lifecycle, Retired);
        if (previous == Retired)
            return;

        RecordMailboxStopped();

        var channel = _channel;
        channel?.Writer.TryComplete();
        if (channel is not null)
        {
            while (channel.Reader.TryRead(out var pending))
            {
                Interlocked.Decrement(ref _count);
                _slots.Release();
                ActorRuntimeMetrics.RecordDequeued(pending.EnqueuedTimestamp, _id.ActorType);
                pending.Message.Dispose();
            }
        }
        _channel = null;
    }

    public void Dispose() => Stop();

    void RecordMailboxStopped()
    {
        if (Interlocked.Exchange(ref _mailboxMetricActive, 0) != 0)
            ActorRuntimeMetrics.RecordMailboxStopped(_id.ActorType);
    }

    readonly record struct QueuedActorMessage(IActorMessage Message, long EnqueuedTimestamp);
}
