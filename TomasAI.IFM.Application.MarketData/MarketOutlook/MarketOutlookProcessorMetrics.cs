using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>Lock-free Stage 1 metrics collector keyed by the fixed update-kind enum.</summary>
public sealed class MarketOutlookProcessorMetrics : IMarketDataOperationsRecorder
{
    sealed class Cell
    {
        internal long Received;
        internal long Enqueued;
        internal long Applied;
        internal long Changed;
        internal long Composed;
        internal long Published;
        internal long Failed;
        internal long Coalesced;
        internal long LastReceivedTicks;
        internal long LastAppliedTicks;
        internal long LastPublishedTicks;
        internal long LastMarketDataAsOfTicks;
        internal object? LastUpdateId;
        internal readonly Latency Queue = new();
        internal readonly Latency Processing = new();
        internal readonly Latency Publication = new();
    }

    sealed class Latency
    {
        internal long Count;
        internal long TotalTicks;
        internal long MaximumTicks;
    }

    readonly Cell[] cells = Enum.GetValues<MarketOutlookUpdateKind>().Select(_ => new Cell()).ToArray();
    int processorReady;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A diagnostic recorder failure must not abort realtime ingestion or recovery; this boundary isolates the observer.")]
    public void Record(in MarketDataOperationMeasurement measurement)
    {
        try
        {
            var cell = cells[(int)measurement.UpdateKind];
            Volatile.Write(ref cell.LastUpdateId, measurement.UpdateId);
            var ticks = UtcTicks(measurement.OccurredAtUtc);
            if (measurement.MarketDataAsOfUtc is { } marketDataAsOfUtc)
                Interlocked.Exchange(ref cell.LastMarketDataAsOfTicks, UtcTicks(marketDataAsOfUtc));
            switch (measurement.Outcome)
            {
                case MarketDataOperationOutcome.Received:
                    Interlocked.Increment(ref cell.Received);
                    Interlocked.Exchange(ref cell.LastReceivedTicks, ticks);
                    break;
                case MarketDataOperationOutcome.Enqueued:
                    Interlocked.Increment(ref cell.Enqueued);
                    break;
                case MarketDataOperationOutcome.Applied:
                    Interlocked.Increment(ref cell.Applied);
                    Interlocked.Exchange(ref cell.LastAppliedTicks, ticks);
                    break;
                case MarketDataOperationOutcome.Changed:
                    Interlocked.Increment(ref cell.Changed);
                    break;
                case MarketDataOperationOutcome.Composed:
                    Interlocked.Increment(ref cell.Composed);
                    break;
                case MarketDataOperationOutcome.Published:
                    Interlocked.Increment(ref cell.Published);
                    Interlocked.Exchange(ref cell.LastPublishedTicks, ticks);
                    break;
                case MarketDataOperationOutcome.Failed:
                    Interlocked.Increment(ref cell.Failed);
                    break;
                case MarketDataOperationOutcome.Coalesced:
                    Interlocked.Increment(ref cell.Coalesced);
                    break;
            }

            if (measurement.Latency is { } latency)
            {
                var target = measurement.Stage switch
                {
                    MarketDataOperationStage.MarketOutlookChannel => cell.Queue,
                    MarketDataOperationStage.MarketOutlookPublication => cell.Publication,
                    _ => cell.Processing
                };
                RecordLatency(target, latency);
            }
        }
        catch
        {
            // Operational telemetry must never fail the market-data path.
        }
    }

    public void SetProcessorReady(bool value) =>
        Volatile.Write(ref processorReady, value ? 1 : 0);

