using System.Numerics;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Bounded SPSC ring-backed actor mailbox with one logical stripe producer and
/// one scheduled consumer.
/// </summary>
/// <remarks>
/// This is an interchangeable alternative to <see cref="ActorThreadQueueV2"/>. One logical dispatch stripe publishes each entity's messages, while the actor scheduling bit preserves
/// per-entity single-consumer execution. Producer and consumer execution may migrate between OS threads.
/// </remarks>
public sealed class ActorThreadQueueSpscRing : IActorThreadQueue, IScheduledActorThreadQueue, IDisposable
{
    const int DefaultCapacity = 8192;
    const int Created = 0;
    const int Active = 1;
    const int Retiring = 2;
    const int Retired = 3;

    readonly object _startLock = new();
    readonly SemaphoreSlim _slotAvailable;
    readonly SemaphoreSlim _itemAvailable;
    readonly ActorAdmissionController _admissionController;
    readonly BoundedSpscRingBuffer<QueuedActorMessage> _ring;
    ActorThreadId _id;
    int _lifecycle;
    int _scheduled;
    int _writerActive;
    int _mailboxMetricActive;
    int _stopRequested;
    int _stopOnce;
    int _itemSignalSet;
    int _asyncReaderWaiting;
    int _slotSignalSet;
    int _producerWaiting;

    public ActorThreadQueueSpscRing(int capacity = DefaultCapacity)
        : this(ActorAdmissionController.Disabled, capacity)
    {
    }

    public ActorThreadQueueSpscRing(
        ActorAdmissionController admissionController,
        int capacity = DefaultCapacity)
    {
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        if (capacity <= 0 || !BitOperations.IsPow2(capacity))
            throw new ArgumentOutOfRangeException(nameof(capacity), "SPSC ring capacity must be a positive power of two.");
        _ring = new BoundedSpscRingBuffer<QueuedActorMessage>(capacity);
        _slotAvailable = new SemaphoreSlim(0, 1);
        _itemAvailable = new SemaphoreSlim(0, 1);
    }

