using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Owns the entity mailboxes for one actor and publishes each non-empty mailbox to the shared actor scheduler once.
/// </summary>
public sealed class ActorThreadQueues(
    IActorSupervisor supervisor,
    int maxRetainedIdleQueues = ActorAdmissionOptions.ExistingRetainedIdleMailboxesPerActor,
    ActorAdmissionController? admissionController = null,
    ActorMetricsStore? metrics = null) : IActorThreadQueues
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorAdmissionController _admissionController =
        admissionController ?? ActorAdmissionController.Disabled;
    readonly int _maxRetainedIdleQueues = maxRetainedIdleQueues >= 0
        ? maxRetainedIdleQueues
        : throw new ArgumentOutOfRangeException(nameof(maxRetainedIdleQueues));
    readonly ActorMetricsStore? _metrics = metrics;
    readonly ConcurrentDictionary<ActorThreadId, IActorThreadQueue> _threadQueues = new();
    readonly ConcurrentDictionary<ActorThreadId, byte> _paused = new();
    readonly ConcurrentDictionary<ActorThreadId, long> _generations = new();

    // Count before publication so a concurrent release cannot miss a newly published queue.
    // Pending additions/removals can temporarily overestimate retention; Count remains exact for diagnostics.
    int _publishedOrPendingQueues;
    int _accepting = 1;

    public int Count => _threadQueues.Count;
    public bool IsAccepting => Volatile.Read(ref _accepting) != 0;

    public bool Write(IActorMessage message)
        => Write(message, message.Subject);

    public bool Write(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default)
        => TryAdmit(message, subject, cancellationToken).Accepted;

    public ActorAdmissionResult TryAdmit(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default)
    {
        IsArgumentNull.Check(message);
        var threadId = subject.ThreadId;
        if (!IsAdmissionOpen(threadId))
        {
            RecordRejected(threadId);
            return ActorAdmissionResult.Rejected(ActorAdmissionReason.Stopping);
        }
        var admission = _admissionController.TryReserve(message, threadId.ActorType, out var charge);
        if (!admission.Accepted)
        {
            RecordRejected(threadId);
            return admission;
        }

        var reservationOwned = true;
        try
        {
            var scheduler = _supervisor.ThreadPool as ActorThreadPoolV2;
            IActorThread? thread = null;
            if (scheduler is null)
                thread = _supervisor.GetThread(threadId);
            else
                scheduler.ValidateAdmission(threadId, default);
            while (true)
            {
                var queue = GetThreadQueue(threadId);
                if (queue is not IScheduledActorThreadQueue scheduled)
                    throw CreateQueueConfigurationException(queue);

                var result = scheduled.TryWriteReserved(message, charge, cancellationToken);
                if (result.Reason == ActorAdmissionReason.MailboxRetired
                    || result.Reason == ActorAdmissionReason.Stopping
                    && (!_threadQueues.TryGetValue(threadId, out var current)
                        || !ReferenceEquals(current, queue)))
                {
                    RemoveRetired(threadId, queue);
                    continue;
                }

                if (result.Accepted)
                {
                    reservationOwned = false;
                    _metrics?.GetOrRegister(threadId, queue).RecordAccepted();
                    if (scheduled.TrySchedule())
                    {
                        if (scheduler is null)
                            thread!.SignalMessageAvailable(threadId);
                        else
                            scheduler.SignalMailbox(threadId);
                    }
                    return result;
                }

                _admissionController.Release(charge);
                reservationOwned = false;
                RecordRejected(threadId);
                return result;
            }
        }
        finally
        {
            if (reservationOwned)
                _admissionController.Release(charge);
        }
    }

    public ValueTask<bool> WriteAsync(
        IActorMessage message,
        CancellationToken cancellationToken = default)
        => WriteAsync(message, message.Subject, cancellationToken);

    public ValueTask<bool> WriteAsync(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default)
    {
        var pending = TryAdmitAsync(message, subject, cancellationToken);
        if (pending.IsCompletedSuccessfully)
            return ValueTask.FromResult(pending.Result.Accepted);
        return AwaitBooleanResult(pending);
    }

    public async ValueTask<ActorAdmissionResult> TryAdmitAsync(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default)
    {
        IsArgumentNull.Check(message);
        var threadId = subject.ThreadId;
        if (!IsAdmissionOpen(threadId))
        {
            RecordRejected(threadId);
            return ActorAdmissionResult.Rejected(ActorAdmissionReason.Stopping);
        }
        var admission = _admissionController.TryReserve(message, threadId.ActorType, out var charge);
        if (!admission.Accepted)
        {
            RecordRejected(threadId);
            return admission;
        }

        var reservationOwned = true;
        try
        {
            var scheduler = _supervisor.ThreadPool as ActorThreadPoolV2;
            IActorThread? thread = null;
            if (scheduler is null)
                thread = await _supervisor.GetThreadAsync(threadId, cancellationToken).ConfigureAwait(false);
            else
                scheduler.ValidateAdmission(threadId, cancellationToken);
            while (true)
            {
                var queue = GetThreadQueue(threadId);
                if (queue is not IScheduledActorThreadQueue scheduled)
                    throw CreateQueueConfigurationException(queue);

                var result = await scheduled
                    .TryWriteReservedAsync(message, charge, cancellationToken)
                    .ConfigureAwait(false);
                if (result.Reason == ActorAdmissionReason.MailboxRetired
                    || result.Reason == ActorAdmissionReason.Stopping
                    && (!_threadQueues.TryGetValue(threadId, out var current)
                        || !ReferenceEquals(current, queue)))
                {
                    RemoveRetired(threadId, queue);
                    continue;
                }

                if (result.Accepted)
                {
                    reservationOwned = false;
                    _metrics?.GetOrRegister(threadId, queue).RecordAccepted();
                    if (scheduled.TrySchedule())
                    {
                        if (scheduler is null)
                            thread!.SignalMessageAvailable(threadId);
                        else
                            scheduler.SignalMailbox(threadId);
                    }
                    return result;
                }

                _admissionController.Release(charge);
                reservationOwned = false;
                RecordRejected(threadId);
                return result;
            }
        }
        finally
        {
            if (reservationOwned)
                _admissionController.Release(charge);
        }
    }

    static async ValueTask<bool> AwaitBooleanResult(ValueTask<ActorAdmissionResult> pending)
        => (await pending.ConfigureAwait(false)).Accepted;

    public IActorThreadQueue GetThreadQueue(ActorThreadId threadId)
    {
        while (true)
        {
            if (_threadQueues.TryGetValue(threadId, out var existing))
            {
                if (existing is not IScheduledActorThreadQueue scheduled || !scheduled.IsRetired)
                    return existing;
                RemoveRetired(threadId, existing);
            }

            var created = _supervisor.Container.Resolve<IActorThreadQueue>();
            created.SetId(threadId);
            created.Start();
            Interlocked.Increment(ref _publishedOrPendingQueues);
            if (_threadQueues.TryAdd(threadId, created))
            {
                var generation = _generations.TryGetValue(threadId, out var restartedGeneration)
                    ? restartedGeneration
                    : 1;
                _metrics?.RegisterMailbox(threadId, created, generation);
                return created;
            }
            Interlocked.Decrement(ref _publishedOrPendingQueues);
            created.Stop();
        }
    }

    public bool TryGetThreadQueue(ActorThreadId threadId, out IActorThreadQueue? queue)
        => _threadQueues.TryGetValue(threadId, out queue);

    public void PauseAdmission()
    {
        Volatile.Write(ref _accepting, 0);
        if (_metrics is not null)
            foreach (var pair in _threadQueues)
                _metrics.GetOrRegister(pair.Key, pair.Value).SetAdmission(false);
    }

    public void ResumeAdmission()
    {
        Volatile.Write(ref _accepting, 1);
        if (_metrics is not null)
            foreach (var pair in _threadQueues)
                _metrics.GetOrRegister(pair.Key, pair.Value)
                    .SetAdmission(!_paused.ContainsKey(pair.Key));
    }
    public bool IsAdmissionOpen(ActorThreadId threadId)
        => IsAccepting && !_paused.ContainsKey(threadId);

    public void PauseAdmission(ActorThreadId threadId)
    {
        _paused[threadId] = 0;
        if (_metrics is not null && _threadQueues.TryGetValue(threadId, out var queue))
        {
            var mailbox = _metrics.GetOrRegister(threadId, queue);
            mailbox.SetAdmission(false);
            mailbox.SetLifecycle(ActorMailboxLifecycleState.Draining);
        }
    }

    public void ResumeAdmission(ActorThreadId threadId)
    {
        _paused.TryRemove(threadId, out _);
        if (_metrics is not null && _threadQueues.TryGetValue(threadId, out var queue))
        {
            var mailbox = _metrics.GetOrRegister(threadId, queue);
            mailbox.SetAdmission(true);
            mailbox.SetLifecycle(ActorMailboxLifecycleState.Running);
        }
    }

    public async ValueTask<bool> WaitForIdleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var deadline = timeout == Timeout.InfiniteTimeSpan ? DateTime.MaxValue : DateTime.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_threadQueues.Values.All(queue => queue.Count == 0)
                && (_metrics?.CaptureSnapshot().Mailboxes.All(mailbox => !mailbox.IsProcessing) ?? true))
                return true;
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<bool> WaitForIdleAsync(
        ActorThreadId threadId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var deadline = timeout == Timeout.InfiniteTimeSpan ? DateTime.MaxValue : DateTime.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var queued = _threadQueues.TryGetValue(threadId, out var queue) ? queue.Count : 0;
            var processing = _metrics?.TryGetMailboxSnapshot(threadId, out var snapshot) == true
                && snapshot?.IsProcessing == true;
            if (queued == 0 && !processing)
            {
                if (_metrics?.TryGetMailboxSnapshot(threadId, out _) == true
                    && _threadQueues.TryGetValue(threadId, out var idleQueue))
                    _metrics.GetOrRegister(threadId, idleQueue).SetLifecycle(ActorMailboxLifecycleState.Paused);
                return true;
            }
            if (DateTime.UtcNow >= deadline)
            {
                if (_metrics is not null && _threadQueues.TryGetValue(threadId, out var stalledQueue))
                    _metrics.GetOrRegister(threadId, stalledQueue).SetLifecycle(ActorMailboxLifecycleState.Quarantined);
                return false;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
        }
    }

    public bool Retire(ActorThreadId threadId)
    {
        if (!_threadQueues.TryGetValue(threadId, out var queue))
            return true;
        if (queue.Count != 0)
            return false;
        if (queue is IScheduledActorThreadQueue scheduled && !scheduled.TryRetire())
            return false;
        if (!((ICollection<KeyValuePair<ActorThreadId, IActorThreadQueue>>)_threadQueues)
            .Remove(new(threadId, queue)))
            return false;
        Interlocked.Decrement(ref _publishedOrPendingQueues);
        _generations.AddOrUpdate(threadId, 2, static (_, generation) => generation + 1);
        if (_metrics is not null)
            _metrics.GetOrRegister(threadId, queue).SetLifecycle(ActorMailboxLifecycleState.Retired);
        _metrics?.RemoveMailbox(threadId, queue);
        queue.Stop();
        return true;
    }

    public void ReleaseThreadQueue(ActorThreadId threadId)
    {
        // Keep the normal actor working set warm. Beyond the bound, newly idle high-cardinality mailboxes are
        // retired immediately so memory remains bounded without allocating a timer or an eviction task per actor.
        if (Volatile.Read(ref _publishedOrPendingQueues) <= _maxRetainedIdleQueues)
            return;

        if (!_threadQueues.TryGetValue(threadId, out var queue))
            return;

        if (queue is IScheduledActorThreadQueue scheduled && !scheduled.TryRetire())
            return;
        if (queue is not IScheduledActorThreadQueue && queue.Count != 0)
            return;

        if (((ICollection<KeyValuePair<ActorThreadId, IActorThreadQueue>>)_threadQueues)
            .Remove(new KeyValuePair<ActorThreadId, IActorThreadQueue>(threadId, queue)))
        {
            Interlocked.Decrement(ref _publishedOrPendingQueues);
            _metrics?.RemoveMailbox(threadId, queue);
            queue.Stop();
        }
    }

    void RemoveRetired(ActorThreadId threadId, IActorThreadQueue queue)
    {
        if (queue is not IScheduledActorThreadQueue { IsRetired: true })
            return;

        if (((ICollection<KeyValuePair<ActorThreadId, IActorThreadQueue>>)_threadQueues)
            .Remove(new KeyValuePair<ActorThreadId, IActorThreadQueue>(threadId, queue)))
        {
            Interlocked.Decrement(ref _publishedOrPendingQueues);
            _metrics?.RemoveMailbox(threadId, queue);
            queue.Stop();
        }
    }

    void RecordRejected(ActorThreadId threadId)
    {
        if (_metrics is null || !_threadQueues.TryGetValue(threadId, out var queue))
            return;
        _metrics.GetOrRegister(threadId, queue).RecordRejected();
    }

    static InvalidOperationException CreateQueueConfigurationException(IActorThreadQueue queue)
        => new(
            $"{nameof(ActorThreadPoolV2)} requires an {nameof(ActorThreadQueueV2)} mailbox, "
            + $"but the container resolved {queue.GetType().FullName}.");
}
