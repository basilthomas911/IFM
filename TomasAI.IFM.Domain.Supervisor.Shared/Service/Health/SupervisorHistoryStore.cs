using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

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
                        global::TomasAI.IFM.Domain.Supervisor.Shared.PersistenceDiagnosticLog.Write4(_logger, exception, point.Revision);
                    }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { /* Expected cancellation or nonfatal diagnostic failure; preserve the primary operation outcome. */ }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
        {
            global::TomasAI.IFM.Domain.Supervisor.Shared.PersistenceDiagnosticLog.Write5(_logger, exception);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Nonfatal history shutdown failures cancel the writer and release the shutdown source.")]
    public void Dispose()
    {
        _writes.Writer.TryComplete();
        try { if (!_writer.Wait(TimeSpan.FromSeconds(5))) _stopping.Cancel(); }
        catch (Exception) { _stopping.Cancel(); }
        _stopping.Dispose();
    }
}
