using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>Stable, bounded metric identity for one local Market Outlook input.</summary>
public enum MarketOutlookUpdateKind : byte
{
    Rsi,
    Tdi,
    Iti,
    Ema,
    BollingerBand,
    EsTrade,
    VixPrice,
    Eod,
    TradeSignal,
    FeedHealth,
    HistoricalWarmup,
    Hydration,
    Recompose
}

/// <summary>
/// Base contract for an in-process Market Outlook update. This contract deliberately implements no
/// actor-message interface and is never serialized through NATS.
/// </summary>
public abstract record MarketOutlookUpdate
{
    internal long QueueSequence { get; init; }
    public required Guid UpdateId { get; init; }
    public abstract MarketOutlookUpdateKind Kind { get; }
    public required MarketOutlookEntityId EntityId { get; init; }
    public required DateTime ReceivedAtUtc { get; init; }
    public required DateTime MarketDataAsOfUtc { get; init; }
    public Guid CommandId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = string.Empty;
    public long SourceSequence { get; init; }
    public Guid StreamEpochId { get; init; }
    public long StreamOrdinal { get; init; }
}

public enum MarketOutlookUpdateSubmission : byte
{
    Enqueued,
    Coalesced
}

public interface IMarketOutlookUpdateWriter
{
    MarketOutlookUpdateSubmission Submit(MarketOutlookUpdate update);
}

/// <summary>Reader capability reserved for the sole Market Outlook processor.</summary>
public interface IMarketOutlookUpdateReader
{
    IAsyncEnumerable<MarketOutlookUpdate> ReadAllAsync(CancellationToken cancellationToken);
    int PendingCount { get; }
    DateTime? OldestPendingUtc { get; }
}

public interface IMarketOutlookOperations
{
    MarketOutlookProcessorMetricsSnapshot GetMetrics();
    bool RequestRecompose(MarketOutlookEntityId entityId);
    ValueTask<bool> WaitForIdleAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

public enum MarketDataOperationStage : byte
{
    MarketOutlookChannel,
    MarketOutlookComposition,
    MarketOutlookCache,
    MarketOutlookPublication,
    DatabentoNative,
    DatabentoInterop,
    DatabentoAggregation,
    DatabentoLifecycle,
    DatabentoRefresh,
    DatabentoWorkerProcess,
    DatabentoGenerationIngress,
    RsiAnalytics,
    TdiAnalytics,
    ItiAnalytics,
    EmaAnalytics,
    BollingerBandAnalytics,
    VixAnalytics,
    EodAnalytics,
    FuturesTradeSignal,
    UiDelivery,
    DatabentoRealtimePublication
}

public enum MarketDataOperationOutcome : byte
{
    Received,
    Enqueued,
    Applied,
    Changed,
    Composed,
    Published,
    Failed,
    Coalesced,
    Requested,
    Started,
    Completed
}

public readonly record struct MarketDataOperationMeasurement(
    MarketDataOperationStage Stage,
    MarketDataOperationOutcome Outcome,
    MarketOutlookUpdateKind UpdateKind,
    Guid UpdateId,
    DateTime OccurredAtUtc,
    TimeSpan? Latency = null,
    DateTime? MarketDataAsOfUtc = null);

/// <summary>
/// Minimal cross-stage recording boundary. Stage 3 replaces the Stage 1 collector with the complete
/// central operational-health implementation without reopening producers.
/// </summary>
public interface IMarketDataOperationsRecorder
{
    void Record(in MarketDataOperationMeasurement measurement);
}

public sealed record MarketOutlookUpdateMetricSnapshot
{
    public required MarketOutlookUpdateKind Kind { get; init; }
    public long Received { get; init; }
    public long Enqueued { get; init; }
    public long Applied { get; init; }
    public long Changed { get; init; }
    public long Composed { get; init; }
    public long Published { get; init; }
    public long Failed { get; init; }
    public long Coalesced { get; init; }
    public Guid LastUpdateId { get; init; }
    public DateTime? LastReceivedUtc { get; init; }
    public DateTime? LastAppliedUtc { get; init; }
    public DateTime? LastPublishedUtc { get; init; }
    public DateTime? LastMarketDataAsOfUtc { get; init; }
    public TimeSpan AverageQueueLatency { get; init; }
    public TimeSpan MaximumQueueLatency { get; init; }
    public TimeSpan AverageProcessingLatency { get; init; }
    public TimeSpan MaximumProcessingLatency { get; init; }
    public TimeSpan AveragePublicationLatency { get; init; }
    public TimeSpan MaximumPublicationLatency { get; init; }
}

public sealed record MarketOutlookProcessorMetricsSnapshot
{
    public required IReadOnlyDictionary<MarketOutlookUpdateKind, MarketOutlookUpdateMetricSnapshot> Updates { get; init; }
    public int PendingCount { get; init; }
    public DateTime? OldestPendingUtc { get; init; }
    public bool IsProcessorReady { get; init; }
}

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

