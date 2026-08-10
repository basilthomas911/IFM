using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>Low-cardinality OpenTelemetry instruments for durable event projection.</summary>
internal static class EventProjectorMetrics
{
    public const string MeterName = "TomasAI.IFM.Application.EventProjector";

    static readonly Meter Meter = new(MeterName);
    static readonly Counter<long> Events = Meter.CreateCounter<long>("ifm.event_projector.events");
    static readonly Histogram<double> StageDuration = Meter.CreateHistogram<double>(
        "ifm.event_projector.stage.duration", "ms");
    static readonly Histogram<double> RecoveryBatchDuration = Meter.CreateHistogram<double>(
        "ifm.event_projector.recovery.batch.duration", "ms");
    static readonly Histogram<double> StartupDuration = Meter.CreateHistogram<double>(
        "ifm.event_projector.startup.duration", "ms");
    static readonly Histogram<long> RecoveryBatchSize = Meter.CreateHistogram<long>(
        "ifm.event_projector.recovery.batch.size", "events");
    static readonly Histogram<double> OutboxPublishDuration = Meter.CreateHistogram<double>(
        "ifm.event_projector.outbox.publish.duration", "ms");
    static readonly ConcurrentDictionary<string, ProjectorMeasurements> Measurements =
        new(StringComparer.Ordinal);

    static EventProjectorMetrics()
    {
        Meter.CreateObservableGauge("ifm.event_projector.backlog.pending", ObservePending, "events");
        Meter.CreateObservableGauge("ifm.event_projector.backlog.oldest.age", ObserveOldestPendingAge, "s");
        Meter.CreateObservableGauge("ifm.event_projector.backlog.blocked", ObserveBlocked, "events");
        Meter.CreateObservableGauge("ifm.event_projector.backlog.terminal_failed", ObserveTerminalFailed, "events");
        Meter.CreateObservableGauge("ifm.event_projector.lease.expired", ObserveExpiredLeases, "leases");
        Meter.CreateObservableGauge("ifm.event_projector.outbox.pending", ObserveOutboxPending, "messages");
        Meter.CreateObservableGauge("ifm.event_projector.outbox.oldest.age", ObserveOldestOutboxAge, "s");
        Meter.CreateObservableGauge("ifm.event_projector.outbox.retrying", ObserveOutboxRetrying, "messages");
        Meter.CreateObservableGauge("ifm.event_projector.worker.busy", ObserveBusyWorkers, "workers");
        Meter.CreateObservableGauge("ifm.event_projector.worker.utilization", ObserveWorkerUtilization, "%");
        Meter.CreateObservableGauge("ifm.event_projector.ready", ObserveReady);
    }

    public static long GetTimestamp() => StageDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    public static void RecordEvent(
        string projectorName,
        string outcome,
        string operation = "projection")
    {
        if (!Events.Enabled)
            return;
        Events.Add(1,
            new KeyValuePair<string, object?>("projector", projectorName),
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("operation", operation));
    }