    public ActorThreadId Id => _id;
    public int Count => _ring.Count;
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
        while (true)
        {
            if (TryReadPublished(out var message))
            {
                yield return message!;
                continue;
            }

            if (Volatile.Read(ref _lifecycle) != Active && Count == 0)
                yield break;

            Volatile.Write(ref _asyncReaderWaiting, 1);
            if (TryReadPublished(out message))
            {
                Volatile.Write(ref _asyncReaderWaiting, 0);
                yield return message!;
                continue;
            }
            if (Volatile.Read(ref _lifecycle) != Active && Count == 0)
            {
                Volatile.Write(ref _asyncReaderWaiting, 0);
                yield break;
            }

            try
            {
                await _itemAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _itemSignalSet, 0);
            }
            finally
            {
                Volatile.Write(ref _asyncReaderWaiting, 0);
            }
        }
    }

    public IEnumerable<IActorMessage> ReadAll(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested
               && ((IScheduledActorThreadQueue)this).TryRead(out var message))
        {
            yield return message!;
        }
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
                : ValueTask.FromException(new ObjectDisposedException(nameof(ActorThreadQueueSpscRing)));

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
                return TryPublishReserved(message, charge, out var reason)
                    ? ActorAdmissionResult.AcceptedResult
                    : RejectQueueAdmission(reason);
            }

            while (!TryPublishReserved(message, charge, out var capacityReason))
            {
                if (capacityReason != ActorAdmissionReason.MailboxLimit)
                    return RejectQueueAdmission(capacityReason);
                _admissionController.RecordQueueRejection(_id.ActorType, capacityReason);
                WaitForSlot(cancellationToken);
            }

            ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
            return ActorAdmissionResult.AcceptedResult;
        }
        finally
        {
            Volatile.Write(ref _writerActive, 0);
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
            if (TryPublishReserved(message, charge, out var reason))
            {
                ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
                Volatile.Write(ref _writerActive, 0);
                return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
            }

            if (_admissionController.Mode == ActorAdmissionMode.Enforce)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = RejectQueueAdmission(reason);
                Volatile.Write(ref _writerActive, 0);
                return ValueTask.FromResult(result);
            }

            if (reason != ActorAdmissionReason.MailboxLimit)
            {
                var result = RejectQueueAdmission(reason);
                Volatile.Write(ref _writerActive, 0);
                return ValueTask.FromResult(result);
            }

            _admissionController.RecordQueueRejection(_id.ActorType, reason);
            return AwaitSlotAndWrite(message, charge, enqueueStarted, cancellationToken);
        }
        catch
        {
            Volatile.Write(ref _writerActive, 0);
            throw;
        }
    }

    async ValueTask<ActorAdmissionResult> AwaitSlotAndWrite(
        IActorMessage message,
        ActorAdmissionCharge charge,
        long enqueueStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await WaitForSlotAsync(cancellationToken).ConfigureAwait(false);
                if (TryPublishReserved(message, charge, out var reason))
                {
                    ActorRuntimeMetrics.RecordEnqueueWait(enqueueStarted, _id.ActorType);
                    return ActorAdmissionResult.AcceptedResult;
                }
                if (reason != ActorAdmissionReason.MailboxLimit)
                    return RejectQueueAdmission(reason);
                _admissionController.RecordQueueRejection(_id.ActorType, reason);
            }
        }
        finally
        {
            Volatile.Write(ref _writerActive, 0);
        }
    }

    static async ValueTask AwaitAccepted(ValueTask<bool> pending)
    {
        if (!await pending.ConfigureAwait(false))
            throw new ObjectDisposedException(nameof(ActorThreadQueueSpscRing));
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

    bool TryPublishReserved(
        IActorMessage message,
        ActorAdmissionCharge charge,
        out ActorAdmissionReason unavailableReason)
    {
        if (!_ring.TryEnqueue(new QueuedActorMessage(
                message,
                ActorRuntimeMetrics.StartQueueWait(),
                charge)))
        {
            unavailableReason = _ring.IsCompleted
                ? ActorAdmissionReason.Stopping
                : ActorAdmissionReason.MailboxLimit;
            return false;
        }

        if (Volatile.Read(ref _asyncReaderWaiting) != 0)
            SignalItemAvailable();
        ActorRuntimeMetrics.RecordAccepted(_id.ActorType);
        unavailableReason = ActorAdmissionReason.None;
        return true;
    }

    void WaitForSlot(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _producerWaiting, 1);
        try
        {
            if (!_ring.IsFull || _ring.IsCompleted)
                return;
            _slotAvailable.Wait(cancellationToken);
            Volatile.Write(ref _slotSignalSet, 0);
        }
        finally
        {
            Volatile.Write(ref _producerWaiting, 0);
        }
    }

    async ValueTask WaitForSlotAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _producerWaiting, 1);
        try
        {
            if (!_ring.IsFull || _ring.IsCompleted)
                return;
            await _slotAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _slotSignalSet, 0);
        }
        finally
        {
            Volatile.Write(ref _producerWaiting, 0);
        }
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
            Volatile.Write(ref _writerActive, 1);
            if (Volatile.Read(ref _lifecycle) == Active)
            {
                unavailableReason = ActorAdmissionReason.None;
                return true;
            }

            Volatile.Write(ref _writerActive, 0);
        }

        unavailableReason = Volatile.Read(ref _stopRequested) != 0
            ? ActorAdmissionReason.Stopping
            : ActorAdmissionReason.MailboxRetired;
        return false;
    }

    bool IScheduledActorThreadQueue.TryRead(out IActorMessage? message)
        => TryReadPublished(out message);

    bool TryReadPublished(out IActorMessage? message)
    {
        if (!_ring.TryDequeue(out var queued))
        {
            message = null;
            return false;
        }

        if (Volatile.Read(ref _producerWaiting) != 0)
            SignalSlotAvailable();
        _admissionController.Release(queued.AdmissionCharge);
        ActorRuntimeMetrics.RecordDequeued(queued.EnqueuedTimestamp, _id.ActorType);
        message = queued.Message;
        return true;
    }

    bool IScheduledActorThreadQueue.TrySchedule()
        => Volatile.Read(ref _lifecycle) == Active
           && Volatile.Read(ref _scheduled) == 0
           && Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0;

    bool IScheduledActorThreadQueue.CompleteDrain() => CompleteDrain();

    bool CompleteDrain()
    {
        if (!_ring.IsEmpty)
            return true;

        Volatile.Write(ref _scheduled, 0);
        return !_ring.IsEmpty && Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0;
    }

    bool IScheduledActorThreadQueue.TryRetire()
    {
        if (Volatile.Read(ref _scheduled) != 0 || !_ring.IsEmpty)
            return false;

        if (Interlocked.CompareExchange(ref _lifecycle, Retiring, Active) != Active)
            return false;

        if (Volatile.Read(ref _writerActive) != 0
            || Volatile.Read(ref _scheduled) != 0
            || !_ring.IsEmpty)
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
                throw new ObjectDisposedException(nameof(ActorThreadQueueSpscRing));

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
        _ring.Complete();

        var spinner = new SpinWait();
        while (Volatile.Read(ref _writerActive) != 0)
        {
            DrainPending(dispose: true);
            spinner.SpinOnce();
        }

        DrainPending(dispose: true);
        if (Volatile.Read(ref _producerWaiting) != 0)
            SignalSlotAvailable();
        if (Volatile.Read(ref _asyncReaderWaiting) != 0)
            SignalItemAvailable();
    }

    void DrainPending(bool dispose)
    {
        while (TryReadPublished(out var message))
        {
            if (dispose)
                message!.Dispose();
        }
    }

    void SignalItemAvailable()
    {
        if (Interlocked.Exchange(ref _itemSignalSet, 1) == 0)
            _itemAvailable.Release();
    }

    void SignalSlotAvailable()
    {
        if (Interlocked.Exchange(ref _slotSignalSet, 1) == 0)
            _slotAvailable.Release();
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
