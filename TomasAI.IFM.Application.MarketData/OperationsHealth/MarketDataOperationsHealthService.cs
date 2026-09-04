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
public sealed class MarketDataOperationsHealthService : IMarketDataOperationsRecorder
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
    }

    readonly Cell[] cells = Enum.GetValues<MarketDataOperationStage>().Select(_ => new Cell()).ToArray();
    readonly object incidentsGate = new();
    readonly Dictionary<string, DatasetIncidentSnapshot> incidents = new(StringComparer.Ordinal);
    readonly DatasetWorkerAdmissionRegistry admissions;
    long revision;

    public MarketDataOperationsHealthService(DatasetWorkerAdmissionRegistry admissions)
        => this.admissions = admissions ?? throw new ArgumentNullException(nameof(admissions));

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
                     or MarketDataOperationOutcome.Applied)
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
        lock (incidentsGate)
            incidents[incident.Dataset] = incident;
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
            ObservedOnUtc = DateTime.UtcNow,
            OverallStatus = overall,
            Stages = stages,
            DatasetIncidents = incidentCopy,
            RejectedStaleGenerationPublications = admissions.RejectedPublications
        };
    }

    static MarketDataStageHealth Snapshot(MarketDataOperationStage stage, Cell cell)
    {
        var received = Interlocked.Read(ref cell.Received);
        var completed = Interlocked.Read(ref cell.Completed);
        var failed = Interlocked.Read(ref cell.Failed);
        var coalesced = Interlocked.Read(ref cell.Coalesced);
        var status = received == 0
            ? MarketDataOperationsStatus.Inactive
            : failed > 0 && completed == 0
                ? MarketDataOperationsStatus.Red
                : failed > 0
                    ? MarketDataOperationsStatus.Yellow
                    : coalesced > 0
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
                MarketDataOperationsStatus.Yellow when failed > 0 => "The stage has recorded one or more failures.",
                MarketDataOperationsStatus.Yellow => "The stage has coalesced work under load.",
                MarketDataOperationsStatus.Green => "The stage is recording successful progress.",
                _ => "The stage has no current-process observations."
            }
        };
    }

    static void RecordLatency(Cell cell, TimeSpan latency)
    {
        var ticks = Math.Max(0, latency.Ticks);
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

    static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value : value.ToUniversalTime();

    static DateTime? ReadUtc(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTime(value, DateTimeKind.Utc);
    }
}
