using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>
/// Singleton root for actor-wide operational data. Snapshot collection reads actor-owned counters directly and never
/// sends a message through an actor mailbox.
/// </summary>
public sealed class SupervisorRuntimeContext : ISupervisorRuntimeContext
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

    public void RegisterWorker(ISupervisorWorkerMetricsSource worker)
        => _workers[worker.SupervisorWorkerId] = worker;

    public void RemoveWorker(ISupervisorWorkerMetricsSource worker)
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
            if (entry.Actor.IsRunning)
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
            if (!entry.Actor.IsRunning)
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

    /// <summary>Serializes replacement with lifecycle operations without awaiting the retired handler.</summary>
    internal async ValueTask ReplaceRealtimeAsync(ActorMailboxId id, Func<IActor, ValueTask<IActor>> replace,
        CancellationToken cancellationToken)
    {
        if (!_actors.TryGetValue(id, out var entry)) throw new InvalidOperationException($"Actor '{id}' is unavailable.");
        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            entry.State = SupervisorActorLifecycleState.Restarting;
            var previous = entry.Actor;
            entry.Actor = await replace(previous).ConfigureAwait(false);
            if (!ReferenceEquals(previous, entry.Actor)) Interlocked.Increment(ref entry.Generation);
            entry.State = SupervisorActorLifecycleState.Running;
        }
        catch (Exception error)
        {
            entry.State = SupervisorActorLifecycleState.Quarantined;
            RecordFailure(id, new(id.ActorType, id.Name, "lifecycle"), "ReplaceRealtime",
                ActorFailureStage.Restart, error);
            throw;
        }
        finally { entry.Gate.Release(); }
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
        var overall = SupervisorActorHealthStatus.Green;
        if (actors.Any(actor => actor.Status == SupervisorActorHealthStatus.Red) || workers.Any(worker => worker.IsFaulted))
            overall = SupervisorActorHealthStatus.Red;
        else if (actors.Any(actor => actor.Status == SupervisorActorHealthStatus.Yellow))
            overall = SupervisorActorHealthStatus.Yellow;
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

    /// <inheritdoc />
    public IReadOnlyList<IActorRuntimeMetricsSource> CaptureActorMetricsSources() =>
        _actors.Values
            .Select(static entry => (IActorRuntimeMetricsSource)new ActorRuntimeMetricsSource(entry))
            .OrderBy(static source => source.ActorId.ActorType)
            .ThenBy(static source => source.ActorId.Name, StringComparer.Ordinal)
            .ToArray();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Diagnostic failures must not interrupt actor processing or replace the original failure.")]
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
        ArgumentNullException.ThrowIfNull(exception);
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Diagnostic failures must not interrupt actor processing or replace the original failure.")]
    internal static void MarkRecorded(Exception exception, Guid failureId)
    {
        try
        {
            if (failureId != Guid.Empty)
                exception.Data[FailureIdExceptionDataKey] = failureId;
        }
        catch
        { /* Best-effort lifecycle cleanup must allow the remaining shutdown steps to execute. */ 
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Diagnostic failures must not interrupt actor processing or replace the original failure.")]
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
        { /* Best-effort lifecycle cleanup must allow the remaining shutdown steps to execute. */ 
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
        var status = SupervisorActorHealthStatus.Green;
        if (!actor.IsRunning) status = SupervisorActorHealthStatus.Red;
        else if (processing || queueDepth > 0 || actor.Mailbox?.ThreadQueues?.IsAccepting == false
            || mailboxes.Any(mailbox => !mailbox.IsAdmissionOpen)) status = SupervisorActorHealthStatus.Yellow;
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Only managed SemaphoreSlim Wait/WaitAsync operations are used, never AvailableWaitHandle. Its lifetime follows outstanding actor operations; disposing during immediate retirement would race active waiters.")]
    sealed class ActorEntry(IActor actor)
    {
        internal IActor Actor = actor;
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal volatile SupervisorActorLifecycleState State = SupervisorActorLifecycleState.Registered;
        internal long Generation;
    }

    sealed class ActorRuntimeMetricsSource(ActorEntry entry) : IActorRuntimeMetricsSource
    {
        public ActorMailboxId ActorId => entry.Actor.Id;

        public SupervisorActorSnapshot CaptureSnapshot() => Capture(entry);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Only managed SemaphoreSlim Wait/WaitAsync operations are used, never AvailableWaitHandle. Its lifetime follows outstanding actor operations; disposing during immediate retirement would race active waiters.")]
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
