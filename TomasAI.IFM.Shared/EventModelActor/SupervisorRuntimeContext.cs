using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum SupervisorActorHealthStatus
{
    Green = 0,
    Yellow = 1,
    Red = 2
}

public enum SupervisorActorLifecycleState
{
    Registered = 0,
    Starting = 1,
    Running = 2,
    Draining = 3,
    Stopped = 4,
    Restarting = 5,
    Faulted = 6,
    TimedOut = 7,
    Quarantined = 8
}

public sealed record SupervisorActorSnapshot(
    ActorMailboxId ActorId,
    string Domain,
    string Implementation,
    bool IsRunning,
    SupervisorActorLifecycleState LifecycleState,
    long Generation,
    SupervisorActorHealthStatus Status,
    int QueueDepth,
    long Accepted,
    long Dequeued,
    long Succeeded,
    long HandledFailures,
    long EscapedFailures,
    long Cancelled,
    long Rejected,
    IReadOnlyList<ActorMailboxMetricsSnapshot> Mailboxes);

public sealed record SupervisorRuntimeSnapshot(
    DateTime ObservedUtc,
    SupervisorActorHealthStatus OverallStatus,
    int ActorCount,
    int RunningActorCount,
    int ProcessingMailboxCount,
    int QueuedMessageCount,
    IReadOnlyList<SupervisorActorSnapshot> Actors,
    IReadOnlyList<SupervisorFailureRecord> Failures,
    IReadOnlyList<SupervisorProjectorSnapshot> Projectors,
    IReadOnlyList<SupervisorWorkerSnapshot>? Workers = null);

public sealed record SupervisorWorkerSnapshot(
    int WorkerId,
    ActorThreadState State,
    bool IsStarted,
    bool IsRunning,
    bool IsFaulted,
    ActorThreadId? CurrentMailbox,
    string ExceptionType,
    string FailureReason);

public interface ISupervisorWorkerMetricsSource
{
    int SupervisorWorkerId { get; }
    SupervisorWorkerSnapshot CaptureSupervisorSnapshot();
}

public sealed record SupervisorProjectorSnapshot(
    string ActorName,
    string ProjectorName,
    string DurableProcessQueue,
    string DurableReplayQueue,
    bool IsReady,
    long RecoveryEventsDiscovered,
    long RecoveryEventsQueued,
    DateTime UpdatedUtc,
    string FailureReason);

public interface ISupervisorProjectorMetricsSource
{
    string SupervisorProjectorKey { get; }
    SupervisorProjectorSnapshot CaptureSupervisorSnapshot();
}

public interface IActorFailureSink
{
    Guid RecordFailure(
        ActorMailboxId actorId,
        ActorThreadId threadId,
        string verb,
        ActorFailureStage stage,
        Exception exception,
        Guid? primaryFailureId = null,
        ActorMessageOutcomeType outcome = ActorMessageOutcomeType.EscapedFailure,
        ActorDeliveryOutcomeType deliveryOutcome = ActorDeliveryOutcomeType.NotRequired);
}

public sealed record SupervisorFailureRecord(
    Guid FailureId,
    DateTime FailedUtc,
    ActorMailboxId ActorId,
    ActorThreadId ThreadId,
    string Verb,
    ActorFailureStage Stage,
    string ExceptionType,
    string Error,
    Guid? PrimaryFailureId,
    ActorFailureSeverity Severity = ActorFailureSeverity.Error,
    ActorMessageOutcomeType Outcome = ActorMessageOutcomeType.EscapedFailure,
    ActorDeliveryOutcomeType DeliveryOutcome = ActorDeliveryOutcomeType.NotRequired,
    int HResult = 0,
    string ExceptionDetail = "",
    string TraceId = "",
    string SpanId = "");

