using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoStageMetric(long Requested, long Started, long Completed, long Failed, DateTime? LastActivityUtc);
public sealed record DatabentoWatchdogMetricsSnapshot(IReadOnlyDictionary<MarketDataOperationStage, DatabentoStageMetric> Stages);

/// <summary>Bounded, non-throwing Stage 2 counters exposed as an immutable local snapshot.</summary>
public sealed class DatabentoWatchdogMetrics : IMarketDataOperationsRecorder
{
    sealed class Cell { internal long Requested, Started, Completed, Failed, LastTicks; }
    readonly Cell[] _cells = Enum.GetValues<MarketDataOperationStage>().Select(_ => new Cell()).ToArray();

    public void Record(in MarketDataOperationMeasurement measurement)
    {
        if (measurement.Stage < MarketDataOperationStage.DatabentoNative) return;
        try
        {
            var cell = _cells[(int)measurement.Stage];
            switch (measurement.Outcome)
            {
                case MarketDataOperationOutcome.Requested: Interlocked.Increment(ref cell.Requested); break;
                case MarketDataOperationOutcome.Started: Interlocked.Increment(ref cell.Started); break;
                case MarketDataOperationOutcome.Completed: Interlocked.Increment(ref cell.Completed); break;
                case MarketDataOperationOutcome.Failed: Interlocked.Increment(ref cell.Failed); break;
            }
            Interlocked.Exchange(ref cell.LastTicks, measurement.OccurredAtUtc.Ticks);
        }
        catch { }
    }

    public DatabentoWatchdogMetricsSnapshot Snapshot() => new(
        Enum.GetValues<MarketDataOperationStage>()
            .Where(stage => stage >= MarketDataOperationStage.DatabentoNative)
            .ToDictionary(stage => stage, stage =>
            {
                var cell = _cells[(int)stage];
                var ticks = Interlocked.Read(ref cell.LastTicks);
                return new DatabentoStageMetric(Interlocked.Read(ref cell.Requested),
                    Interlocked.Read(ref cell.Started), Interlocked.Read(ref cell.Completed),
                    Interlocked.Read(ref cell.Failed), ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc));
            }));
}

public sealed class CompositeMarketDataOperationsRecorder(params IMarketDataOperationsRecorder[] recorders)
    : IMarketDataOperationsRecorder
{
    public void Record(in MarketDataOperationMeasurement measurement)
    {
        foreach (var recorder in recorders)
            try { recorder.Record(measurement); } catch { }
    }
}
