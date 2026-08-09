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
    readonly ActorAdmissionController _admissionController;
    Channel<QueuedActorMessage>? _channel;
    ActorThreadId _id;
    int _lifecycle;
    int _scheduled;
    int _writers;
    int _count;
    int _mailboxMetricActive;
    int _stopRequested;
    int _stopOnce;

    public ActorThreadQueueV2(int capacity = DefaultCapacity, int spinEnqueue = 32, int spinDequeue = 32)
        : this(ActorAdmissionController.Disabled, capacity, spinEnqueue, spinDequeue)
    {
    }

    public ActorThreadQueueV2(
        ActorAdmissionController admissionController,
        int capacity = DefaultCapacity,
        int spinEnqueue = 32,
        int spinDequeue = 32)
    {
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
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
                _admissionController.Release(queued.AdmissionCharge);
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
        var admission = _admissionController.TryReserve(message, _id.ActorType, out var charge);
        if (!admission.Accepted)
            return false;

        try
        {
            var result = ((IScheduledActorThreadQueue)this)
                .TryWriteReserved(message, charge, cancellationToken);
            if (result.Accepted)
                return true;
            _admissionController.Release(charge);
            return false;
        }
        catch
        {
            _admissionController.Release(charge);
            throw;
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
        var admission = _admissionController.TryReserve(message, _id.ActorType, out var charge);
        if (!admission.Accepted)
            return ValueTask.FromResult(false);

        ValueTask<ActorAdmissionResult> pending;
        try
        {
            pending = ((IScheduledActorThreadQueue)this)
                .TryWriteReservedAsync(message, charge, cancellationToken);
        }
        catch
        {
            _admissionController.Release(charge);
            throw;
        }
        if (pending.IsCompletedSuccessfully)
        {
            if (pending.Result.Accepted)
                return ValueTask.FromResult(true);
            _admissionController.Release(charge);
            return ValueTask.FromResult(false);
        }

        return AwaitCompatibilityWrite(pending, charge);
    }

    ActorAdmissionResult IScheduledActorThreadQueue.TryWriteReserved(
        IActorMessage message,
        ActorAdmissionCharge charge,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(message);
        if (!TryAcquireWriter(out var unavailableReason))
            return RejectQueueAdmission(unavailableReason);

        try
        {
            var enqueueStarted = ActorRuntimeMetrics.StartEnqueueWait();
            if (_admissionController.Mode == ActorAdmissionMode.Enforce)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_slots.Wait(0))
                    return RejectQueueAdmission(ActorAdmissionReason.MailboxLimit);
            }
            else
            {
                if (_slots.CurrentCount == 0)
                    _admissionController.RecordQueueRejection(_id.ActorType, ActorAdmissionReason.MailboxLimit);
                _slots.Wait(cancellationToken);
            }

            ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
            return PublishReserved(message, charge);
        }
        finally
        {
            Interlocked.Decrement(ref _writers);
        }
    }

    ValueTask<ActorAdmissionResult> IScheduledActorThreadQueue.TryWriteReservedAsync(
        IActorMessage message,
        ActorAdmissionCharge charge,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(message);
        if (!TryAcquireWriter(out var unavailableReason))
            return ValueTask.FromResult(RejectQueueAdmission(unavailableReason));

        try
        {
            var enqueueStarted = ActorRuntimeMetrics.StartEnqueueWait();
            if (_admissionController.Mode == ActorAdmissionMode.Enforce)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = _slots.Wait(0)
                    ? PublishReserved(message, charge)
                    : RejectQueueAdmission(ActorAdmissionReason.MailboxLimit);
                Interlocked.Decrement(ref _writers);
                return ValueTask.FromResult(result);
            }

            var pending = _slots.WaitAsync(cancellationToken);
            if (pending.IsCompletedSuccessfully)
            {
                ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
                var result = PublishReserved(message, charge);
                Interlocked.Decrement(ref _writers);
                return ValueTask.FromResult(result);
            }

            _admissionController.RecordQueueRejection(_id.ActorType, ActorAdmissionReason.MailboxLimit);
            return AwaitSlotAndWrite(pending, message, charge, enqueueStarted);
        }
        catch
        {
            Interlocked.Decrement(ref _writers);
            throw;
        }
    }

    async ValueTask<ActorAdmissionResult> AwaitSlotAndWrite(
        Task waitForSlot,
        IActorMessage message,
        ActorAdmissionCharge charge,
        long enqueueStarted)
    {
        try
        {
            await waitForSlot.ConfigureAwait(false);
            ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
            return PublishReserved(message, charge);
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

    async ValueTask<bool> AwaitCompatibilityWrite(
        ValueTask<ActorAdmissionResult> pending,
        ActorAdmissionCharge charge)
    {
        try
        {
            var result = await pending.ConfigureAwait(false);
            if (result.Accepted)
                return true;
            _admissionController.Release(charge);
            return false;
        }
        catch
        {
            _admissionController.Release(charge);
            throw;
        }
    }

    ActorAdmissionResult PublishReserved(IActorMessage message, ActorAdmissionCharge charge)
    {
        var channel = Volatile.Read(ref _channel);
        Interlocked.Increment(ref _count);
        if (channel is null || !channel.Writer.TryWrite(new QueuedActorMessage(
                message,
                ActorRuntimeMetrics.StartQueueWait(),
                charge)))
        {
            Interlocked.Decrement(ref _count);
            _slots.Release();
            return RejectQueueAdmission(ActorAdmissionReason.Stopping);
        }
        ActorRuntimeMetrics.RecordAccepted(_id.ActorType);
        return ActorAdmissionResult.AcceptedResult;
    }

    ActorAdmissionResult RejectQueueAdmission(ActorAdmissionReason reason)
    {
        if (reason != ActorAdmissionReason.MailboxRetired)
            _admissionController.RecordQueueRejection(_id.ActorType, reason);
        return ActorAdmissionResult.Rejected(reason);
    }

    bool TryAcquireWriter(out ActorAdmissionReason unavailableReason)
    {
        while (Volatile.Read(ref _lifecycle) == Active)
        {
            Interlocked.Increment(ref _writers);
            if (Volatile.Read(ref _lifecycle) == Active)
            {
                unavailableReason = ActorAdmissionReason.None;
                return true;
            }

            Interlocked.Decrement(ref _writers);
        }

        unavailableReason = Volatile.Read(ref _stopRequested) != 0
            ? ActorAdmissionReason.Stopping
            : ActorAdmissionReason.MailboxRetired;
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
        _admissionController.Release(queued.AdmissionCharge);
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
        Volatile.Write(ref _stopRequested, 1);
        Interlocked.Exchange(ref _lifecycle, Retired);
        if (Interlocked.Exchange(ref _stopOnce, 1) != 0)
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
                _admissionController.Release(pending.AdmissionCharge);
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

    readonly record struct QueuedActorMessage(
        IActorMessage Message,
        long EnqueuedTimestamp,
        ActorAdmissionCharge AdmissionCharge);
}
