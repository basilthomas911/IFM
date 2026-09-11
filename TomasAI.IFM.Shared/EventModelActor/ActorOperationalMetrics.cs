using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum ActorMessageOutcomeType
{
    Unknown = 0,
    Succeeded = 1,
    HandledFailure = 2,
    EscapedFailure = 3,
    CancelledBeforeCommit = 4,
    CompletedAfterCancellation = 5
}

public enum ActorFailureStage
{
    Unknown = 0,
    Admission = 1,
    Parsing = 2,
    Validation = 3,
    Deduplication = 4,
    StateReplay = 5,
    Execution = 6,
    Persistence = 7,
    Projection = 8,
    Publication = 9,
    ExceptionHandling = 10,
    Cleanup = 11,
    Reply = 12,
    MailboxInfrastructure = 13,
    WorkerLoop = 14,
    Startup = 15,
    Shutdown = 16,
    Drain = 17,
    Restart = 18
}

public enum ActorDeliveryOutcomeType
{
    NotRequired = 0,
    Succeeded = 1,
    Failed = 2,
    Deferred = 3,
    Redelivered = 4,
    Terminal = 5
}

public enum ActorFailureSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

public enum ActorMailboxLifecycleState
{
    Running = 0,
    Draining = 1,
    Paused = 2,
    Quarantined = 3,
    Retired = 4
}

public sealed record ActorMailboxMetricsSnapshot(
    ActorThreadId ThreadId,
    int QueueDepth,
    long Accepted,
    long Dequeued,
    long Succeeded,
    long HandledFailures,
    long EscapedFailures,
    long Cancelled,
    long Rejected,
    bool IsAdmissionOpen,
    ActorMailboxLifecycleState LifecycleState,
    long Generation,
    bool IsProcessing,
    string CurrentVerb,
    DateTime? LastAcceptedUtc,
    DateTime? LastStartedUtc,
    DateTime? LastCompletedUtc,
    DateTime? LastFailedUtc,
    string LastExceptionType,
    string LastError,
    long Revision);

public sealed record ActorMetricsSnapshot(
    ActorMailboxId ActorId,
    DateTime ObservedUtc,
    IReadOnlyList<ActorMailboxMetricsSnapshot> Mailboxes,
    long Revision);

/// <summary>
/// Actor-owned operational measurements. Reads are observational and never execute on the actor mailbox.
/// </summary>
public interface IActorMetricsStore
{
    ActorMailboxId ActorId { get; }
    ActorMetricsSnapshot CaptureSnapshot();
    bool TryGetMailboxSnapshot(ActorThreadId threadId, out ActorMailboxMetricsSnapshot? snapshot);
}

/// <summary>
/// Mutable actor-owned metrics with lock-free steady-state updates and lock-free published-entry reads.
/// Structural publication occurs only when an entity mailbox is created or retired.
/// </summary>
public sealed class ActorMetricsStore(ActorMailboxId actorId) : IActorMetricsStore
{
    readonly ConcurrentDictionary<ActorThreadId, ActorMailboxMetrics> _mailboxes = new();
    readonly object _publicationGate = new();
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

sealed class ActorMailboxMetrics(ActorThreadId threadId, IActorThreadQueue queue, long generation)
{
    readonly ActorThreadId _threadId = threadId;
    IActorThreadQueue _queue = queue;
    long _accepted;
    long _dequeued;
    long _succeeded;
    long _handledFailures;
    long _escapedFailures;
    long _cancelled;
    long _rejected;
    long _lastAcceptedTicks;
    long _lastStartedTicks;
    long _lastCompletedTicks;
    long _lastFailedTicks;
    long _revision;
    long _generation = generation;
    int _admissionOpen = 1;
    int _lifecycleState = (int)ActorMailboxLifecycleState.Running;
    int _processing;
    string _currentVerb = string.Empty;
    string _lastExceptionType = string.Empty;
    string _lastError = string.Empty;

    internal void SetQueue(IActorThreadQueue queue, long generation)
    {
        Volatile.Write(ref _queue, queue);
        Interlocked.Exchange(ref _generation, generation);
    }
    internal bool IsFor(IActorThreadQueue queue) => ReferenceEquals(Volatile.Read(ref _queue), queue);

    internal void RecordAccepted()
    {
        Interlocked.Increment(ref _accepted);
        Interlocked.Exchange(ref _lastAcceptedTicks, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _revision);
    }

    internal void RecordDequeued(string verb)
    {
        Interlocked.Increment(ref _dequeued);
        Volatile.Write(ref _currentVerb, verb);
        Interlocked.Exchange(ref _lastStartedTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref _processing, 1);
        Interlocked.Increment(ref _revision);
    }