    public MarketOutlookProcessorMetricsSnapshot GetSnapshot(IMarketOutlookUpdateReader reader) 
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new()
    {
        Updates = Enum.GetValues<MarketOutlookUpdateKind>().ToDictionary(
            static kind => kind,
            kind => Snapshot(kind, cells[(int)kind])),
        PendingCount = reader.PendingCount,
        OldestPendingUtc = reader.OldestPendingUtc,
        IsProcessorReady = Volatile.Read(ref processorReady) == 1
    };
    }

    public void Clear()
    {
        foreach (var cell in cells)
        {
            Interlocked.Exchange(ref cell.Received, 0);
            Interlocked.Exchange(ref cell.Enqueued, 0);
            Interlocked.Exchange(ref cell.Applied, 0);
            Interlocked.Exchange(ref cell.Changed, 0);
            Interlocked.Exchange(ref cell.Composed, 0);
            Interlocked.Exchange(ref cell.Published, 0);
            Interlocked.Exchange(ref cell.Failed, 0);
            Interlocked.Exchange(ref cell.Coalesced, 0);
            Interlocked.Exchange(ref cell.LastReceivedTicks, 0);
            Interlocked.Exchange(ref cell.LastAppliedTicks, 0);
            Interlocked.Exchange(ref cell.LastPublishedTicks, 0);
            Interlocked.Exchange(ref cell.LastMarketDataAsOfTicks, 0);
            Volatile.Write(ref cell.LastUpdateId, null);
            Clear(cell.Queue);
            Clear(cell.Processing);
            Clear(cell.Publication);
        }
    }

    static MarketOutlookUpdateMetricSnapshot Snapshot(MarketOutlookUpdateKind kind, Cell cell) => new()
    {
        Kind = kind,
        Received = Interlocked.Read(ref cell.Received),
        Enqueued = Interlocked.Read(ref cell.Enqueued),
        Applied = Interlocked.Read(ref cell.Applied),
        Changed = Interlocked.Read(ref cell.Changed),
        Composed = Interlocked.Read(ref cell.Composed),
        Published = Interlocked.Read(ref cell.Published),
        Failed = Interlocked.Read(ref cell.Failed),
        Coalesced = Interlocked.Read(ref cell.Coalesced),
        LastUpdateId = Volatile.Read(ref cell.LastUpdateId) is Guid updateId ? updateId : Guid.Empty,
        LastReceivedUtc = ReadUtc(ref cell.LastReceivedTicks),
        LastAppliedUtc = ReadUtc(ref cell.LastAppliedTicks),
        LastPublishedUtc = ReadUtc(ref cell.LastPublishedTicks),
        LastMarketDataAsOfUtc = ReadUtc(ref cell.LastMarketDataAsOfTicks),
        AverageQueueLatency = Average(cell.Queue),
        MaximumQueueLatency = TimeSpan.FromTicks(Interlocked.Read(ref cell.Queue.MaximumTicks)),
        AverageProcessingLatency = Average(cell.Processing),
        MaximumProcessingLatency = TimeSpan.FromTicks(Interlocked.Read(ref cell.Processing.MaximumTicks)),
        AveragePublicationLatency = Average(cell.Publication),
        MaximumPublicationLatency = TimeSpan.FromTicks(Interlocked.Read(ref cell.Publication.MaximumTicks))
    };

    static void RecordLatency(Latency target, TimeSpan latency)
    {
        var ticks = Math.Max(0, latency.Ticks);
        Interlocked.Increment(ref target.Count);
        Interlocked.Add(ref target.TotalTicks, ticks);
        var current = Interlocked.Read(ref target.MaximumTicks);
        while (ticks > current)
        {
            var observed = Interlocked.CompareExchange(ref target.MaximumTicks, ticks, current);
            if (observed == current)
                break;
            current = observed;
        }
    }

    static TimeSpan Average(Latency value)
    {
        var count = Interlocked.Read(ref value.Count);
        return count == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(Interlocked.Read(ref value.TotalTicks) / count);
    }

    static void Clear(Latency value)
    {
        Interlocked.Exchange(ref value.Count, 0);
        Interlocked.Exchange(ref value.TotalTicks, 0);
        Interlocked.Exchange(ref value.MaximumTicks, 0);
    }

    static long UtcTicks(DateTime value) =>
        (value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime()).Ticks;

    static DateTime? ReadUtc(ref long value)
    {
        var ticks = Interlocked.Read(ref value);
        return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }
}
