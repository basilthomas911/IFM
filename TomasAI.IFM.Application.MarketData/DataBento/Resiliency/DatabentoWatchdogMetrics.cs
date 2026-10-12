using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Bounded, non-throwing Stage 2 counters exposed as an immutable local snapshot.</summary>
public sealed class DatabentoWatchdogMetrics : IMarketDataOperationsRecorder
{
    sealed class Cell { internal long Requested, Started, Completed, Failed, LastTicks; }
    readonly Cell[] _cells = Enum.GetValues<MarketDataOperationStage>().Select(_ => new Cell()).ToArray();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A diagnostic recorder failure must not abort realtime ingestion or recovery; this boundary isolates the observer.")]
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
        catch { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
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
