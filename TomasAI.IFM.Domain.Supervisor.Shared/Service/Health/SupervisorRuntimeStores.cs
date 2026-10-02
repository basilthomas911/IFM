using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Reports authority from the latest immutable metrics snapshot.</summary>
public sealed class SupervisorHealthManager(ISupervisorActorMetricsState state) : ISupervisorHealthManager
{
    public SupervisorAuthorityState AuthorityState =>
        state.Current.CriticalActors > 0 || state.Current.FailedActors > 0
            ? SupervisorAuthorityState.AvailableWithDegradedCapability
            : SupervisorAuthorityState.Available;
}

/// <summary>Provides the current bounded incident count directly from the latest snapshot.</summary>
public sealed class SupervisorIncidentStore : ISupervisorIncidentStore
{
    readonly ConcurrentDictionary<ActorThreadId, SupervisorActorIncident> _active = [];
    readonly ConcurrentDictionary<ActorThreadId, ConcurrentQueue<DateTime>> _restarts = [];
    readonly ConcurrentDictionary<ActorThreadId, byte> _manualReview = [];
    public int ActiveIncidentCount => _active.Count;
    public IReadOnlyList<SupervisorActorIncident> ActiveIncidents => [.. _active.Values.OrderBy(value => value.ThreadId.ToString())];

    public bool Acknowledge(ActorThreadId threadId, string requester, string reason)
    {
        if (string.IsNullOrWhiteSpace(requester) || string.IsNullOrWhiteSpace(reason)) return false;
        _manualReview.TryRemove(threadId, out _);
        return _active.TryRemove(threadId, out _);
    }

    internal void Observe(SupervisorActorMetricsSnapshot snapshot)
    {
        var observed = new HashSet<ActorThreadId>();
        foreach (var thread in snapshot.Actors.SelectMany(actor => actor.Threads))
        {
            if (thread.Health == SupervisorActorHealth.Healthy) continue;
            observed.Add(thread.ThreadId);
            _active.AddOrUpdate(thread.ThreadId,
                _ => new(thread.ThreadId, thread.Health, snapshot.ObservedUtc, snapshot.ObservedUtc,
                    thread.Generation, thread.QueueDepth, thread.QueueCapacity),
                (_, current) => current with { Health = thread.Health, LastObservedUtc = snapshot.ObservedUtc,
                    Generation = thread.Generation, QueueDepth = thread.QueueDepth, QueueCapacity = thread.QueueCapacity });
        }
        foreach (var threadId in _active.Keys)
            if (!observed.Contains(threadId) && !_manualReview.ContainsKey(threadId)) _active.TryRemove(threadId, out _);
    }

    internal void RecordRestart(ActorThreadId threadId, long generation)
    {
        var now = DateTime.UtcNow;
        var history = _restarts.GetOrAdd(threadId, static _ => []);
        history.Enqueue(now);
        while (history.TryPeek(out var oldest) && now - oldest > TimeSpan.FromHours(24)) history.TryDequeue(out _);
        var last24Hours = history.Count;
        var last60Minutes = history.Count(value => now - value <= TimeSpan.FromHours(1));
        if (last60Minutes < 2 && last24Hours < 4) return;
        _manualReview[threadId] = 0;
        _active.AddOrUpdate(threadId,
            _ => new(threadId, SupervisorActorHealth.Critical, now, now, generation, 0, 0,
                last60Minutes, last24Hours, true),
            (_, current) => current with { Health = SupervisorActorHealth.Critical, LastObservedUtc = now,
                Generation = generation, RestartsIn60Minutes = last60Minutes,
                RestartsIn24Hours = last24Hours, RequiresTraderReview = true });
    }
}

/// <summary>Tracks active lifecycle actions without retaining unbounded history.</summary>
public sealed class SupervisorOperationStore : ISupervisorOperationStore
{
    const int RetentionLimit = 256;
    readonly ConcurrentQueue<SupervisorHealthOperation> _recent = [];
    readonly ConcurrentDictionary<Guid, byte> _recorded = [];
    int _active;
    long _revision;
    public int ActiveOperationCount => Math.Max(0, Volatile.Read(ref _active));
    public long LatestRevision => Interlocked.Read(ref _revision);
    public IReadOnlyList<SupervisorHealthOperation> RecentOperations => [.. _recent];
    internal SupervisorHealthOperation Started(ActorThreadId threadId, long generation)
    {
        Interlocked.Increment(ref _active); Interlocked.Increment(ref _revision);
        return new(Guid.NewGuid(), threadId, generation, DateTime.UtcNow, null, null,
            "Mailbox remained at its configured limit for fifteen minutes.");
    }
    internal void Completed(SupervisorHealthOperation operation, SupervisorOperationOutcome outcome, string reason)
    {
        _recent.Enqueue(operation with { CompletedUtc = DateTime.UtcNow, Outcome = outcome, Reason = reason });
        while (_recent.Count > RetentionLimit) _recent.TryDequeue(out _);
        Interlocked.Decrement(ref _active); Interlocked.Increment(ref _revision);
    }

