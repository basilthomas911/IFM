using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public enum MarketDataOperationsStatus : byte
{
    Inactive = 0,
    Green = 1,
    Yellow = 2,
    Orange = 3,
    Red = 4
}

public sealed record MarketDataStageHealth
{
    public required MarketDataOperationStage Stage { get; init; }
    public required MarketDataOperationsStatus Status { get; init; }
    public long Received { get; init; }
    public long Completed { get; init; }
    public long Failed { get; init; }
    public long Coalesced { get; init; }
    public DateTime? LastObservedUtc { get; init; }
    public DateTime? LastSucceededUtc { get; init; }
    public DateTime? LastFailedUtc { get; init; }
    public DateTime? MarketDataAsOfUtc { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public TimeSpan MaximumLatency { get; init; }
    public Guid LastDiagnosticId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record MarketDataOperationsHealthSnapshot
{
    public required long Revision { get; init; }
    public required DateTime ObservedOnUtc { get; init; }
    public required MarketDataOperationsStatus OverallStatus { get; init; }
    public required IReadOnlyDictionary<MarketDataOperationStage, MarketDataStageHealth> Stages { get; init; }
    public required IReadOnlyDictionary<string, DatasetIncidentSnapshot> DatasetIncidents { get; init; }
    public long RejectedStaleGenerationPublications { get; init; }
}

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
    readonly object incidentsGate = new();
    readonly Dictionary<string, DatasetIncidentSnapshot> incidents = new(StringComparer.Ordinal);
    readonly DatasetWorkerAdmissionRegistry admissions;
    readonly TimeProvider time;
    long revision;

    public MarketDataOperationsHealthService(DatasetWorkerAdmissionRegistry admissions, TimeProvider? timeProvider = null)
    {
        this.admissions = admissions ?? throw new ArgumentNullException(nameof(admissions));
        time = timeProvider ?? TimeProvider.System;
    }

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
        var overall = incidentCopy.Values.Any(value => value.ProcessReplacementLatched)
            || activeStages.Any(value => value.Status == MarketDataOperationsStatus.Red)
                ? MarketDataOperationsStatus.Red
                : incidentCopy.Values.Any(value => value.IsOpen)
                  || activeStages.Any(value => value.Status == MarketDataOperationsStatus.Orange)
                    ? MarketDataOperationsStatus.Orange
                    : activeStages.Any(value => value.Status == MarketDataOperationsStatus.Yellow)
                        ? MarketDataOperationsStatus.Yellow
                        : activeStages.Length == 0
                            ? MarketDataOperationsStatus.Inactive
                            : MarketDataOperationsStatus.Green;
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
        var status = received == 0
            ? MarketDataOperationsStatus.Inactive
            : lastFailed > lastSucceeded && completed == 0
                ? MarketDataOperationsStatus.Red
                : lastFailed > lastSucceeded
                    ? MarketDataOperationsStatus.Yellow
                    : completed == 0
                        ? MarketDataOperationsStatus.Yellow
                        : MarketDataOperationsStatus.Green;
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
