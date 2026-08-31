using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>Resolves a routed contract to its explicit contract or continuation-series identity.</summary>
public interface IFuturesTradeSessionBarSeriesResolver
{
    /// <summary>Resolves the configured market series for one live contract.</summary>
    MarketSeriesIdentity Resolve(string contractId);
}

/// <summary>Uses exact contracts when no continuation mapping is configured.</summary>
public sealed class ContractFuturesTradeSessionBarSeriesResolver : IFuturesTradeSessionBarSeriesResolver
{
    /// <inheritdoc />
    public MarketSeriesIdentity Resolve(string contractId) => MarketSeriesIdentity.ForContract(contractId);
}

/// <summary>Maps configured contract-root prefixes to roll-aware continuation identities.</summary>
public sealed class PrefixFuturesTradeSessionBarSeriesResolver(
    IReadOnlyDictionary<string, MarketSeriesIdentity> mappings)
    : IFuturesTradeSessionBarSeriesResolver
{
    readonly KeyValuePair<string, MarketSeriesIdentity>[] orderedMappings =
        [.. mappings.OrderByDescending(x => x.Key.Length)];

    /// <inheritdoc />
    public MarketSeriesIdentity Resolve(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        foreach (var mapping in orderedMappings)
            if (contractId.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
                return mapping.Value;
        return MarketSeriesIdentity.ForContract(contractId);
    }
}

/// <summary>
/// Aggregates live normalized trades into session-aligned OHLCV bars without owning durable actor state.
/// </summary>
public sealed class FuturesTradeSessionBarAccumulator(
    IMarketSessionCalendar calendar,
    IFuturesTradeSessionBarSeriesResolver seriesResolver,
    TimeProvider timeProvider)
{
    private static readonly TimeFrameType[] TimeFrames =
        [.. FuturesIntradaySignalActivationProfile.TimeFrames, TimeFrameType.Daily];
    private readonly Dictionary<BucketKey, ObservationBucket> buckets = [];
    private readonly Dictionary<string, StreamPosition> positions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> invalidEpochs = new(StringComparer.Ordinal);
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
            || trade.TradeOrdinal <= 0
            || trade.LastPrice <= 0
            || trade.LastSize == 0
            || trade.NormalizedTradeAction != NormalizedTradeAction.New)
        {
            return [];
        }

        var contractId = source.Price.ContractId;
        var seriesIdentity = seriesResolver.Resolve(contractId);
        if (invalidEpochs.TryGetValue(contractId, out var invalidEpoch))
        {
            if (invalidEpoch == trade.StreamEpochId)
            {
                positions[contractId] = new(trade.StreamEpochId, trade.TradeOrdinal);
                return [];
            }
            invalidEpochs.Remove(contractId);
        }
        if (positions.TryGetValue(contractId, out var position))
        {
            if (position.EpochId == trade.StreamEpochId && trade.TradeOrdinal <= position.Ordinal)
                return [];
            if (position.EpochId == trade.StreamEpochId && trade.TradeOrdinal != position.Ordinal + 1)
            {
                RemoveSeries(seriesIdentity);
                positions[contractId] = new(trade.StreamEpochId, trade.TradeOrdinal);
                invalidEpochs[contractId] = trade.StreamEpochId;
                return [];
            }
            if (position.EpochId != trade.StreamEpochId) RemoveSeries(seriesIdentity);
        }
        positions[contractId] = new(trade.StreamEpochId, trade.TradeOrdinal);

        if (contracts.TryGetValue(seriesIdentity, out var priorContract)
            && !string.Equals(priorContract, contractId, StringComparison.Ordinal))
        {
            RemoveSeries(seriesIdentity);
        }
        contracts[seriesIdentity] = contractId;

        var timestamp = trade.EventTimestamp.ToUniversalTime();
        var valueDate = calendar.GetValueDate(timestamp);
        if (!calendar.IsTradingDate(valueDate)) return [];
        var session = calendar.GetSession(valueDate);
        if (timestamp < session.StartUtc || timestamp >= session.EndUtc) return [];

        var closed = new List<FuturesTradeSessionBarReadModel>();
        foreach (var timeFrame in TimeFrames)
        {
            var interval = GetInterval(timeFrame, timestamp, session);
            var key = new BucketKey(seriesIdentity, timeFrame);
            if (buckets.TryGetValue(key, out var bucket))
            {
                if (timestamp < bucket.StartUtc) continue;
                if (timestamp >= bucket.EndUtc)
                {
                    closed.Add(bucket.Close(timeProvider));
                    buckets.Remove(key);
                }
            }
            if (!buckets.TryGetValue(key, out bucket))
            {
                buckets.Add(key, bucket = new ObservationBucket(
                    seriesIdentity, contractId, valueDate, timeFrame, interval.StartUtc, interval.EndUtc,
                    trade.StreamEpochId));
            }
            bucket.Add(trade);
        }
        return closed;
    }

    /// <summary>Closes all intervals whose exclusive end is at or before a server-owned UTC barrier.</summary>
    public IReadOnlyList<FuturesTradeSessionBarReadModel> CloseThrough(
        DateTimeOffset barrierUtc)
    {
        var closed = buckets
            .Where(x => x.Value.EndUtc <= barrierUtc)
            .Select(x => x.Value.Close(timeProvider))
            .ToArray();
        foreach (var observation in closed)
            buckets.Remove(new(observation.MarketSeriesIdentity, observation.TimeFrame));
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

    readonly record struct BucketKey(MarketSeriesIdentity SeriesIdentity, TimeFrameType TimeFrame);
    readonly record struct StreamPosition(Guid EpochId, long Ordinal);
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
            firstEvent = firstEvent == DateTimeOffset.MaxValue
                ? trade.EventTimestamp.ToUniversalTime() : firstEvent;
            lastEvent = trade.EventTimestamp.ToUniversalTime();
        }

        internal FuturesTradeSessionBarReadModel Close(TimeProvider timeProvider)
        {
            var observationId = FuturesTradeSessionBarId.Create(
                seriesIdentity, timeFrame, endUtc, lastSequence);
            return new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = seriesIdentity, ObservationId = observationId,
                ContractId = contractId, ValueDate = valueDate, TimeFrame = timeFrame,
                IntervalStartUtc = startUtc, IntervalEndUtc = endUtc,
                Open = open, High = high, Low = low, Close = close, Volume = volume,
                TradeCount = tradeCount, PriceVolumeSum = priceVolumeSum,
                FirstSourceSequence = firstSequence, LastSourceSequence = lastSequence,
                FirstMarketEventUtc = firstEvent, LastMarketEventUtc = lastEvent,
                CalculatedAtUtc = timeProvider.GetUtcNow(), SchemaVersion = 2,
                CalculationVersion = "trade-session-bar-v1", IsComplete = true, IsValid = true,
                ValidationIssues = [],
                CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
                StreamEpochId = streamEpochId
            };
        }
    }
}
