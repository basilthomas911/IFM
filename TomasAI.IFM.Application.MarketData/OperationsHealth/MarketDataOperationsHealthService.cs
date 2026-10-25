using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

/// <summary>
/// Independent bounded central registry. Recording performs only atomic fixed-array updates and can
/// never throw into a producer. Snapshot readers never invoke native, network, or database code.
/// </summary>
public sealed partial class MarketDataOperationsHealthService : IMarketDataOperationsRecorder
{
    sealed class Cell
    {
        internal long Received;
        internal long Completed;
        internal long Failed;
        internal long Coalesced;
        internal long LastObservedTicks;
        internal long LastSucceededTicks;
        internal long LastFailedTicks;
        internal long MarketDataAsOfTicks;
        internal long LatencyCount;
        internal long LatencyTotalTicks;
        internal long LatencyMaximumTicks;
        internal object? LastDiagnosticId;
        internal readonly long[] LatencyBuckets = new long[9];
    }

    readonly Cell[] cells = Enum.GetValues<MarketDataOperationStage>().Select(_ => new Cell()).ToArray();
    readonly System.Threading.Lock incidentsGate = new();
    readonly Dictionary<string, DatasetIncidentSnapshot> incidents = new(StringComparer.Ordinal);
    readonly DatasetWorkerAdmissionRegistry admissions;
    readonly TimeProvider time;
    long revision;

