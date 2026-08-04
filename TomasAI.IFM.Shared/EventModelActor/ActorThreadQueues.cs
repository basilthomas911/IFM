using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Owns the entity mailboxes for one actor and publishes each non-empty mailbox to the shared actor scheduler once.
/// </summary>
public sealed class ActorThreadQueues(IActorSupervisor supervisor) : IActorThreadQueues
{
    const int MaxRetainedIdleQueues = 1024;
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ConcurrentDictionary<ActorThreadId, IActorThreadQueue> _threadQueues = new();

    public int Count => _threadQueues.Count;

    public bool Write(IActorMessage message)
        => Write(message, message.Subject);

    public bool Write(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default)
    {
        IsArgumentNull.Check(message);
        var threadId = subject.ThreadId;
        var thread = _supervisor.GetThread(threadId);

        while (true)
        {
            var queue = GetThreadQueue(threadId);
            if (queue is not IScheduledActorThreadQueue scheduled)
                throw CreateQueueConfigurationException(queue);

            if (!scheduled.TryWrite(message, cancellationToken))
            {
                RemoveRetired(threadId, queue);
                continue;
            }

            if (scheduled.TrySchedule())
                thread.SignalMessageAvailable(threadId);
            return true;
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
        IsArgumentNull.Check(message);
        var threadId = subject.ThreadId;
        var getThread = _supervisor.GetThreadAsync(threadId, cancellationToken);
        if (!getThread.IsCompletedSuccessfully)
            return AwaitThreadAndWrite(getThread, message, threadId, cancellationToken);

        return WriteToQueueAsync(getThread.Result, message, threadId, cancellationToken);
    }

    ValueTask<bool> WriteToQueueAsync(
        IActorThread thread,
        IActorMessage message,
        ActorThreadId threadId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var queue = GetThreadQueue(threadId);
            if (queue is not IScheduledActorThreadQueue scheduled)
                return ValueTask.FromException<bool>(CreateQueueConfigurationException(queue));

            var enqueue = scheduled.TryWriteAsync(message, cancellationToken);
            if (!enqueue.IsCompletedSuccessfully)
                return AwaitScheduledWrite(enqueue, scheduled, thread, message, threadId, queue, cancellationToken);

            if (!enqueue.Result)
            {
                RemoveRetired(threadId, queue);
                continue;
            }

            if (scheduled.TrySchedule())
                thread.SignalMessageAvailable(threadId);
            return ValueTask.FromResult(true);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    async ValueTask<bool> AwaitThreadAndWrite(
        ValueTask<IActorThread> pendingThread,
        IActorMessage message,
        ActorThreadId threadId,
        CancellationToken cancellationToken)
    {
        var thread = await pendingThread.ConfigureAwait(false);
        return await WriteToQueueAsync(thread, message, threadId, cancellationToken).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    async ValueTask<bool> AwaitScheduledWrite(
        ValueTask<bool> pending,
        IScheduledActorThreadQueue scheduled,
        IActorThread thread,
        IActorMessage message,
        ActorThreadId threadId,
        IActorThreadQueue queue,
        CancellationToken cancellationToken)
    {
        if (await pending.ConfigureAwait(false))
        {
            if (scheduled.TrySchedule())
                thread.SignalMessageAvailable(threadId);
            return true;
        }

        RemoveRetired(threadId, queue);
        return await WriteToQueueAsync(thread, message, threadId, cancellationToken).ConfigureAwait(false);
    }

    public IActorThreadQueue GetThreadQueue(ActorThreadId threadId)
    {
        while (true)
        {
            var queue = _threadQueues.GetOrAdd(threadId, static (id, state) =>
            {
                var created = state.Container.Resolve<IActorThreadQueue>();
                created.SetId(id);
                created.Start();
                return created;
            }, _supervisor);

            if (queue is not IScheduledActorThreadQueue scheduled || !scheduled.IsRetired)
                return queue;

            RemoveRetired(threadId, queue);
        }
    }

    public bool TryGetThreadQueue(ActorThreadId threadId, out IActorThreadQueue? queue)
        => _threadQueues.TryGetValue(threadId, out queue);

    public void ReleaseThreadQueue(ActorThreadId threadId)
    {
        // Keep the normal actor working set warm. Beyond the bound, newly idle high-cardinality mailboxes are
        // retired immediately so memory remains bounded without allocating a timer or an eviction task per actor.
        if (_threadQueues.Count <= MaxRetainedIdleQueues)
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

        ((ICollection<KeyValuePair<ActorThreadId, IActorThreadQueue>>)_threadQueues)
            .Remove(new KeyValuePair<ActorThreadId, IActorThreadQueue>(threadId, queue));
    }

    static InvalidOperationException CreateQueueConfigurationException(IActorThreadQueue queue)
        => new(
            $"{nameof(ActorThreadPoolV2)} requires an {nameof(ActorThreadQueueV2)} mailbox, "
            + $"but the container resolved {queue.GetType().FullName}.");
}