    public MarketOutlookProcessorMetricsSnapshot GetSnapshot(IMarketOutlookUpdateReader reader) => new()
    {
        Updates = Enum.GetValues<MarketOutlookUpdateKind>().ToDictionary(
            static kind => kind,
            kind => Snapshot(kind, cells[(int)kind])),
        PendingCount = reader.PendingCount,
        OldestPendingUtc = reader.OldestPendingUtc,
        IsProcessorReady = Volatile.Read(ref processorReady) == 1
    };

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

/// <summary>
/// Bounded MPSC channel. When the bounded lane is full, the latest pending value for an
/// entity/update-kind pair is retained in an explicitly measured overflow slot.
/// </summary>
public sealed class MarketOutlookUpdateChannel : IMarketOutlookUpdateWriter, IMarketOutlookUpdateReader
{
    const int MaximumChannelBatch = 64;
    readonly record struct PendingKey(MarketOutlookEntityId EntityId, MarketOutlookUpdateKind Kind);

    readonly Channel<MarketOutlookUpdate> channel;
    readonly ConcurrentDictionary<PendingKey, MarketOutlookUpdate> latestOverflow = new();
    readonly ConcurrentDictionary<long, long> pendingReceivedTicks = new();
    readonly IMarketDataOperationsRecorder recorder;
    long queueSequence;

    public MarketOutlookUpdateChannel(
        IMarketDataOperationsRecorder recorder,
        int capacity = 8_192)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        this.recorder = recorder;
        channel = Channel.CreateBounded<MarketOutlookUpdate>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public MarketOutlookUpdateSubmission Submit(MarketOutlookUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var now = DateTime.UtcNow;
        // Observe each analytic's output separately at the existing local ingress boundary.
        // This does not pretend to instrument its internal calculation or infer stalls from quiet inputs.
        MarketDataOperationStage? analytic = update.Kind switch
        {
            MarketOutlookUpdateKind.Rsi => MarketDataOperationStage.RsiAnalytics,
            MarketOutlookUpdateKind.Tdi => MarketDataOperationStage.TdiAnalytics,
            MarketOutlookUpdateKind.Iti => MarketDataOperationStage.ItiAnalytics,
            MarketOutlookUpdateKind.Ema => MarketDataOperationStage.EmaAnalytics,
            MarketOutlookUpdateKind.BollingerBand => MarketDataOperationStage.BollingerBandAnalytics,
            MarketOutlookUpdateKind.VixPrice => MarketDataOperationStage.VixAnalytics,
            MarketOutlookUpdateKind.Eod => MarketDataOperationStage.EodAnalytics,
            MarketOutlookUpdateKind.TradeSignal => MarketDataOperationStage.FuturesTradeSignal,
            _ => null
        };
        if (analytic is { } analyticStage)
            SafeRecord(new(analyticStage, MarketDataOperationOutcome.Completed, update.Kind,
                update.UpdateId, now, MarketDataAsOfUtc: update.MarketDataAsOfUtc));
        var accepted = update with { QueueSequence = Interlocked.Increment(ref queueSequence) };
        pendingReceivedTicks[accepted.QueueSequence] = UtcTicks(accepted.ReceivedAtUtc);
        SafeRecord(new(
            MarketDataOperationStage.MarketOutlookChannel,
            MarketDataOperationOutcome.Received,
            update.Kind,
            update.UpdateId,
            now,
            MarketDataAsOfUtc: update.MarketDataAsOfUtc));

        if (channel.Writer.TryWrite(accepted))
        {
            SafeRecord(new(
                MarketDataOperationStage.MarketOutlookChannel,
                MarketDataOperationOutcome.Enqueued,
                update.Kind,
                update.UpdateId,
                now,
                MarketDataAsOfUtc: update.MarketDataAsOfUtc));
            return MarketOutlookUpdateSubmission.Enqueued;
        }

        MarketOutlookUpdate? replaced = null;
        latestOverflow.AddOrUpdate(
            new(update.EntityId, update.Kind),
            accepted,
            (_, previous) =>
            {
                replaced = previous;
                return accepted;
            });
        if (replaced is not null)
            pendingReceivedTicks.TryRemove(replaced.QueueSequence, out _);
        SafeRecord(new(
            MarketDataOperationStage.MarketOutlookChannel,
            MarketDataOperationOutcome.Coalesced,
            update.Kind,
            update.UpdateId,
            now,
            MarketDataAsOfUtc: update.MarketDataAsOfUtc));
        return MarketOutlookUpdateSubmission.Coalesced;
    }

    public async IAsyncEnumerable<MarketOutlookUpdate> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var channelBatch = 0;
            while (channelBatch++ < MaximumChannelBatch && channel.Reader.TryRead(out var update))
            {
                try
                {
                    yield return update;
                }
                finally
                {
                    pendingReceivedTicks.TryRemove(update.QueueSequence, out _);
                }
            }

            foreach (var pair in latestOverflow.ToArray())
            {
                if (latestOverflow.TryRemove(pair.Key, out var update))
                {
                    try
                    {
                        yield return update;
                    }
                    finally
                    {
                        pendingReceivedTicks.TryRemove(update.QueueSequence, out _);
                    }
                }
            }
        }
    }

    public int PendingCount => pendingReceivedTicks.Count;

    public DateTime? OldestPendingUtc
    {
        get
        {
            var ticks = pendingReceivedTicks.IsEmpty ? 0 : pendingReceivedTicks.Values.Min();
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    void SafeRecord(in MarketDataOperationMeasurement measurement)
    {
        try
        {
            recorder.Record(measurement);
        }
        catch
        {
            // Operational telemetry cannot fail or block a producer submission.
        }
    }

    static long UtcTicks(DateTime value) =>
        (value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime()).Ticks;
}
