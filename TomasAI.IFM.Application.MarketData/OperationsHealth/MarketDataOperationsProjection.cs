using System.Collections.ObjectModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public sealed partial class MarketDataOperationsHealthService
{
    sealed record RuntimeProjection(DateTime ObservedOnUtc, string Session, DateOnly? ValueDate,
        DateTime? LastProbe, DateTime? NextProbe, IReadOnlyList<MarketDataDatasetHealthReadModel> Datasets,
        IReadOnlyDictionary<MarketDataOperationStage, MarketDataOperationStageReadModel> Stages);
    RuntimeProjection? runtimeProjection;

    /// <summary>Called by an independent observer using cached, process-local state only.</summary>
    public void ObserveRuntime(MarketSessionReadModel session, DatabentoLifecycleSnapshot watchdog,
        IReadOnlyList<DatasetWorkerProcessSnapshot> workers, MarketOutlookProcessorMetricsSnapshot outlook,
        RealtimeTickPublisherSnapshot? publisher, DatabentoStage3Options options)
    {
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
            var prior = existing?.Datasets.FirstOrDefault(value => value.Dataset == worker.Dataset);
            var diagnostic = worker.Diagnostics;
            var complete = diagnostic?.Complete == true;
            var stale = active && diagnostic is not null && now - diagnostic.ObservedOnUtc > interval + TimeSpan.FromSeconds(30);
            var status = !active && !worker.Running ? "Inactive"
                : incident?.ProcessReplacementLatched == true || !worker.Running || !worker.Healthy || !complete ? "Red"
                : incident?.IsOpen == true ? "Orange" : stale ? "Yellow" : "Green";
            return new MarketDataDatasetHealthReadModel
            {
                Dataset = worker.Dataset, Status = status, SessionState = session.State.ToString(),
                ValueDate = session.ActiveValueDate, ProcessId = worker.ProcessId,
                WorkerInstanceId = worker.WorkerInstanceId, GenerationId = worker.GenerationId,
                StartedOnUtc = worker.StartedOnUtc, LastObservedUtc = diagnostic?.ObservedOnUtc,
                LastHealthyUtc = worker.Healthy && complete && !stale ? diagnostic?.ObservedOnUtc : prior?.LastHealthyUtc,
                Running = worker.Running, Healthy = worker.Healthy && complete && !stale,
                GracefulStopSucceeded = worker.GracefulStopSucceeded, ForcedTermination = worker.ForcedTermination,
                CooperativeAttempts = incident?.CooperativeAttempts ?? 0,
                ProcessReplacementCount = incident?.ProcessReplacements ?? 0,
                ProcessReplacementLatched = incident?.ProcessReplacementLatched == true,
                IncidentAge = incident?.IsOpen == true ? incident.UnhealthyDuration : null,
                IncidentOpenedUtc = incident?.IsOpen == true ? incident.ObservedOnUtc - incident.UnhealthyDuration : null,
                NextProbeUtc = nextProbe, RecordsProduced = diagnostic?.RecordsProduced ?? 0,
                RecordsConsumed = diagnostic?.RecordsConsumed ?? 0, RingUsed = diagnostic?.RingUsed ?? 0,
                RingCapacity = diagnostic?.RingCapacity ?? 0, ChannelBatchCount = diagnostic?.ChannelBatchCount ?? 0,
                ChannelBatchCapacity = diagnostic?.ChannelBatchCapacity ?? 0,
                RecordsStarted = diagnostic?.Aggregation?.RecordsStarted ?? 0,
                RecordsCompleted = diagnostic?.Aggregation?.RecordsCompleted ?? 0,
                Reason = !complete ? "Worker diagnostic evidence is incomplete; current health is unconfirmed."
                    : stale ? "Worker diagnostic observation is overdue; current health is unconfirmed."
                    : BoundReason(incident?.IsOpen == true ? incident.FailureReason.ToString() : worker.Detail)
            };
        }).ToArray();
        var overlays = new Dictionary<MarketDataOperationStage, MarketDataOperationStageReadModel>();
        if (options.Enabled)
        {
            var status = !active && datasets.All(value => !value.Running) ? "Inactive"
                : datasets.Length == 0 || datasets.Any(value => value.Status == "Red") ? "Red"
                : datasets.Any(value => value.Status == "Orange") ? "Orange"
                : datasets.Any(value => value.Status == "Yellow") ? "Yellow" : "Green";
            overlays[MarketDataOperationStage.DatabentoWorkerProcess] = Gauge(
                MarketDataOperationStage.DatabentoWorkerProcess, status, "WorkerOwnership",
                datasets.Length == 0 && active ? "No supervised dataset is owned in an active session." : "Exact supervised process/control state.",
                now, active, datasets.Length, 16);
            var diagnosticFeeds = workers.Select(value => value.Diagnostics).OfType<DatasetWorkerDiagnostics>().ToArray();
            foreach (var stage in new[] { MarketDataOperationStage.DatabentoNative, MarketDataOperationStage.DatabentoInterop,
                MarketDataOperationStage.DatabentoAggregation })
            {
                var pending = stage == MarketDataOperationStage.DatabentoInterop
                    ? diagnosticFeeds.Sum(value => (long)Math.Min(value.RingUsed, (ulong)long.MaxValue))
                    : stage == MarketDataOperationStage.DatabentoAggregation ? diagnosticFeeds.Sum(value => (long)value.ChannelBatchCount) : 0;
                var capacity = stage == MarketDataOperationStage.DatabentoInterop
                    ? diagnosticFeeds.Sum(value => (long)Math.Min(value.RingCapacity, (ulong)long.MaxValue))
                    : stage == MarketDataOperationStage.DatabentoAggregation ? diagnosticFeeds.Sum(value => (long)value.ChannelBatchCapacity) : 0;
                overlays[stage] = Gauge(stage, status, "WorkerDiagnostics", "Native and managed progress from dataset workers.",
                    diagnosticFeeds.Length == 0 ? now : diagnosticFeeds.Min(value => value.ObservedOnUtc), active, pending, capacity);
            }
        }
        var pendingAge = outlook.OldestPendingUtc is { } oldest ? MaxAge(now - oldest) : TimeSpan.Zero;
        var outlookStatus = !active ? "Inactive" : !outlook.IsProcessorReady ? "Red"
            : outlook.PendingCount > 0 && pendingAge > TimeSpan.FromMinutes(1) ? "Red"
            : outlook.PendingCount > 0 && pendingAge > TimeSpan.FromSeconds(5) ? "Yellow" : "Green";
        foreach (var stage in new[] { MarketDataOperationStage.MarketOutlookChannel, MarketDataOperationStage.MarketOutlookComposition })
            overlays[stage] = Gauge(stage, outlookStatus,
                !outlook.IsProcessorReady ? "ProcessorUnavailable" : outlookStatus is "Red" or "Yellow" ? "PendingWorkAged" : "CurrentProgress",
                !outlook.IsProcessorReady ? "Market Outlook processor is not ready." : "Local Market Outlook pending work and processor readiness.",
                now, active, outlook.PendingCount, 0) with { OldestPendingAge = pendingAge };
        if (publisher is { PolicyEnabled: true })
        {
            var status = !active && !publisher.Running ? "Inactive" : publisher.Faulted || !publisher.Running ? "Red"
                : publisher.Depth >= publisher.Capacity ? "Yellow" : "Green";
            overlays[MarketDataOperationStage.DatabentoRealtimePublication] = Gauge(
                MarketDataOperationStage.DatabentoRealtimePublication, status, publisher.Failure.ToString(),
                publisher.FailureDetail, now, active, publisher.Depth, publisher.Capacity) with
            {
                Received = publisher.Accepted, Completed = publisher.Published,
                Failed = publisher.Failed + publisher.Rejected + publisher.Expired,
                Saturated = publisher.SaturationCount, OldestPendingAge = publisher.OldestQueuedAge
            };
        }
        Volatile.Write(ref runtimeProjection, new(now, session.State.ToString(), session.ActiveValueDate, lastProbe, nextProbe,
            Array.AsReadOnly(datasets), new ReadOnlyDictionary<MarketDataOperationStage, MarketDataOperationStageReadModel>(overlays)));
        Interlocked.Increment(ref revision);
    }

    public MarketDataOperationsHealthReadModel GetReadModel()
    {
        var snapshot = GetSnapshot();
        var runtime = Volatile.Read(ref runtimeProjection);
        var stages = snapshot.Stages.Values.Select(value =>
        {
            var result = new MarketDataOperationStageReadModel
            {
                Stage = value.Stage.ToString(), Status = value.Status.ToString(), Reason = value.Reason,
                ReasonCode = value.Received == 0 ? "NotObserved" : value.Status == MarketDataOperationsStatus.Green ? "CurrentProgress" : "ProgressUnconfirmed",
                Received = value.Received, Completed = value.Completed, Failed = value.Failed, Coalesced = value.Coalesced,
                LastObservedUtc = value.LastObservedUtc, LastSucceededUtc = value.LastSucceededUtc,
                LastFailedUtc = value.LastFailedUtc, MarketDataAsOfUtc = value.MarketDataAsOfUtc,
                AverageLatency = value.AverageLatency, MaximumLatency = value.MaximumLatency,
                P50Latency = Percentile(value.Stage, .50), P95Latency = Percentile(value.Stage, .95), P99Latency = Percentile(value.Stage, .99)
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
                    Pending = overlay.Pending, Capacity = overlay.Capacity,
                    HighWater = overlay.HighWater, Saturated = overlay.Saturated, OldestPendingAge = overlay.OldestPendingAge,
                    LastObservedUtc = overlay.LastObservedUtc,
                    Received = Math.Max(result.Received, overlay.Received), Completed = Math.Max(result.Completed, overlay.Completed),
                    Failed = Math.Max(result.Failed, overlay.Failed)
                };
            }
            if (runtime?.Session == "Closed") result = result with { Status = "Inactive", Required = false, ReasonCode = "SessionClosed", Reason = "Planned session closure; historical counters retained." };
            return result;
        }).ToArray();
        var overall = stages.Select(value => value.Status).Concat(runtime?.Datasets.Select(value => value.Status) ?? [])
            .OrderByDescending(StatusRank).FirstOrDefault() ?? "Inactive";
        if (runtime is null) overall = "Orange";
        else if (snapshot.ObservedOnUtc - runtime.ObservedOnUtc > TimeSpan.FromSeconds(15)
                 && StatusRank(overall) < StatusRank("Orange")) overall = "Orange";
        return new()
        {
            Revision = snapshot.Revision, ObservedOnUtc = runtime?.ObservedOnUtc ?? snapshot.ObservedOnUtc,
            OverallStatus = overall, SessionState = runtime?.Session ?? "Unknown", ValueDate = runtime?.ValueDate,
            LastProbeUtc = runtime?.LastProbe, NextProbeUtc = runtime?.NextProbe,
            RejectedStaleGenerationPublications = snapshot.RejectedStaleGenerationPublications,
            Stages = Array.AsReadOnly(stages), Datasets = runtime?.Datasets ?? []
        };
    }

    static MarketDataOperationStageReadModel Gauge(MarketDataOperationStage stage, string status,
        string code, string reason, DateTime observed, bool required, long pending, long capacity) => new()
    {
        Stage = stage.ToString(), Status = status, ReasonCode = code, Reason = BoundReason(reason),
        LastObservedUtc = observed, Required = required, Pending = pending, Capacity = capacity
    };
    static int StatusRank(string value) => value switch { "Red" => 4, "Orange" => 3, "Yellow" => 2, "Green" => 1, _ => 0 };
    static TimeSpan MaxAge(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
    static string BoundReason(string value) => value.Length <= 4096 ? value : value[..4096];
}

/// <summary>Observes cached state, not native/worker commands. Independent of the watchdog and processor loops.</summary>
public sealed class MarketDataOperationsHealthObserver(
    MarketDataOperationsHealthService health, DatasetWorkerProcessRecoveryService workers,
    IMarketOutlookOperations outlook, IFuturesMarketSessionAuthority sessions,
    DatabentoMarketDataWatchdogService watchdog, DatabentoStage3Options options,
    ITickAggregationEventPublisher publisher, TimeProvider time,
    ILogger<MarketDataOperationsHealthObserver> logger) : BackgroundService
{
    public void ObserveOnce() => health.ObserveRuntime(sessions.Current, watchdog.Current, workers.Current,
        outlook.GetMetrics(), (publisher as ITickAggregationPublisherDiagnostics)?.GetSnapshot(), options);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { ObserveOnce(); }
            catch (Exception exception) { logger.LogWarning(exception, "Central market-data observation failed; last snapshot retained."); }
            await Task.Delay(TimeSpan.FromSeconds(5), time, stoppingToken).ConfigureAwait(false);
        }
    }
}
