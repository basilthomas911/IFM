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
    ActorAdmissionController? admissionController = null) : IActorThreadQueues
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorAdmissionController _admissionController =
        admissionController ?? ActorAdmissionController.Disabled;
    readonly int _maxRetainedIdleQueues = maxRetainedIdleQueues >= 0
        ? maxRetainedIdleQueues
        : throw new ArgumentOutOfRangeException(nameof(maxRetainedIdleQueues));
    readonly ConcurrentDictionary<ActorThreadId, IActorThreadQueue> _threadQueues = new();

    public int Count => _threadQueues.Count;

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
        var admission = _admissionController.TryReserve(message, threadId.ActorType, out var charge);
        if (!admission.Accepted)
            return admission;

        var reservationOwned = true;
        try
        {
            var thread = _supervisor.GetThread(threadId);
            while (true)
            {
                var queue = GetThreadQueue(threadId);
                if (queue is not IScheduledActorThreadQueue scheduled)
                    throw CreateQueueConfigurationException(queue);

                var result = scheduled.TryWriteReserved(message, charge, cancellationToken);
                if (result.Reason == ActorAdmissionReason.MailboxRetired)
                {
                    RemoveRetired(threadId, queue);
                    continue;
                }

                if (result.Accepted)
                {
                    reservationOwned = false;
                    if (scheduled.TrySchedule())
                        thread.SignalMessageAvailable(threadId);
                    return result;
                }

                _admissionController.Release(charge);
                reservationOwned = false;
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
        var admission = _admissionController.TryReserve(message, threadId.ActorType, out var charge);
        if (!admission.Accepted)
            return admission;

        var reservationOwned = true;
        try
        {
            var thread = await _supervisor.GetThreadAsync(threadId, cancellationToken).ConfigureAwait(false);
            while (true)
            {
                var queue = GetThreadQueue(threadId);
                if (queue is not IScheduledActorThreadQueue scheduled)
                    throw CreateQueueConfigurationException(queue);

                var result = await scheduled
                    .TryWriteReservedAsync(message, charge, cancellationToken)
                    .ConfigureAwait(false);
                if (result.Reason == ActorAdmissionReason.MailboxRetired)
                {
                    RemoveRetired(threadId, queue);
                    continue;
                }

                if (result.Accepted)
                {
                    reservationOwned = false;
                    if (scheduled.TrySchedule())
                        thread.SignalMessageAvailable(threadId);
                    return result;
                }

                _admissionController.Release(charge);
                reservationOwned = false;
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
            if (_threadQueues.TryAdd(threadId, created))
                return created;
            created.Stop();
        }
    }

    public bool TryGetThreadQueue(ActorThreadId threadId, out IActorThreadQueue? queue)
        => _threadQueues.TryGetValue(threadId, out queue);

    public void ReleaseThreadQueue(ActorThreadId threadId)
    {
        // Keep the normal actor working set warm. Beyond the bound, newly idle high-cardinality mailboxes are
        // retired immediately so memory remains bounded without allocating a timer or an eviction task per actor.
        if (_threadQueues.Count <= _maxRetainedIdleQueues)
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
            queue.Stop();
        }
    }

    static InvalidOperationException CreateQueueConfigurationException(IActorThreadQueue queue)
        => new(
            $"{nameof(ActorThreadPoolV2)} requires an {nameof(ActorThreadQueueV2)} mailbox, "
            + $"but the container resolved {queue.GetType().FullName}.");
}
