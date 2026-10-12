using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

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
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.recorder = recorder;
        channel = Channel.CreateBounded<MarketOutlookUpdate>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public MarketOutlookUpdateSubmission Submit(MarketOutlookUpdate update)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
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
            MarketOutlookUpdateKind.Vwap => MarketDataOperationStage.VwapAnalytics,
            MarketOutlookUpdateKind.Adx => MarketDataOperationStage.AdxAnalytics,
            MarketOutlookUpdateKind.Atr => MarketDataOperationStage.AtrAnalytics,
            MarketOutlookUpdateKind.Macd => MarketDataOperationStage.MacdAnalytics,
            _ => null
        };
        if (analytic is { } analyticStage)
            SafeRecord(new(analyticStage, MarketDataOperationOutcome.Completed, update.Kind,
                update.UpdateId, now, MarketDataAsOfUtc: update.MarketDataAsOfUtc));
        var accepted = update with { QueueSequence = Interlocked.Increment(ref queueSequence),
            ActorGeneration = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.Current };
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
            // Values can become empty between a separate IsEmpty check and Min().
            // Enumerate one concurrent snapshot and make the empty case explicit.
            var ticks = pendingReceivedTicks.Values.DefaultIfEmpty().Min();
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A diagnostic recorder failure must not abort realtime ingestion or recovery; this boundary isolates the observer.")]
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