    public MarketDataOperationsHealthService(DatasetWorkerAdmissionRegistry admissions, TimeProvider? timeProvider = null)
    {
        this.admissions = admissions ?? throw new ArgumentNullException(nameof(admissions));
        time = timeProvider ?? TimeProvider.System;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A diagnostic recorder failure must not abort realtime ingestion or recovery; this boundary isolates the observer.")]
    public void Record(in MarketDataOperationMeasurement measurement)
    {
        try
        {
            var index = (int)measurement.Stage;
            if ((uint)index >= (uint)cells.Length)
                return;
            var cell = cells[index];
            var occurred = NormalizeUtc(measurement.OccurredAtUtc).Ticks;
            Interlocked.Increment(ref cell.Received);
            Interlocked.Exchange(ref cell.LastObservedTicks, occurred);
            Volatile.Write(ref cell.LastDiagnosticId, measurement.UpdateId);
            if (measurement.MarketDataAsOfUtc is { } marketDataAsOf)
                Interlocked.Exchange(ref cell.MarketDataAsOfTicks, NormalizeUtc(marketDataAsOf).Ticks);
            if (measurement.Outcome == MarketDataOperationOutcome.Failed)
            {
                Interlocked.Increment(ref cell.Failed);
                Interlocked.Exchange(ref cell.LastFailedTicks, occurred);
            }
            else if (measurement.Outcome == MarketDataOperationOutcome.Coalesced)
            {
                Interlocked.Increment(ref cell.Coalesced);
            }
            else if (measurement.Outcome is MarketDataOperationOutcome.Completed
                     or MarketDataOperationOutcome.Published
                     or MarketDataOperationOutcome.Composed
                     or MarketDataOperationOutcome.Applied
                     or MarketDataOperationOutcome.Changed)
            {
                Interlocked.Increment(ref cell.Completed);
                Interlocked.Exchange(ref cell.LastSucceededTicks, occurred);
            }
            if (measurement.Latency is { } latency)
                RecordLatency(cell, latency);
            Interlocked.Increment(ref revision);
        }
        catch
        {
            // A health recorder is never allowed to fail its monitored producer.
        }
    }

    public void RecordIncident(DatasetIncidentSnapshot incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        if (string.IsNullOrWhiteSpace(incident.Dataset) || incident.Dataset.Length > 64) return;
        lock (incidentsGate)
        {
            if (incidents.Count >= 16 && !incidents.ContainsKey(incident.Dataset)) return;
            incidents[incident.Dataset] = incident;
        }
        Interlocked.Increment(ref revision);
    }

    public MarketDataOperationsHealthSnapshot GetSnapshot()
    {
        Dictionary<string, DatasetIncidentSnapshot> incidentCopy;
        lock (incidentsGate)
            incidentCopy = incidents.ToDictionary(StringComparer.Ordinal);
        var stages = Enum.GetValues<MarketDataOperationStage>()
            .ToDictionary(stage => stage, stage => Snapshot(stage, cells[(int)stage]));
        var activeStages = stages.Values.Where(stage => stage.Received != 0).ToArray();
        var overall = (incidentCopy.Values.Any(value => value.ProcessReplacementLatched)
            || activeStages.Any(value => value.Status == MarketDataOperationsStatus.Red)) switch { true => MarketDataOperationsStatus.Red, false => (incidentCopy.Values.Any(value => value.IsOpen)
                  || activeStages.Any(value => value.Status == MarketDataOperationsStatus.Orange)) switch { true => MarketDataOperationsStatus.Orange, false => (activeStages.Any(value => value.Status == MarketDataOperationsStatus.Yellow)) switch { true => MarketDataOperationsStatus.Yellow, false => (activeStages.Length == 0) switch { true => MarketDataOperationsStatus.Inactive, false => MarketDataOperationsStatus.Green } } } };
        return new()
        {
            Revision = Interlocked.Read(ref revision),
            ObservedOnUtc = time.GetUtcNow().UtcDateTime,
            OverallStatus = overall,
            Stages = new System.Collections.ObjectModel.ReadOnlyDictionary<MarketDataOperationStage, MarketDataStageHealth>(stages),
            DatasetIncidents = new System.Collections.ObjectModel.ReadOnlyDictionary<string, DatasetIncidentSnapshot>(incidentCopy),
            RejectedStaleGenerationPublications = admissions.RejectedPublications
        };
    }

    static MarketDataStageHealth Snapshot(MarketDataOperationStage stage, Cell cell)
    {
        var received = Interlocked.Read(ref cell.Received);
        var completed = Interlocked.Read(ref cell.Completed);
        var failed = Interlocked.Read(ref cell.Failed);
        var coalesced = Interlocked.Read(ref cell.Coalesced);
        var lastFailed = Interlocked.Read(ref cell.LastFailedTicks);
        var lastSucceeded = Interlocked.Read(ref cell.LastSucceededTicks);
        var status = (received == 0) switch { true => MarketDataOperationsStatus.Inactive, false => (lastFailed > lastSucceeded && completed == 0) switch { true => MarketDataOperationsStatus.Red, false => (lastFailed > lastSucceeded) switch { true => MarketDataOperationsStatus.Yellow, false => (completed == 0) switch { true => MarketDataOperationsStatus.Yellow, false => MarketDataOperationsStatus.Green } } } };
        var count = Interlocked.Read(ref cell.LatencyCount);
        return new()
        {
            Stage = stage,
            Status = status,
            Received = received,
            Completed = completed,
            Failed = failed,
            Coalesced = coalesced,
            LastObservedUtc = ReadUtc(ref cell.LastObservedTicks),
            LastSucceededUtc = ReadUtc(ref cell.LastSucceededTicks),
            LastFailedUtc = ReadUtc(ref cell.LastFailedTicks),
            MarketDataAsOfUtc = ReadUtc(ref cell.MarketDataAsOfTicks),
            AverageLatency = count == 0 ? TimeSpan.Zero
                : TimeSpan.FromTicks(Interlocked.Read(ref cell.LatencyTotalTicks) / count),
            MaximumLatency = TimeSpan.FromTicks(Interlocked.Read(ref cell.LatencyMaximumTicks)),
            LastDiagnosticId = Volatile.Read(ref cell.LastDiagnosticId) is Guid id ? id : Guid.Empty,
            Reason = status switch
            {
                MarketDataOperationsStatus.Red => "The stage has failed without a recorded success.",
                MarketDataOperationsStatus.Yellow when lastFailed > lastSucceeded => "The latest failure has no later recorded success.",
                MarketDataOperationsStatus.Yellow => "Work was observed without completed progress.",
                MarketDataOperationsStatus.Green => "The stage is recording successful progress.",
                _ => "The stage has no current-process observations."
            }
        };
    }

    static void RecordLatency(Cell cell, TimeSpan latency)
    {
        var ticks = Math.Max(0, latency.Ticks);
        var bucket = 0;
        while (bucket < LatencyUpperMilliseconds.Length - 1
            && ticks > TimeSpan.FromMilliseconds(LatencyUpperMilliseconds[bucket]).Ticks) bucket++;
        Interlocked.Increment(ref cell.LatencyBuckets[bucket]);
        Interlocked.Increment(ref cell.LatencyCount);
        Interlocked.Add(ref cell.LatencyTotalTicks, ticks);
        var current = Interlocked.Read(ref cell.LatencyMaximumTicks);
        while (ticks > current)
        {
            var observed = Interlocked.CompareExchange(ref cell.LatencyMaximumTicks, ticks, current);
            if (observed == current) break;
            current = observed;
        }
    }

    static readonly double[] LatencyUpperMilliseconds = [1, 5, 10, 50, 100, 500, 1_000, 5_000, 60_000];

    TimeSpan Percentile(MarketDataOperationStage stage, double percentile)
    {
        var cell = cells[(int)stage];
        var bins = new long[cell.LatencyBuckets.Length];
        for (var index = 0; index < bins.Length; index++)
            bins[index] = Interlocked.Read(ref cell.LatencyBuckets[index]);
        var total = bins.Sum();
        if (total == 0) return TimeSpan.Zero;
        var rank = (long)Math.Ceiling(total * percentile);
        long cumulative = 0;
        for (var index = 0; index < bins.Length; index++)
        {
            cumulative += bins[index];
            if (cumulative >= rank)
                return index == bins.Length - 1
                    ? TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(60).Ticks, Interlocked.Read(ref cell.LatencyMaximumTicks)))
                    : TimeSpan.FromMilliseconds(LatencyUpperMilliseconds[index]);
        }
        return TimeSpan.FromMilliseconds(LatencyUpperMilliseconds[^1]);
    }

    static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value : value.ToUniversalTime();

    static DateTime? ReadUtc(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTime(value, DateTimeKind.Utc);
    }
}

