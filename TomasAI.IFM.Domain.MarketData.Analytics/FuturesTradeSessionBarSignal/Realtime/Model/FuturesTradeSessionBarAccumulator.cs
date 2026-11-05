using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>
/// Aggregates live normalized trades into session-aligned OHLCV bars without owning durable actor state.
/// </summary>
public sealed class FuturesTradeSessionBarAccumulator(
    IMarketSessionCalendar calendar,
    IFuturesTradeSessionBarSeriesResolver seriesResolver,
    TimeProvider timeProvider)
{
    internal static readonly TimeSpan RecoveryWindow = TimeSpan.FromSeconds(2);
    const int MaximumPendingTrades = 4096;
    private static readonly TimeFrameType[] TimeFrames =
        [.. FuturesIntradaySignalActivationProfile.TimeFrames, TimeFrameType.Daily];
    private readonly Dictionary<BucketKey, ObservationBucket> buckets = [];
    private readonly Dictionary<string, TradeStream> streams = new(StringComparer.Ordinal);
    private readonly Dictionary<BucketKey, DateTimeOffset> finalizedThrough = [];
    private readonly Dictionary<MarketSeriesIdentity, string> contracts = [];
    private readonly IMarketSessionCalendar calendar =
        calendar ?? throw new ArgumentNullException(nameof(calendar));
    private readonly IFuturesTradeSessionBarSeriesResolver seriesResolver =
        seriesResolver ?? throw new ArgumentNullException(nameof(seriesResolver));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>Accepts one exact normalized trade and returns every interval closed by it.</summary>
    public IReadOnlyList<FuturesTradeSessionBarReadModel> Accept(
        FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Trade
            || source.Price.Trade is not { } trade
            || trade.StreamEpochId == Guid.Empty
            || trade.TradeOrdinal <= 0)
        {
            return [];
        }

        var contractId = source.Price.ContractId;
        var seriesIdentity = seriesResolver.Resolve(contractId);
        if (contracts.TryGetValue(seriesIdentity, out var priorContract)
            && !string.Equals(priorContract, contractId, StringComparison.Ordinal))
        {
            RemoveSeries(seriesIdentity);
            streams.Remove(priorContract);
        }
        contracts[seriesIdentity] = contractId;

        if (!streams.TryGetValue(contractId, out var stream))
        {
            stream = new TradeStream(seriesIdentity);
            streams.Add(contractId, stream);
        }
        if (stream.EpochId != trade.StreamEpochId)
        {
            if (stream.RetiredEpochs.Contains(trade.StreamEpochId)) return [];
            if (stream.EpochId != Guid.Empty)
            {
                stream.RetiredEpochs.Add(stream.EpochId);
                RemoveSeries(seriesIdentity);
                contracts[seriesIdentity] = contractId;
            }
            stream.EpochId = trade.StreamEpochId;
            stream.Ordinal = trade.TradeOrdinal - 1;
            stream.Pending.Clear();
            stream.GapDeadline = null;
        }
        if (trade.TradeOrdinal <= stream.Ordinal) return [];

        var closed = new List<FuturesTradeSessionBarReadModel>();
        if (stream.Pending.Count == 0 && trade.TradeOrdinal == stream.Ordinal + 1)
        {
            Apply(contractId, stream, trade, closed);
            return closed;
        }
        if (!stream.Pending.TryAdd(trade.TradeOrdinal, trade)) return [];
        Drain(contractId, stream, timeProvider.GetUtcNow(), closed);
        return closed;
    }

    void Apply(string contractId, TradeStream stream, FuturesMarketTradeSnapshot trade,
        List<FuturesTradeSessionBarReadModel> closed)
    {
        stream.Ordinal = trade.TradeOrdinal;
        var timestamp = trade.EventTimestamp.ToUniversalTime();
        if (timestamp > stream.LatestTradeUtc) stream.LatestTradeUtc = timestamp;
        if (trade.LastPrice <= 0 || trade.LastSize == 0
            || trade.NormalizedTradeAction != NormalizedTradeAction.New) return;
        var valueDate = calendar.GetValueDate(timestamp);
        if (!calendar.IsTradingDate(valueDate)) return;
        var session = calendar.GetSession(valueDate);
        if (timestamp < session.StartUtc || timestamp >= session.EndUtc) return;

        foreach (var timeFrame in TimeFrames)
        {
            var interval = GetInterval(timeFrame, timestamp, session);
            var key = new BucketKey(stream.Series, timeFrame);
            if (interval.StartUtc <= stream.IncompleteThrough
                || (finalizedThrough.TryGetValue(key, out var endUtc) && interval.StartUtc < endUtc))
                continue;
            if (buckets.TryGetValue(key, out var bucket))
            {
                if (timestamp < bucket.StartUtc) continue;
                if (timestamp >= bucket.EndUtc)
                {
                    closed.Add(bucket.Close(timeProvider));
                    finalizedThrough[key] = bucket.EndUtc;
                    buckets.Remove(key);
                }
            }
            if (!buckets.TryGetValue(key, out bucket))
            {
                bucket = new ObservationBucket(
                    stream.Series, contractId, valueDate, timeFrame, interval.StartUtc, interval.EndUtc,
                    trade.StreamEpochId);
                buckets.Add(key, bucket);
            }
            bucket.Add(trade);
        }
    }

    void Drain(string contractId, TradeStream stream, DateTimeOffset now,
        List<FuturesTradeSessionBarReadModel> closed)
    {
        while (stream.Pending.Count != 0)
        {
            var next = stream.Pending.First();
            if (next.Key != stream.Ordinal + 1)
            {
                stream.GapDeadline ??= now + RecoveryWindow;
                if (now < stream.GapDeadline && stream.Pending.Count < MaximumPendingTrades) return;

                // A missing trade can belong to either side of an interval boundary.
                // Suppress the affected intervals and resume within the same generation.
                RemoveSeries(stream.Series);
                contracts[stream.Series] = contractId;
                var nextTime = next.Value.EventTimestamp.ToUniversalTime();
                var gapEnd = nextTime > stream.LatestTradeUtc ? nextTime : stream.LatestTradeUtc;
                if (gapEnd > stream.IncompleteThrough) stream.IncompleteThrough = gapEnd;
            }
            stream.Pending.Remove(next.Key);
            Apply(contractId, stream, next.Value, closed);
            stream.GapDeadline = null;
        }
    }

    /// <summary>Expires gaps and closes elapsed intervals after allowing for late delivery.</summary>
    public IReadOnlyList<FuturesTradeSessionBarReadModel> CloseThrough(
        DateTimeOffset barrierUtc)
    {
        var closed = new List<FuturesTradeSessionBarReadModel>();
        foreach (var (contract, stream) in streams)
            Drain(contract, stream, barrierUtc, closed);
        foreach (var (key, bucket) in buckets.ToArray())
        {
            if (bucket.EndUtc + RecoveryWindow > barrierUtc
                || (contracts.TryGetValue(key.SeriesIdentity, out var contract)
                    && streams[contract].Pending.Count != 0)) continue;
            closed.Add(bucket.Close(timeProvider));
            finalizedThrough[key] = bucket.EndUtc;
            buckets.Remove(key);
        }
        return closed;
    }

    private void RemoveSeries(MarketSeriesIdentity identity)
    {
        foreach (var key in buckets.Keys.Where(x => x.SeriesIdentity == identity).ToArray())
            buckets.Remove(key);
        contracts.Remove(identity);
    }

    private static Interval GetInterval(
        TimeFrameType timeFrame,
        DateTimeOffset timestamp,
        MarketSessionBounds session)
    {
        if (timeFrame == TimeFrameType.Daily) return new(session.StartUtc, session.EndUtc);
        var duration = timeFrame switch
        {
            TimeFrameType.FifteenSeconds => TimeSpan.FromSeconds(15),
            TimeFrameType.OneMinute => TimeSpan.FromMinutes(1),
            TimeFrameType.FiveMinutes => TimeSpan.FromMinutes(5),
            TimeFrameType.FifteenMinutes => TimeSpan.FromMinutes(15),
            TimeFrameType.OneHour => TimeSpan.FromHours(1),
            TimeFrameType.FourHours => TimeSpan.FromHours(4),
            _ => throw new ArgumentOutOfRangeException(nameof(timeFrame))
        };
        var elapsedTicks = (timestamp - session.StartUtc).Ticks;
        var start = session.StartUtc.AddTicks(elapsedTicks / duration.Ticks * duration.Ticks);
        var end = start + duration;
        if (end > session.EndUtc) end = session.EndUtc;
        return new(start, end);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct BucketKey(MarketSeriesIdentity SeriesIdentity, TimeFrameType TimeFrame);
    sealed class TradeStream(MarketSeriesIdentity series)
    {
        internal MarketSeriesIdentity Series { get; } = series;
        internal Guid EpochId { get; set; }
        internal long Ordinal { get; set; }
        internal DateTimeOffset LatestTradeUtc { get; set; }
        internal DateTimeOffset IncompleteThrough { get; set; }
        internal DateTimeOffset? GapDeadline { get; set; }
        internal SortedDictionary<long, FuturesMarketTradeSnapshot> Pending { get; } = [];
        internal HashSet<Guid> RetiredEpochs { get; } = [];
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct Interval(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

    sealed class ObservationBucket(
        MarketSeriesIdentity seriesIdentity,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timeFrame,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        Guid streamEpochId)
    {
        private decimal open;
        private decimal high = decimal.MinValue;
        private decimal low = decimal.MaxValue;
        private decimal close;
        private decimal volume;
        private long tradeCount;
        private decimal priceVolumeSum;
        private long firstSequence = long.MaxValue;
        private long lastSequence;
        private DateTimeOffset firstEvent = DateTimeOffset.MaxValue;
        private DateTimeOffset lastEvent = DateTimeOffset.MinValue;

        internal DateTimeOffset StartUtc => startUtc;
        internal DateTimeOffset EndUtc => endUtc;

        internal void Add(FuturesMarketTradeSnapshot trade)
        {
            if (tradeCount == 0) open = trade.LastPrice;
            high = Math.Max(high, trade.LastPrice);
            low = Math.Min(low, trade.LastPrice);
            close = trade.LastPrice;
            volume += trade.LastSize;
            tradeCount++;
            priceVolumeSum += trade.LastPrice * trade.LastSize;
            firstSequence = Math.Min(firstSequence, trade.SourceSequence);
            lastSequence = Math.Max(lastSequence, trade.SourceSequence);
            var marketEventUtc = trade.EventTimestamp.ToUniversalTime();
            firstEvent = firstEvent < marketEventUtc ? firstEvent : marketEventUtc;
            lastEvent = lastEvent > marketEventUtc ? lastEvent : marketEventUtc;
        }

        internal FuturesTradeSessionBarReadModel Close(TimeProvider timeProvider)
        {
            var observationId = FuturesTradeSessionBarId.Create(
                seriesIdentity, timeFrame, endUtc, lastSequence);
            return new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = seriesIdentity,
                ObservationId = observationId,
                ContractId = contractId,
                ValueDate = valueDate,
                TimeFrame = timeFrame,
                IntervalStartUtc = startUtc,
                IntervalEndUtc = endUtc,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                TradeCount = tradeCount,
                PriceVolumeSum = priceVolumeSum,
                FirstSourceSequence = firstSequence,
                LastSourceSequence = lastSequence,
                FirstMarketEventUtc = firstEvent,
                LastMarketEventUtc = lastEvent,
                CalculatedAtUtc = timeProvider.GetUtcNow(),
                SchemaVersion = 2,
                CalculationVersion = "trade-session-bar-v1",
                IsComplete = true,
                IsValid = true,
                ValidationIssues = [],
                CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
                StreamEpochId = streamEpochId
            };
        }
    }
}