/// <summary>
/// Singleton root for actor-wide operational data. Snapshot collection reads actor-owned counters directly and never
/// sends a message through an actor mailbox.
/// </summary>
public sealed class SupervisorRuntimeContext : IActorFailureSink
{
    internal const string FailureIdExceptionDataKey = "IFM.SupervisorFailureId";
    const int MaximumFailureHistory = 512;
    readonly ConcurrentDictionary<ActorMailboxId, ActorEntry> _actors = new();
    readonly ConcurrentQueue<SupervisorFailureRecord> _failures = new();
    readonly ConcurrentDictionary<string, ISupervisorProjectorMetricsSource> _projectors = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<int, ISupervisorWorkerMetricsSource> _workers = new();
    readonly ConcurrentDictionary<ActorThreadId, MailboxOperationGate> _mailboxOperationGates = new();
    int _failureCount;

    internal void Register(IActor actor) => _actors[actor.Id] = new(actor);

    internal void Remove(IActor actor)
    {
        if (_actors.TryGetValue(actor.Id, out var entry) && ReferenceEquals(entry.Actor, actor))
            ((ICollection<KeyValuePair<ActorMailboxId, ActorEntry>>)_actors)
                .Remove(new(actor.Id, entry));
    }

    public void RegisterProjector(ISupervisorProjectorMetricsSource projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        _projectors[projector.SupervisorProjectorKey] = projector;
    }

    internal void RegisterWorker(ISupervisorWorkerMetricsSource worker)
        => _workers[worker.SupervisorWorkerId] = worker;

    internal void RemoveWorker(ISupervisorWorkerMetricsSource worker)
        => ((ICollection<KeyValuePair<int, ISupervisorWorkerMetricsSource>>)_workers)
            .Remove(new(worker.SupervisorWorkerId, worker));