    internal bool RecordSucceeded()
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _succeeded);
        FinishCompletion();
        return true;
    }

    internal bool RecordHandledFailure(Exception exception)
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _handledFailures);
        RecordFailure(exception);
        FinishCompletion();
        return true;
    }

    internal bool RecordEscapedFailure(Exception exception)
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _escapedFailures);
        RecordFailure(exception);
        FinishCompletion();
        return true;
    }

    internal bool RecordCancelled()
    {
        if (!TryComplete())
            return false;
        Interlocked.Increment(ref _cancelled);
        FinishCompletion();
        return true;
    }

    internal void RecordRejected()
    {
        Interlocked.Increment(ref _rejected);
        Interlocked.Increment(ref _revision);
    }

    internal void SetAdmission(bool isOpen)
    {
        Volatile.Write(ref _admissionOpen, isOpen ? 1 : 0);
        Interlocked.Increment(ref _revision);
    }

    internal void SetLifecycle(ActorMailboxLifecycleState state)
    {
        Volatile.Write(ref _lifecycleState, (int)state);
        Interlocked.Increment(ref _revision);
    }

    internal ActorMailboxMetricsSnapshot CaptureSnapshot()
    {
        var queue = Volatile.Read(ref _queue);
        return new ActorMailboxMetricsSnapshot(
            _threadId,
            queue.Count,
            Interlocked.Read(ref _accepted),
            Interlocked.Read(ref _dequeued),
            Interlocked.Read(ref _succeeded),
            Interlocked.Read(ref _handledFailures),
            Interlocked.Read(ref _escapedFailures),
            Interlocked.Read(ref _cancelled),
            Interlocked.Read(ref _rejected),
            Volatile.Read(ref _admissionOpen) != 0,
            (ActorMailboxLifecycleState)Volatile.Read(ref _lifecycleState),
            Interlocked.Read(ref _generation),
            Volatile.Read(ref _processing) != 0,
            Volatile.Read(ref _currentVerb),
            ToUtc(Interlocked.Read(ref _lastAcceptedTicks)),
            ToUtc(Interlocked.Read(ref _lastStartedTicks)),
            ToUtc(Interlocked.Read(ref _lastCompletedTicks)),
            ToUtc(Interlocked.Read(ref _lastFailedTicks)),
            Volatile.Read(ref _lastExceptionType),
            Volatile.Read(ref _lastError),
            Interlocked.Read(ref _revision));
    }

    void RecordFailure(Exception exception)
    {
        Volatile.Write(ref _lastExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        Volatile.Write(ref _lastError, Bound(exception.Message, 2048));
        Interlocked.Exchange(ref _lastFailedTicks, DateTime.UtcNow.Ticks);
    }

    bool TryComplete() => Interlocked.CompareExchange(ref _processing, 0, 1) == 1;

    void FinishCompletion()
    {
        Volatile.Write(ref _currentVerb, string.Empty);
        Interlocked.Exchange(ref _lastCompletedTicks, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _revision);
    }

    static DateTime? ToUtc(long ticks) => ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    static string Bound(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
}

static class ActorOperationalMetrics
{
    internal static ActorFailureStage MapStage(string stage) => stage switch
    {
        ActorRuntimeMetrics.ParsingStage => ActorFailureStage.Parsing,
        ActorRuntimeMetrics.ValidationStage => ActorFailureStage.Validation,
        ActorRuntimeMetrics.DeduplicationStage => ActorFailureStage.Deduplication,
        ActorRuntimeMetrics.ReplayStage => ActorFailureStage.StateReplay,
        ActorRuntimeMetrics.ExecutionStage => ActorFailureStage.Execution,
        ActorRuntimeMetrics.PersistenceStage => ActorFailureStage.Persistence,
        ActorRuntimeMetrics.PublicationStage => ActorFailureStage.Publication,
        ActorRuntimeMetrics.ReplyStage => ActorFailureStage.Reply,
        _ => ActorFailureStage.Unknown
    };

    internal static void RecordHandledFailure(
        IActorMailbox? mailbox,
        ActorThreadId threadId,
        Exception exception)
    {
        if (mailbox?.Metrics is not ActorMetricsStore store
            || !mailbox.ThreadQueues.TryGetThreadQueue(threadId, out var queue)
            || queue is null)
            return;

        store.GetOrRegister(threadId, queue).RecordHandledFailure(exception);
    }

    internal static Guid RecordHandledFailure(
        IActorMailbox? mailbox,
        SupervisorRuntimeContext? runtime,
        ActorThreadId threadId,
        string verb,
        ActorFailureStage stage,
        Exception exception,
        Guid? primaryFailureId = null)
    {
        RecordHandledFailure(mailbox, threadId, exception);
        return runtime?.RecordFailure(
            threadId.MailboxId, threadId, verb, stage, exception, primaryFailureId,
            ActorMessageOutcomeType.HandledFailure) ?? Guid.Empty;
    }
}