    /// <summary>Records the projected terminal outcome once for each operation ID.</summary>
    public void Record(SupervisorActorOperationRequest request, SupervisorActorOperationResult result)
    {
        if (!_recorded.TryAdd(request.OperationId, 0)) return;
        var now = DateTime.UtcNow;
        _recent.Enqueue(new(request.OperationId, request.Target, request.ExpectedGeneration, now, now,
            result.Outcome, $"{request.Requester}: {request.Reason}; {result.Stage}: {result.FailureReason}"));
        while (_recent.Count > RetentionLimit && _recent.TryDequeue(out var removed))
            _recorded.TryRemove(removed.OperationId, out _);
        Interlocked.Increment(ref _revision);
    }
}

/// <summary>Exposes the latest bounded operational revision.</summary>
public sealed class SupervisorHistoryStore : ISupervisorHistoryStore, IDisposable
{
    const int RetentionLimit = 10_080;
    readonly SupervisorOperationStore _operations;
    readonly ISupervisorHistoryPersistence _persistence;
    readonly ILogger<SupervisorHistoryStore> _logger;
    readonly ConcurrentQueue<SupervisorHealthHistoryPoint> _snapshots = [];
    readonly Channel<SupervisorHealthHistoryPoint> _writes = Channel.CreateBounded<SupervisorHealthHistoryPoint>(
        new BoundedChannelOptions(256) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropOldest });
    readonly CancellationTokenSource _stopping = new();
    readonly Task _writer;
    long _latestSnapshotRevision;

    public SupervisorHistoryStore(
        SupervisorOperationStore operations,
        ISupervisorHistoryPersistence persistence,
        ILogger<SupervisorHistoryStore> logger)
    {
        _operations = operations;
        _persistence = persistence;
        _logger = logger;
        _writer = Task.Run(RunWriterAsync);
    }

    public long LatestRevision => Math.Max(_operations.LatestRevision, Interlocked.Read(ref _latestSnapshotRevision));

    public IReadOnlyList<SupervisorHealthHistoryPoint> Read(DateTime fromUtc, DateTime toUtc, int maximumCount = 1440)
    {
        if (fromUtc > toUtc || maximumCount <= 0) return [];
        var boundedCount = Math.Min(maximumCount, RetentionLimit);
        return [.. _snapshots
            .Where(value => value.ObservedUtc >= fromUtc.ToUniversalTime() && value.ObservedUtc <= toUtc.ToUniversalTime())
            .OrderByDescending(value => value.ObservedUtc)
            .Take(boundedCount)];
    }

    internal void Observe(SupervisorActorMetricsSnapshot value)
    {
        var point = new SupervisorHealthHistoryPoint(value.Revision, value.ObservedUtc, value.ExpectedActors, value.CollectedActors,
            value.FailedActors, value.HealthyActors, value.DegradedActors, value.CriticalActors, value.UnknownActors,
            value.EntityMailboxes, value.QueueDepth, value.Rejected, value.Failed, value.Quality,
            value.ProcessCpuPercent, value.ProcessWorkingSetBytes, value.ManagedHeapBytes, value.TotalAllocatedBytes,
            value.Gen0Collections, value.Gen1Collections, value.Gen2Collections, value.TotalGcPause,
            value.ThreadPoolPendingItems, value.ThreadPoolThreads);
        _snapshots.Enqueue(point);
        while (_snapshots.Count > RetentionLimit) _snapshots.TryDequeue(out _);
        Interlocked.Exchange(ref _latestSnapshotRevision, value.Revision);
        _writes.Writer.TryWrite(point);
    }

    async Task RunWriterAsync()
    {
        try
        {
            var loaded = await _persistence.ReadAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
                RetentionLimit, _stopping.Token).ConfigureAwait(false);
            foreach (var point in loaded.OrderBy(value => value.ObservedUtc))
            {
                _snapshots.Enqueue(point);
                Interlocked.Exchange(ref _latestSnapshotRevision,
                    Math.Max(point.Revision, Interlocked.Read(ref _latestSnapshotRevision)));
            }
            while (await _writes.Reader.WaitToReadAsync(_stopping.Token).ConfigureAwait(false))
                while (_writes.Reader.TryRead(out var point))
                    try { await _persistence.AppendAsync(point, _stopping.Token).ConfigureAwait(false); }
                    catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
                    {
                        _logger.LogError(exception, "Supervisor health history revision {Revision} could not be persisted.", point.Revision);
                    }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
        {
            _logger.LogError(exception, "Supervisor health history persistence worker failed.");
        }
    }

    public void Dispose()
    {
        _writes.Writer.TryComplete();
        try { if (!_writer.Wait(TimeSpan.FromSeconds(5))) _stopping.Cancel(); }
        catch (Exception) { _stopping.Cancel(); }
        _stopping.Dispose();
    }
}