    public static void RecordStage(
        string projectorName,
        EventProjectorStageType stage,
        string outcome,
        long startedTimestamp)
    {
        if (startedTimestamp == 0 || !StageDuration.Enabled)
            return;
        StageDuration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            new KeyValuePair<string, object?>("projector", projectorName),
            new KeyValuePair<string, object?>("stage", StageTag(stage)),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static long GetRecoveryTimestamp() => RecoveryBatchDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    public static long GetStartupTimestamp() => StartupDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    public static void RecordStartup(string projectorName, string outcome, long startedTimestamp)
    {
        if (startedTimestamp == 0 || !StartupDuration.Enabled)
            return;
        StartupDuration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            new KeyValuePair<string, object?>("projector", projectorName),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static void RecordRecoveryBatch(string projectorName, int size, long startedTimestamp)
    {
        if (RecoveryBatchSize.Enabled)
            RecoveryBatchSize.Record(size, new KeyValuePair<string, object?>("projector", projectorName));
        if (startedTimestamp != 0 && RecoveryBatchDuration.Enabled)
            RecoveryBatchDuration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                new KeyValuePair<string, object?>("projector", projectorName));
    }

    public static long GetOutboxTimestamp() => OutboxPublishDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    public static void RecordOutboxPublish(string projectorName, string outcome, long startedTimestamp)
    {
        RecordEvent(projectorName, outcome, "outbox");
        if (startedTimestamp != 0 && OutboxPublishDuration.Enabled)
            OutboxPublishDuration.Record(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                new KeyValuePair<string, object?>("projector", projectorName),
                new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static void RegisterProjector(string projectorName, int workerCapacity)
    {
        var measurement = Measurements.GetOrAdd(projectorName, static _ => new ProjectorMeasurements());
        Volatile.Write(ref measurement.WorkerCapacity, workerCapacity);
    }

    public static void UnregisterProjector(string projectorName)
    {
        if (Measurements.TryGetValue(projectorName, out var measurement))
            Volatile.Write(ref measurement.WorkerCapacity, 0);
    }

    public static void SetReadiness(string projectorName, bool isReady)
        => Volatile.Write(
            ref Measurements.GetOrAdd(projectorName, static _ => new()).Ready,
            isReady ? 1 : 0);

    public static void WorkerBusy(string projectorName)
        => Interlocked.Increment(ref Measurements.GetOrAdd(projectorName, static _ => new()).BusyWorkers);

    public static void WorkerAvailable(string projectorName)
    {
        var measurement = Measurements.GetOrAdd(projectorName, static _ => new());
        if (Interlocked.Decrement(ref measurement.BusyWorkers) < 0)
            Volatile.Write(ref measurement.BusyWorkers, 0);
    }

    public static void UpdateSnapshot(
        string projectorName,
        EventProjectorOperationalSnapshotReadModel snapshot,
        DateTime observedAtUtc)
    {
        var measurement = Measurements.GetOrAdd(projectorName, static _ => new());
        Volatile.Write(ref measurement.Pending, snapshot.PendingCount);
        Volatile.Write(ref measurement.Blocked, snapshot.BlockedCount);
        Volatile.Write(ref measurement.TerminalFailed, snapshot.TerminalFailedCount);
        Volatile.Write(ref measurement.ExpiredLeases, snapshot.ExpiredLeaseCount);
        Volatile.Write(ref measurement.OutboxPending, snapshot.OutboxPendingCount);
        Volatile.Write(ref measurement.OutboxRetrying, snapshot.OutboxRetryCount);
        Volatile.Write(ref measurement.OldestPendingAgeSeconds,
            AgeSeconds(observedAtUtc, snapshot.OldestPendingAtUtc));
        Volatile.Write(ref measurement.OldestOutboxAgeSeconds,
            AgeSeconds(observedAtUtc, snapshot.OldestOutboxPendingAtUtc));
    }

    static double AgeSeconds(DateTime nowUtc, DateTime? timestampUtc)
        => timestampUtc.HasValue ? Math.Max(0, (nowUtc - timestampUtc.Value).TotalSeconds) : 0;

    static string StageTag(EventProjectorStageType stage) => stage switch
    {
        EventProjectorStageType.None => "none",
        EventProjectorStageType.ValidateSourceEvent => "validate-source-event",
        EventProjectorStageType.PublishProcessingEvent => "publish-processing-event",
        EventProjectorStageType.ApplyProjection => "apply-projection",
        EventProjectorStageType.PublishCompletedEvent => "publish-completed-event",
        EventProjectorStageType.PublishFailedEvent => "publish-failed-event",
        EventProjectorStageType.PersistCompletion => "persist-completion",
        EventProjectorStageType.Completed => "completed",
        _ => "unknown"
    };

    static IEnumerable<Measurement<long>> ObservePending() => ObserveLong(static value => value.Pending);
    static IEnumerable<Measurement<double>> ObserveOldestPendingAge() => ObserveDouble(static value => value.OldestPendingAgeSeconds);
    static IEnumerable<Measurement<long>> ObserveBlocked() => ObserveLong(static value => value.Blocked);
    static IEnumerable<Measurement<long>> ObserveTerminalFailed() => ObserveLong(static value => value.TerminalFailed);
    static IEnumerable<Measurement<long>> ObserveExpiredLeases() => ObserveLong(static value => value.ExpiredLeases);
    static IEnumerable<Measurement<long>> ObserveOutboxPending() => ObserveLong(static value => value.OutboxPending);
    static IEnumerable<Measurement<double>> ObserveOldestOutboxAge() => ObserveDouble(static value => value.OldestOutboxAgeSeconds);
    static IEnumerable<Measurement<long>> ObserveOutboxRetrying() => ObserveLong(static value => value.OutboxRetrying);
    static IEnumerable<Measurement<long>> ObserveBusyWorkers() => ObserveLong(static value => Volatile.Read(ref value.BusyWorkers));
    static IEnumerable<Measurement<double>> ObserveWorkerUtilization()
    {
        foreach (var pair in Measurements)
        {
            var capacity = Volatile.Read(ref pair.Value.WorkerCapacity);
            var busy = Volatile.Read(ref pair.Value.BusyWorkers);
            yield return new Measurement<double>(
                capacity <= 0 ? 0 : Math.Min(100, busy * 100d / capacity),
                new KeyValuePair<string, object?>("projector", pair.Key));
        }
    }
    static IEnumerable<Measurement<long>> ObserveReady() => ObserveLong(static value => value.Ready);

    static IEnumerable<Measurement<long>> ObserveLong(Func<ProjectorMeasurements, long> selector)
    {
        foreach (var pair in Measurements)
            yield return new Measurement<long>(selector(pair.Value),
                new KeyValuePair<string, object?>("projector", pair.Key));
    }

    static IEnumerable<Measurement<double>> ObserveDouble(Func<ProjectorMeasurements, double> selector)
    {
        foreach (var pair in Measurements)
            yield return new Measurement<double>(selector(pair.Value),
                new KeyValuePair<string, object?>("projector", pair.Key));
    }

    sealed class ProjectorMeasurements
    {
        public long Pending;
        public double OldestPendingAgeSeconds;
        public long Blocked;
        public long TerminalFailed;
        public long ExpiredLeases;
        public long OutboxPending;
        public double OldestOutboxAgeSeconds;
        public long OutboxRetrying;
        public int BusyWorkers;
        public int WorkerCapacity;
        public long Ready;
    }
}
