using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>
/// Mutable actor-owned metrics with lock-free steady-state updates and lock-free published-entry reads.
/// Structural publication occurs only when an entity mailbox is created or retired.
/// </summary>
public sealed class ActorMetricsStore(ActorMailboxId actorId) : IActorMetricsStore
{
    readonly ConcurrentDictionary<ActorThreadId, ActorMailboxMetrics> _mailboxes = new();
    readonly System.Threading.Lock _publicationGate = new();
    ActorMailboxMetrics[] _published = [];
    long _revision;

    public ActorMailboxId ActorId { get; } = actorId;

    internal ActorMailboxMetrics RegisterMailbox(
        ActorThreadId threadId,
        IActorThreadQueue queue,
        long generation = 1)
    {
        while (true)
        {
            if (_mailboxes.TryGetValue(threadId, out var existing))
            {
                existing.SetQueue(queue, generation);
                return existing;
            }

            var created = new ActorMailboxMetrics(threadId, queue, generation);
            if (_mailboxes.TryAdd(threadId, created))
            {
                PublishEntries();
                return created;
            }
        }
    }

    internal void RemoveMailbox(ActorThreadId threadId, IActorThreadQueue queue)
    {
        if (!_mailboxes.TryGetValue(threadId, out var metrics) || !metrics.IsFor(queue))
            return;
        if (((ICollection<KeyValuePair<ActorThreadId, ActorMailboxMetrics>>)_mailboxes)
            .Remove(new(threadId, metrics)))
        {
            PublishEntries();
            Interlocked.Increment(ref _revision);
        }
    }

    internal ActorMailboxMetrics GetOrRegister(ActorThreadId threadId, IActorThreadQueue queue)
        => _mailboxes.TryGetValue(threadId, out var existing)
            ? existing
            : RegisterMailbox(threadId, queue);

    public ActorMetricsSnapshot CaptureSnapshot()
    {
        var entries = Volatile.Read(ref _published);
        var snapshots = new ActorMailboxMetricsSnapshot[entries.Length];
        for (var index = 0; index < entries.Length; index++)
            snapshots[index] = entries[index].CaptureSnapshot();
        return new ActorMetricsSnapshot(ActorId, DateTime.UtcNow, snapshots, Interlocked.Read(ref _revision));
    }

    public bool TryGetMailboxSnapshot(ActorThreadId threadId, out ActorMailboxMetricsSnapshot? snapshot)
    {
        if (_mailboxes.TryGetValue(threadId, out var metrics))
        {
            snapshot = metrics.CaptureSnapshot();
            return true;
        }
        snapshot = null;
        return false;
    }

    void PublishEntries()
    {
        lock (_publicationGate)
        {
            Volatile.Write(ref _published, [.. _mailboxes.Values]);
            Interlocked.Increment(ref _revision);
        }
    }
}