/// <summary>Runs one generation-fenced thread restart at a time without blocking the polling thread.</summary>
public sealed class SupervisorHealthActionCoordinator(
    IActorSupervisor supervisor,
    IActorRuntimeMetricsSourceProvider sources,
    SupervisorOperationStore operations,
    SupervisorIncidentStore incidents,
    SupervisorHistoryStore history,
    SupervisorHealthActionOptions options,
    ILogger<SupervisorHealthActionCoordinator> logger) : ISupervisorHealthActionCoordinator
{
    readonly ConcurrentDictionary<ActorThreadId, byte> _active = [];

    public void Observe(SupervisorActorMetricsSnapshot snapshot)
    {
        history.Observe(snapshot);
        incidents.Observe(snapshot);
        if (!options.AutomaticMutationEnabled) return;
        foreach (var actor in snapshot.Actors)
            foreach (var thread in actor.Threads)
                if (thread.RestartRequired && _active.TryAdd(thread.ThreadId, 0))
                    _ = Task.Run(() => RestartAsync(thread.ThreadId, thread.Generation));
    }

    async Task RestartAsync(ActorThreadId threadId, long expectedGeneration)
    {
        var operation = operations.Started(threadId, expectedGeneration);
        var outcome = SupervisorOperationOutcome.Failed;
        var reason = "Automatic restart failed before completion.";
        try
        {
            var current = sources.CaptureActorMetricsSources()
                .FirstOrDefault(source => source.ActorId == threadId.MailboxId)?.CaptureSnapshot();
            var mailbox = current?.Mailboxes.FirstOrDefault(value => value.ThreadId == threadId);
            if (mailbox is null || mailbox.Generation != expectedGeneration)
            {
                outcome = SupervisorOperationOutcome.Rejected;
                reason = "The health observation was stale before restart execution.";
                return;
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var restarted = await supervisor.RestartAsync(threadId, expectedGeneration, TimeSpan.FromMinutes(1), timeout.Token).ConfigureAwait(false);
            outcome = restarted ? SupervisorOperationOutcome.Succeeded : SupervisorOperationOutcome.Rejected;
            reason = restarted ? "Actor thread restarted successfully." : "Actor thread generation changed or the mailbox could not drain safely.";
            if (restarted) incidents.RecordRestart(threadId, expectedGeneration + 1);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
        {
            outcome = exception is OperationCanceledException ? SupervisorOperationOutcome.TimedOut : SupervisorOperationOutcome.Failed;
            reason = exception.Message;
            SupervisorHealthActionLog.RestartFailed(logger, threadId, expectedGeneration, exception);
        }
        finally
        {
            operations.Completed(operation, outcome, reason);
            _active.TryRemove(threadId, out _);
        }
    }
}

/// <summary>Controls the explicit production gate for automatic actor lifecycle mutation.</summary>
public sealed record SupervisorHealthActionOptions
{
    /// <summary>Gets whether restart-required observations may trigger an automatic restart.</summary>
    public bool AutomaticMutationEnabled { get; init; }
}

static class SupervisorHealthActionLog
{
    static readonly Action<ILogger, ActorThreadId, long, Exception?> RestartFailure =
        LoggerMessage.Define<ActorThreadId, long>(LogLevel.Error, new(7301, nameof(RestartFailed)),
            "Automatic actor-thread restart failed for {ActorThreadId} generation {Generation}.");

    internal static void RestartFailed(ILogger logger, ActorThreadId threadId, long generation, Exception exception)
        => RestartFailure(logger, threadId, generation, exception);
}
