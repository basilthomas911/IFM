using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.Health.Collection;

/// <summary>Collects all managed actor metrics on one dedicated, named background thread.</summary>
public sealed class SupervisorActorMetricsPollingService : ISupervisorActorMetricsPollingService, IDisposable
{
    readonly object _processSampleGate = new();
    TimeSpan _lastProcessorTime;
    long _lastProcessTimestamp;
    public const string PollingThreadName = "IFM.Supervisor.ActorMetricsPoller";
    static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(60);

    readonly ISupervisorManagedActorMetricsSource _actors;
    readonly ISupervisorActorMetricsState _state;
    readonly ISupervisorExceptionLog _exceptionLog;
    readonly ISupervisorHealthLlmAdvisorySink _advisory;
    readonly ISupervisorHealthActionCoordinator _actions;
    readonly ILogger<SupervisorActorMetricsPollingService> _logger;
    readonly TimeProvider _timeProvider;
    readonly TimeSpan _interval;
    readonly ManualResetEventSlim _stop = new(false);
    readonly object _gate = new();
    Thread? _thread;
    int _serviceState;
    long _revision;
    long _completedCycles;
    long _failedCycles;
    long _overruns;
    long _lastHeartbeatUtcTicks;

    public SupervisorActorMetricsPollingService(
        ISupervisorManagedActorMetricsSource actors,
        ISupervisorActorMetricsState state,
        ISupervisorExceptionLog exceptionLog,
        ISupervisorHealthLlmAdvisorySink advisory,
        ISupervisorHealthActionCoordinator actions,
        ILogger<SupervisorActorMetricsPollingService> logger,
        TimeProvider? timeProvider = null,
        TimeSpan? interval = null)
    {
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _exceptionLog = exceptionLog ?? throw new ArgumentNullException(nameof(exceptionLog));
        _advisory = advisory ?? throw new ArgumentNullException(nameof(advisory));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _interval = interval ?? DefaultInterval;
        if (_interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));
    }

    /// <inheritdoc />
    public SupervisorPollingServiceState State =>
        (SupervisorPollingServiceState)Volatile.Read(ref _serviceState);

    /// <inheritdoc />
    public void Start()
    {
        try
        {
            lock (_gate)
            {
                if (_thread is { IsAlive: true }) return;
                _stop.Reset();
                Volatile.Write(ref _serviceState, (int)SupervisorPollingServiceState.Starting);
                _thread = new Thread(ThreadMain)
                {
                    Name = PollingThreadName,
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                _thread.Start();
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            Volatile.Write(ref _serviceState, (int)SupervisorPollingServiceState.Faulted);
            TryPollCycleFailure(exception);
        }
    }

    /// <inheritdoc />
    public bool Stop(TimeSpan timeout)
    {
        try
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan) return false;
            Thread? thread;
            lock (_gate)
            {
                thread = _thread;
                if (thread is null)
                {
                    Volatile.Write(ref _serviceState, (int)SupervisorPollingServiceState.Stopped);
                    return true;
                }
                Volatile.Write(ref _serviceState, (int)SupervisorPollingServiceState.Stopping);
                _stop.Set();
            }
            if (thread == Thread.CurrentThread) return false;
            var stopped = thread.Join(timeout);
            if (stopped)
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_thread, thread)) _thread = null;
                }
                Volatile.Write(ref _serviceState, (int)SupervisorPollingServiceState.Stopped);
            }
            return stopped;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            TryPollCycleFailure(exception);
            return false;
        }
    }

    /// <inheritdoc />
    public SupervisorPollingServiceStatus CaptureStatus()
    {
        try
        {
            var ticks = Volatile.Read(ref _lastHeartbeatUtcTicks);
            return new(
                State,
                PollingThreadName,
                _thread?.IsAlive == true,
                Interlocked.Read(ref _completedCycles),
                Interlocked.Read(ref _failedCycles),
                Interlocked.Read(ref _overruns),
                ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            TryPollCycleFailure(exception);
            return new(SupervisorPollingServiceState.Faulted, PollingThreadName, false, 0, 1, 0, null);
        }
    }

    void ThreadMain()
    {
        Volatile.Write(ref _serviceState, (int)SupervisorPollingServiceState.Running);
        var deadline = Stopwatch.GetTimestamp();
        try
        {
            while (!_stop.IsSet)
            {
                RunCycleWithFinalContainment();
                deadline += (long)(_interval.TotalSeconds * Stopwatch.Frequency);
                var remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), deadline);
                if (remaining <= TimeSpan.Zero)
                {
                    Interlocked.Increment(ref _overruns);
                    deadline = Stopwatch.GetTimestamp() + (long)(_interval.TotalSeconds * Stopwatch.Frequency);
                    remaining = _interval;
                }
                _stop.Wait(remaining);
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            Interlocked.Increment(ref _failedCycles);
            TryPollCycleFailure(exception);
        }
        finally
        {
            Volatile.Write(ref _serviceState, (int)SupervisorPollingServiceState.Stopped);
        }
    }

    void RunCycleWithFinalContainment()
    {
        var started = Stopwatch.GetTimestamp();
        SupervisorActorMetricsSnapshot snapshot;
        try
        {
            snapshot = BuildSnapshot(started);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            Interlocked.Increment(ref _failedCycles);
            TryPollCycleFailure(exception);
            snapshot = BuildMinimalSnapshot(started);
        }

        try { _state.Store(snapshot); }
        catch (Exception exception) when (IsRecoverable(exception)) { TryPollCycleFailure(exception); }

        TryHeartbeat(snapshot);
        try { _actions.Observe(snapshot); }
        catch (Exception exception) when (IsRecoverable(exception)) { TryPollCycleFailure(exception); }
        TryAdvisory(snapshot);
        Volatile.Write(ref _lastHeartbeatUtcTicks, snapshot.ObservedUtc.Ticks);
        Interlocked.Increment(ref _completedCycles);
    }

    SupervisorActorMetricsSnapshot BuildSnapshot(long started)
    {
        var sources = _actors.Current ?? [];
        var collected = new List<SupervisorActorMetrics>(sources.Count);
        var failedActors = 0;
        foreach (var source in sources)
        {
            try { collected.Add(source.CaptureSnapshot()); }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failedActors++;
                TryActorSnapshotFailure(source.ActorId, exception);
            }
        }

        var completed = Stopwatch.GetTimestamp();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var healthy = collected.Count(value => value.Health == SupervisorActorHealth.Healthy);
        var degraded = collected.Count(value => value.Health == SupervisorActorHealth.Degraded);
        var critical = collected.Count(value => value.Health == SupervisorActorHealth.Critical);
        var explicitUnknown = collected.Count(value => value.Health == SupervisorActorHealth.Unknown);
        var process = CaptureProcessMetrics();
        return new(
            Interlocked.Increment(ref _revision), now, started, completed,
            Stopwatch.GetElapsedTime(started, completed), now + _interval,
            sources.Count, collected.Count, failedActors, healthy, degraded, critical,
            explicitUnknown + failedActors, collected.Sum(value => value.EntityMailboxes),
            collected.Sum(value => value.QueueDepth), collected.Sum(value => value.Rejected),
            collected.Sum(value => value.Failed),
            failedActors == 0 ? SupervisorSnapshotQuality.Complete : SupervisorSnapshotQuality.Partial,
            collected.ToArray(), process.CpuPercent, process.WorkingSetBytes, process.ManagedHeapBytes,
            process.TotalAllocatedBytes, process.Gen0Collections, process.Gen1Collections, process.Gen2Collections,
            process.TotalGcPause, process.ThreadPoolPendingItems, process.ThreadPoolThreads);
    }

    SupervisorActorMetricsSnapshot BuildMinimalSnapshot(long started)
    {
        var completed = Stopwatch.GetTimestamp();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return new(
            Interlocked.Increment(ref _revision), now, started, completed,
            Stopwatch.GetElapsedTime(started, completed), now + _interval,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            SupervisorSnapshotQuality.Minimal, []);
    }

    ProcessMetrics CaptureProcessMetrics()
    {
        using var process = Process.GetCurrentProcess();
        var now = Stopwatch.GetTimestamp();
        var processorTime = process.TotalProcessorTime;
        double cpu = 0;
        lock (_processSampleGate)
        {
            if (_lastProcessTimestamp != 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(_lastProcessTimestamp, now).TotalSeconds;
                if (elapsed > 0)
                    cpu = Math.Clamp((processorTime - _lastProcessorTime).TotalSeconds /
                        (elapsed * Environment.ProcessorCount) * 100, 0, 100);
            }
            _lastProcessTimestamp = now;
            _lastProcessorTime = processorTime;
        }
        var gc = GC.GetGCMemoryInfo();
        return new(cpu, process.WorkingSet64, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(false),
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), Sum(gc.PauseDurations),
            ThreadPool.PendingWorkItemCount, ThreadPool.ThreadCount);
    }

    static TimeSpan Sum(ReadOnlySpan<TimeSpan> values)
    {
        var total = TimeSpan.Zero;
        foreach (var value in values) total += value;
        return total;
    }

    readonly record struct ProcessMetrics(double CpuPercent, long WorkingSetBytes, long ManagedHeapBytes,
        long TotalAllocatedBytes, long Gen0Collections, long Gen1Collections, long Gen2Collections,
        TimeSpan TotalGcPause, long ThreadPoolPendingItems, int ThreadPoolThreads);

    void TryHeartbeat(SupervisorActorMetricsSnapshot snapshot)
    {
        try
        {
            SupervisorPollingLog.Heartbeat(
                _logger, snapshot.Revision, snapshot.ExpectedActors, snapshot.CollectedActors,
                snapshot.FailedActors, snapshot.HealthyActors, snapshot.DegradedActors,
                snapshot.CriticalActors, snapshot.UnknownActors, snapshot.EntityMailboxes,
                snapshot.QueueDepth, snapshot.Rejected, snapshot.Failed, snapshot.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            try { _exceptionLog.HeartbeatLogFailed(exception); }
            catch (Exception fallbackException) when (IsRecoverable(fallbackException)) { }
        }
    }

    void TryAdvisory(SupervisorActorMetricsSnapshot snapshot)
    {
        try
        {
            _advisory.Observe(new(
                snapshot.Revision, snapshot.ObservedUtc, snapshot.HealthyActors,
                snapshot.DegradedActors, snapshot.CriticalActors, snapshot.UnknownActors));
        }
        catch (Exception exception) when (IsRecoverable(exception)) { TryPollCycleFailure(exception); }
    }

    void TryActorSnapshotFailure(TomasAI.IFM.Shared.EventModelActor.ActorMailboxId actorId, Exception exception)
    {
        try { _exceptionLog.ActorSnapshotFailed(actorId, exception); }
        catch (Exception fallbackException) when (IsRecoverable(fallbackException)) { }
    }

    void TryPollCycleFailure(Exception exception)
    {
        try { _exceptionLog.PollCycleFailed(exception); }
        catch (Exception fallbackException) when (IsRecoverable(fallbackException)) { }
    }

    static bool IsRecoverable(Exception exception) =>
        exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException;

    /// <inheritdoc />
    public void Dispose()
    {
        Stop(TimeSpan.FromSeconds(5));
        _stop.Dispose();
    }
}