public sealed partial class MarketDataOperationsHealthService
{
    sealed record RuntimeProjection(DateTime ObservedOnUtc, string Session, DateOnly? ValueDate,
        DateTime? LastProbe, DateTime? NextProbe, IReadOnlyList<MarketDataDatasetHealthReadModel> Datasets,
        IReadOnlyDictionary<MarketDataOperationStage, MarketDataOperationStageReadModel> Stages);
    RuntimeProjection? runtimeProjection;

    /// <summary>Called by an independent observer using cached, process-local state only.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public void ObserveRuntime(MarketSessionReadModel session, DatabentoLifecycleSnapshot watchdog,
        IReadOnlyList<DatasetWorkerProcessSnapshot> workers, MarketOutlookProcessorMetricsSnapshot outlook,
        RealtimeTickPublisherSnapshot? publisher, DatabentoStage3Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(outlook);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(watchdog);
        ArgumentNullException.ThrowIfNull(workers);
        if (workers.Count > 16) throw new ArgumentOutOfRangeException(nameof(workers));
        var now = time.GetUtcNow().UtcDateTime;
        var active = session.IsMarketOpen;
        var interval = options.Enabled ? options.ScheduledInterval(session.State) : TimeSpan.FromSeconds(15);
        var lastProbe = watchdog.LastObservation?.ObservedOnUtc;
        var nextProbe = active && lastProbe is { } observed && interval > TimeSpan.Zero
            ? observed + interval : (DateTime?)null;
        var existing = Volatile.Read(ref runtimeProjection);
        var currentIncidents = GetSnapshot().DatasetIncidents;
        var datasets = workers.Select(worker =>
        {
            currentIncidents.TryGetValue(worker.Dataset, out var incident);
            var prior = existing?.Datasets.FirstOrDefault(value => string.Equals(value.Dataset,worker.Dataset,StringComparison.Ordinal));
            var diagnostic = worker.Diagnostics;
            var complete = diagnostic?.Complete == true;
            var stale = active && diagnostic is not null && now - diagnostic.ObservedOnUtc > interval + TimeSpan.FromSeconds(30);
            var status = (!active && !worker.Running) switch { true => "Inactive", false => (incident?.ProcessReplacementLatched == true || !worker.Running || !worker.Healthy || !complete) switch { true => "Red", false => (incident?.IsOpen == true) switch { true => "Orange", false => (stale) switch { true => "Yellow", false => "Green" } } } };
            return new MarketDataDatasetHealthReadModel
            {
                Dataset = worker.Dataset,
                Status = status,
                SessionState = session.State.ToString(),
                ValueDate = session.ActiveValueDate,
                ProcessId = worker.ProcessId,
                WorkerInstanceId = worker.WorkerInstanceId,
                GenerationId = worker.GenerationId,
                StartedOnUtc = worker.StartedOnUtc,
                LastObservedUtc = diagnostic?.ObservedOnUtc,
                LastHealthyUtc = worker.Healthy && complete && !stale ? diagnostic?.ObservedOnUtc : prior?.LastHealthyUtc,
                Running = worker.Running,
                Healthy = worker.Healthy && complete && !stale,
                GracefulStopSucceeded = worker.GracefulStopSucceeded,
                ForcedTermination = worker.ForcedTermination,
                CooperativeAttempts = incident?.CooperativeAttempts ?? 0,
                ProcessReplacementCount = incident?.ProcessReplacements ?? 0,
                ProcessReplacementLatched = incident?.ProcessReplacementLatched == true,
                IncidentAge = incident?.IsOpen == true ? incident.UnhealthyDuration : null,
                IncidentOpenedUtc = incident?.IsOpen == true ? incident.ObservedOnUtc - incident.UnhealthyDuration : null,
                NextProbeUtc = nextProbe,
                RecordsProduced = diagnostic?.RecordsProduced ?? 0,
                RecordsConsumed = diagnostic?.RecordsConsumed ?? 0,
                RingUsed = diagnostic?.RingUsed ?? 0,
                RingCapacity = diagnostic?.RingCapacity ?? 0,
                ChannelBatchCount = diagnostic?.ChannelBatchCount ?? 0,
                ChannelBatchCapacity = diagnostic?.ChannelBatchCapacity ?? 0,
                RecordsStarted = diagnostic?.Aggregation?.RecordsStarted ?? 0,
                RecordsCompleted = diagnostic?.Aggregation?.RecordsCompleted ?? 0,
                Reason = (!complete) switch { true => "Worker diagnostic evidence is incomplete; current health is unconfirmed.", false => (stale) switch { true => "Worker diagnostic observation is overdue; current health is unconfirmed.", false => BoundReason(incident?.IsOpen == true ? incident.FailureReason.ToString() : worker.Detail) } }
            };
        }).ToArray();
        var overlays = new Dictionary<MarketDataOperationStage, MarketDataOperationStageReadModel>();
        if (options.Enabled)
        {
            var status = (!active && datasets.All(value => !value.Running)) switch { true => "Inactive", false => (datasets.Length == 0 || datasets.Any(value => string.Equals(value.Status,"Red",StringComparison.Ordinal))) switch { true => "Red", false => (datasets.Any(value => string.Equals(value.Status,"Orange",StringComparison.Ordinal))) switch { true => "Orange", false => (datasets.Any(value => string.Equals(value.Status,"Yellow",StringComparison.Ordinal))) switch { true => "Yellow", false => "Green" } } } };
            overlays[MarketDataOperationStage.DatabentoWorkerProcess] = Gauge(
                MarketDataOperationStage.DatabentoWorkerProcess, status, "WorkerOwnership",
                datasets.Length == 0 && active ? "No supervised dataset is owned in an active session." : "Exact supervised process/control state.",
                now, active, datasets.Length, 16);
            var diagnosticFeeds = workers.Select(value => value.Diagnostics).OfType<DatasetWorkerDiagnostics>().ToArray();
            foreach (var stage in new[] { MarketDataOperationStage.DatabentoNative, MarketDataOperationStage.DatabentoInterop,
                MarketDataOperationStage.DatabentoAggregation })
            {
                var pending = (stage == MarketDataOperationStage.DatabentoInterop) switch { true => diagnosticFeeds.Sum(value => (long)Math.Min(value.RingUsed, (ulong)long.MaxValue)), false => (stage == MarketDataOperationStage.DatabentoAggregation) switch { true => diagnosticFeeds.Sum(value => (long)value.ChannelBatchCount), false => 0 } };
                var capacity = (stage == MarketDataOperationStage.DatabentoInterop) switch { true => diagnosticFeeds.Sum(value => (long)Math.Min(value.RingCapacity, (ulong)long.MaxValue)), false => (stage == MarketDataOperationStage.DatabentoAggregation) switch { true => diagnosticFeeds.Sum(value => (long)value.ChannelBatchCapacity), false => 0 } };
                overlays[stage] = Gauge(stage, status, "WorkerDiagnostics", "Native and managed progress from dataset workers.",
                    diagnosticFeeds.Length == 0 ? now : diagnosticFeeds.Min(value => value.ObservedOnUtc), active, pending, capacity);
            }
        }
        var pendingAge = outlook.OldestPendingUtc is { } oldest ? MaxAge(now - oldest) : TimeSpan.Zero;
        var outlookStatus = (!active) switch { true => "Inactive", false => (!outlook.IsProcessorReady) switch { true => "Red", false => (outlook.PendingCount > 0 && pendingAge > TimeSpan.FromMinutes(1)) switch { true => "Red", false => (outlook.PendingCount > 0 && pendingAge > TimeSpan.FromSeconds(5)) switch { true => "Yellow", false => "Green" } } } };
        foreach (var stage in new[] { MarketDataOperationStage.MarketOutlookChannel, MarketDataOperationStage.MarketOutlookComposition })
            overlays[stage] = Gauge(stage, outlookStatus,
                (!outlook.IsProcessorReady) switch { true => "ProcessorUnavailable", false => (outlookStatus is "Red" or "Yellow") switch { true => "PendingWorkAged", false => "CurrentProgress" } },
                !outlook.IsProcessorReady ? "Market Outlook processor is not ready." : "Local Market Outlook pending work and processor readiness.",
                now, active, outlook.PendingCount, 0) with
            { OldestPendingAge = pendingAge };
        if (publisher is { PolicyEnabled: true })
        {
            var status = (!active && !publisher.Running) switch { true => "Inactive", false => (publisher.Faulted || !publisher.Running) switch { true => "Red", false => (publisher.Depth >= publisher.Capacity) switch { true => "Yellow", false => "Green" } } };
            overlays[MarketDataOperationStage.DatabentoRealtimePublication] = Gauge(
                MarketDataOperationStage.DatabentoRealtimePublication, status, publisher.Failure.ToString(),
                publisher.FailureDetail, now, active, publisher.Depth, publisher.Capacity) with
            {
                Received = publisher.Accepted,
                Completed = publisher.Published,
                Failed = publisher.Failed + publisher.Rejected + publisher.Expired,
                Saturated = publisher.SaturationCount,
                OldestPendingAge = publisher.OldestQueuedAge
            };
        }
        Volatile.Write(ref runtimeProjection, new(now, session.State.ToString(), session.ActiveValueDate, lastProbe, nextProbe,
            Array.AsReadOnly(datasets), new ReadOnlyDictionary<MarketDataOperationStage, MarketDataOperationStageReadModel>(overlays)));
        Interlocked.Increment(ref revision);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public MarketDataOperationsHealthReadModel GetReadModel()
    {
        var snapshot = GetSnapshot();
        var runtime = Volatile.Read(ref runtimeProjection);
        var stages = snapshot.Stages.Values.Select(value =>
        {
            var result = new MarketDataOperationStageReadModel
            {
                Stage = value.Stage.ToString(),
                Status = value.Status.ToString(),
                Reason = value.Reason,
                ReasonCode = (value.Received == 0) switch { true => "NotObserved", false => (value.Status == MarketDataOperationsStatus.Green) switch { true => "CurrentProgress", false => "ProgressUnconfirmed" } },
                Received = value.Received,
                Completed = value.Completed,
                Failed = value.Failed,
                Coalesced = value.Coalesced,
                LastObservedUtc = value.LastObservedUtc,
                LastSucceededUtc = value.LastSucceededUtc,
                LastFailedUtc = value.LastFailedUtc,
                MarketDataAsOfUtc = value.MarketDataAsOfUtc,
                AverageLatency = value.AverageLatency,
                MaximumLatency = value.MaximumLatency,
                P50Latency = Percentile(value.Stage, .50),
                P95Latency = Percentile(value.Stage, .95),
                P99Latency = Percentile(value.Stage, .99)
            };
            if (runtime?.Stages.TryGetValue(value.Stage, out var overlay) == true)
            {
                var useOverlayStatus = StatusRank(overlay.Status) >= StatusRank(result.Status);
                result = result with
                {
                    Required = overlay.Required,
                    Status = useOverlayStatus ? overlay.Status : result.Status,
                    Reason = useOverlayStatus ? overlay.Reason : result.Reason,
                    ReasonCode = useOverlayStatus ? overlay.ReasonCode : result.ReasonCode,
                    Pending = overlay.Pending,
                    Capacity = overlay.Capacity,
                    HighWater = overlay.HighWater,
                    Saturated = overlay.Saturated,
                    OldestPendingAge = overlay.OldestPendingAge,
                    LastObservedUtc = overlay.LastObservedUtc,
                    Received = Math.Max(result.Received, overlay.Received),
                    Completed = Math.Max(result.Completed, overlay.Completed),
                    Failed = Math.Max(result.Failed, overlay.Failed)
                };
            }
            if (string.Equals(runtime?.Session,"Closed",StringComparison.Ordinal)) result = result with { Status = "Inactive", Required = false, ReasonCode = "SessionClosed", Reason = "Planned session closure; historical counters retained." };
            return result;
        }).ToArray();
        var overall = stages.Select(value => value.Status).Concat(runtime?.Datasets.Select(value => value.Status) ?? [])
            .OrderByDescending(StatusRank).FirstOrDefault() ?? "Inactive";
        if (runtime is null) overall = "Orange";
        else if (snapshot.ObservedOnUtc - runtime.ObservedOnUtc > TimeSpan.FromSeconds(15)
                 && StatusRank(overall) < StatusRank("Orange")) overall = "Orange";
        return new()
        {
            Revision = snapshot.Revision,
            ObservedOnUtc = runtime?.ObservedOnUtc ?? snapshot.ObservedOnUtc,
            OverallStatus = overall,
            SessionState = runtime?.Session ?? "Unknown",
            ValueDate = runtime?.ValueDate,
            LastProbeUtc = runtime?.LastProbe,
            NextProbeUtc = runtime?.NextProbe,
            RejectedStaleGenerationPublications = snapshot.RejectedStaleGenerationPublications,
            Stages = Array.AsReadOnly(stages),
            Datasets = runtime?.Datasets ?? []
        };
    }

    static MarketDataOperationStageReadModel Gauge(MarketDataOperationStage stage, string status,
        string code, string reason, DateTime observed, bool required, long pending, long capacity) => new()
        {
            Stage = stage.ToString(),
            Status = status,
            ReasonCode = code,
            Reason = BoundReason(reason),
            LastObservedUtc = observed,
            Required = required,
            Pending = pending,
            Capacity = capacity
        };
    static int StatusRank(string value) => value switch { "Red" => 4, "Orange" => 3, "Yellow" => 2, "Green" => 1, _ => 0 };
    static TimeSpan MaxAge(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
    static string BoundReason(string value) => value.Length <= 4096 ? value : value[..4096];
}