    internal async ValueTask<T> RunMailboxOperationAsync<T>(
        ActorThreadId threadId,
        Func<ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        MailboxOperationGate gate;
        while (true)
        {
            gate = _mailboxOperationGates.GetOrAdd(threadId, static _ => new());
            Interlocked.Increment(ref gate.Users);
            if (_mailboxOperationGates.TryGetValue(threadId, out var published)
                && ReferenceEquals(gate, published))
                break;
            Interlocked.Decrement(ref gate.Users);
        }
        var acquired = false;
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
                gate.Semaphore.Release();
            if (Interlocked.Decrement(ref gate.Users) == 0)
                ((ICollection<KeyValuePair<ActorThreadId, MailboxOperationGate>>)_mailboxOperationGates)
                    .Remove(new(threadId, gate));
        }
    }

    internal async ValueTask StartAsync(
        IActor actor,
        Func<ValueTask> start,
        CancellationToken cancellationToken)
    {
        var entry = _actors.GetOrAdd(actor.Id, _ => new(actor));
        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (actor.IsRunning)
            {
                entry.State = SupervisorActorLifecycleState.Running;
                return;
            }
            entry.State = SupervisorActorLifecycleState.Starting;
            try
            {
                await start().ConfigureAwait(false);
                Interlocked.Increment(ref entry.Generation);
                entry.State = SupervisorActorLifecycleState.Running;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                entry.State = SupervisorActorLifecycleState.Registered;
                throw;
            }
            catch (Exception exception)
            {
                entry.State = exception is TimeoutException
                    ? SupervisorActorLifecycleState.TimedOut
                    : SupervisorActorLifecycleState.Faulted;
                if (!TryGetRecordedFailureId(exception, out _))
                    RecordFailure(actor.Id, new(actor.Id.ActorType, actor.Id.Name, "lifecycle"),
                        "Start", ActorFailureStage.Startup, exception);
                throw;
            }
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    internal async ValueTask StopAsync(
        IActor actor,
        Func<ValueTask> stop,
        CancellationToken cancellationToken)
    {
        var entry = _actors.GetOrAdd(actor.Id, _ => new(actor));
        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!actor.IsRunning)
            {
                entry.State = SupervisorActorLifecycleState.Stopped;
                return;
            }
            entry.State = SupervisorActorLifecycleState.Draining;
            try
            {
                await stop().ConfigureAwait(false);
                entry.State = SupervisorActorLifecycleState.Stopped;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                entry.State = SupervisorActorLifecycleState.Quarantined;
                throw;
            }
            catch (Exception exception)
            {
                entry.State = exception is TimeoutException
                    ? SupervisorActorLifecycleState.TimedOut
                    : SupervisorActorLifecycleState.Faulted;
                if (!TryGetRecordedFailureId(exception, out _))
                    RecordFailure(actor.Id, new(actor.Id.ActorType, actor.Id.Name, "lifecycle"),
                        "Stop", exception is TimeoutException ? ActorFailureStage.Drain : ActorFailureStage.Shutdown, exception);
                throw;
            }
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    internal async ValueTask RestartAsync(
        IActor actor,
        Func<ValueTask> stop,
        Func<ValueTask> start,
        CancellationToken cancellationToken)
    {
        var entry = _actors.GetOrAdd(actor.Id, _ => new(actor));
        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            entry.State = SupervisorActorLifecycleState.Restarting;
            try
            {
                if (actor.IsRunning)
                    await stop().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await start().ConfigureAwait(false);
                Interlocked.Increment(ref entry.Generation);
                entry.State = SupervisorActorLifecycleState.Running;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                entry.State = SupervisorActorLifecycleState.Quarantined;
                throw;
            }
            catch (Exception exception)
            {
                entry.State = exception is TimeoutException
                    ? SupervisorActorLifecycleState.Quarantined
                    : SupervisorActorLifecycleState.Faulted;
                if (!TryGetRecordedFailureId(exception, out _))
                    RecordFailure(actor.Id, new(actor.Id.ActorType, actor.Id.Name, "lifecycle"),
                        "Restart", exception is TimeoutException ? ActorFailureStage.Drain : ActorFailureStage.Restart, exception);
                throw;
            }
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public SupervisorRuntimeSnapshot CaptureSnapshot(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var observedUtc = DateTime.UtcNow;
        var actors = _actors.Values
            .Select(entry => Capture(entry))
            .OrderBy(actor => actor.Domain, StringComparer.Ordinal)
            .ThenBy(actor => actor.ActorId.ActorType)
            .ThenBy(actor => actor.ActorId.Name, StringComparer.Ordinal)
            .ToArray();
        var running = actors.Count(actor => actor.IsRunning);
        var processing = actors.Sum(actor => actor.Mailboxes.Count(mailbox => mailbox.IsProcessing));
        var queued = actors.Sum(actor => actor.QueueDepth);
        var workers = _workers.Values
            .Select(worker => worker.CaptureSupervisorSnapshot())
            .OrderBy(worker => worker.WorkerId)
            .ToArray();
        var overall = actors.Any(actor => actor.Status == SupervisorActorHealthStatus.Red)
            || workers.Any(worker => worker.IsFaulted)
            ? SupervisorActorHealthStatus.Red
            : actors.Any(actor => actor.Status == SupervisorActorHealthStatus.Yellow)
                ? SupervisorActorHealthStatus.Yellow
                : SupervisorActorHealthStatus.Green;
        var from = (fromUtc ?? observedUtc.AddHours(-1)).ToUniversalTime();
        var to = (toUtc ?? observedUtc).ToUniversalTime();
        var failures = _failures
            .Where(failure => failure.FailedUtc >= from && failure.FailedUtc <= to)
            .OrderByDescending(failure => failure.FailedUtc)
            .ToArray();
        var projectors = _projectors.Values
            .Select(projector => projector.CaptureSupervisorSnapshot())
            .OrderBy(projector => projector.ActorName, StringComparer.Ordinal)
            .ThenBy(projector => projector.ProjectorName, StringComparer.Ordinal)
            .ToArray();
        return new(
            observedUtc,
            overall,
            actors.Length,
            running,
            processing,
            queued,
            actors,
            failures,
            projectors,
            workers);
    }

    public Guid RecordFailure(
        ActorMailboxId actorId,
        ActorThreadId threadId,
        string verb,
        ActorFailureStage stage,
        Exception exception,
        Guid? primaryFailureId = null,
        ActorMessageOutcomeType outcome = ActorMessageOutcomeType.EscapedFailure,
        ActorDeliveryOutcomeType deliveryOutcome = ActorDeliveryOutcomeType.NotRequired)
    {
        try
        {
            var failureId = Guid.NewGuid();
            var recordedOutcome = primaryFailureId.HasValue && outcome == ActorMessageOutcomeType.EscapedFailure
                ? ActorMessageOutcomeType.HandledFailure
                : outcome;
            var activity = System.Diagnostics.Activity.Current;
            _failures.Enqueue(new(
                failureId,
                DateTime.UtcNow,
                actorId,
                threadId,
                Bound(verb, 128),
                stage,
                exception.GetType().FullName ?? exception.GetType().Name,
                Bound(exception.Message, 2048),
                primaryFailureId,
                ActorFailureSeverity.Error,
                recordedOutcome,
                deliveryOutcome,
                exception.HResult,
                Bound(exception.ToString(), 8_192),
                activity?.TraceId.ToString() ?? string.Empty,
                activity?.SpanId.ToString() ?? string.Empty));
            if (Interlocked.Increment(ref _failureCount) > MaximumFailureHistory)
            {
                while (Volatile.Read(ref _failureCount) > MaximumFailureHistory
                       && _failures.TryDequeue(out _))
                    Interlocked.Decrement(ref _failureCount);
            }
            return failureId;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    internal static void MarkRecorded(Exception exception, Guid failureId)
    {
        try
        {
            if (failureId != Guid.Empty)
                exception.Data[FailureIdExceptionDataKey] = failureId;
        }
        catch
        {
        }
    }

    internal static bool TryGetRecordedFailureId(Exception exception, out Guid failureId)
    {
        try
        {
            if (exception.Data[FailureIdExceptionDataKey] is Guid value && value != Guid.Empty)
            {
                failureId = value;
                return true;
            }
        }
        catch
        {
        }
        failureId = Guid.Empty;
        return false;
    }

    static SupervisorActorSnapshot Capture(ActorEntry entry)
    {
        var actor = entry.Actor;
        var metrics = actor.Mailbox?.Metrics.CaptureSnapshot();
        var mailboxes = metrics?.Mailboxes ?? [];
        var queueDepth = mailboxes.Sum(mailbox => mailbox.QueueDepth);
        var processing = mailboxes.Any(mailbox => mailbox.IsProcessing);
        var status = !actor.IsRunning
            ? SupervisorActorHealthStatus.Red
            : processing || queueDepth > 0
                ? SupervisorActorHealthStatus.Yellow
                : SupervisorActorHealthStatus.Green;
        var type = actor.GetType();
        return new(
            actor.Id,
            type.Namespace ?? "Unclassified",
            type.FullName ?? type.Name,
            actor.IsRunning,
            actor.IsRunning ? SupervisorActorLifecycleState.Running : entry.State,
            Interlocked.Read(ref entry.Generation),
            status,
            queueDepth,
            mailboxes.Sum(mailbox => mailbox.Accepted),
            mailboxes.Sum(mailbox => mailbox.Dequeued),
            mailboxes.Sum(mailbox => mailbox.Succeeded),
            mailboxes.Sum(mailbox => mailbox.HandledFailures),
            mailboxes.Sum(mailbox => mailbox.EscapedFailures),
            mailboxes.Sum(mailbox => mailbox.Cancelled),
            mailboxes.Sum(mailbox => mailbox.Rejected),
            mailboxes);
    }

    sealed class ActorEntry(IActor actor)
    {
        internal readonly IActor Actor = actor;
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal volatile SupervisorActorLifecycleState State = SupervisorActorLifecycleState.Registered;
        internal long Generation;
    }

    sealed class MailboxOperationGate
    {
        internal readonly SemaphoreSlim Semaphore = new(1, 1);
        internal int Users;
    }

    static string Bound(string? value, int maximum)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maximum ? value : value[..maximum];
    }
}
