using System.Numerics;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Bounded MPSC ring-backed actor mailbox with one scheduled consumer at a time.</summary>
/// <remarks>
/// This is an interchangeable alternative to <see cref="ActorThreadQueueV2"/>. Multiple producers reserve
/// sequence-stamped slots concurrently, while the actor scheduling bit preserves per-entity single-consumer execution.
/// </remarks>
public sealed class ActorThreadQueueMpscRing : IActorThreadQueue, IScheduledActorThreadQueue, IDisposable
{
    const int DefaultCapacity = 8192;
    const int Created = 0;
    const int Active = 1;
    const int Retiring = 2;
    const int Retired = 3;

    readonly object _startLock = new();
    readonly int _capacity;
    readonly SemaphoreSlim _slots;
    readonly SemaphoreSlim _itemAvailable;
    readonly ActorAdmissionController _admissionController;
    BoundedMpscRingBuffer<QueuedActorMessage>? _ring;
    ActorThreadId _id;
    int _lifecycle;
    int _scheduled;
    int _writers;
    int _count;
    int _mailboxMetricActive;
    int _stopRequested;
    int _stopOnce;
    int _itemSignalSet;
    int _asyncReaderWaiting;

    public ActorThreadQueueMpscRing(int capacity = DefaultCapacity)
        : this(ActorAdmissionController.Disabled, capacity)
    {
    }

    public ActorThreadQueueMpscRing(
        ActorAdmissionController admissionController,
        int capacity = DefaultCapacity)
    {
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        if (capacity <= 0 || !BitOperations.IsPow2(capacity))
            throw new ArgumentOutOfRangeException(nameof(capacity), "MPSC ring capacity must be a positive power of two.");
        _capacity = capacity;
        _slots = new SemaphoreSlim(capacity, capacity);
        _itemAvailable = new SemaphoreSlim(0, 1);
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
                : ValueTask.FromException(new ObjectDisposedException(nameof(ActorThreadQueueMpscRing)));

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
            throw new ObjectDisposedException(nameof(ActorThreadQueueMpscRing));
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
        var ring = Volatile.Read(ref _ring);
        var count = Interlocked.Increment(ref _count);
        if (ring is null || !ring.TryEnqueueReserved(new QueuedActorMessage(
                message,
                ActorRuntimeMetrics.StartQueueWait(),
                charge)))
        {
            Interlocked.Decrement(ref _count);
            _slots.Release();
            return RejectQueueAdmission(ActorAdmissionReason.Stopping);
        }

        if (count == 1 && Volatile.Read(ref _asyncReaderWaiting) != 0)
            SignalItemAvailable();
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
        => TryReadPublished(out message);

    bool TryReadPublished(out IActorMessage? message)
    {
        var ring = Volatile.Read(ref _ring);
        if (ring is null || !ring.TryDequeue(out var queued))
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
                throw new ObjectDisposedException(nameof(ActorThreadQueueMpscRing));

            _ring = new BoundedMpscRingBuffer<QueuedActorMessage>(_capacity);
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
        var ring = Volatile.Read(ref _ring);
        ring?.Complete();

        var spinner = new SpinWait();
        while (Volatile.Read(ref _writers) != 0)
        {
            DrainPending(dispose: true);
            spinner.SpinOnce();
        }

        DrainPending(dispose: true);
        _ring = null;
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
