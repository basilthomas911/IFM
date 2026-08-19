using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

/// <summary>
/// Single-writer session accumulator. Replayed trades reconstruct observed volume,
/// live trades extend it, and an official cleared-volume statistic replaces it.
/// </summary>
internal sealed class FuturesSessionAccumulator
{
    private const decimal PriceScale = 1_000_000_000m;
    private const byte NewStatistic = 1;
    private const byte UndefinedPriceFlag = 4;
    private const ushort OpeningPrice = 1;
    private const ushort SessionLowPrice = 4;
    private const ushort SessionHighPrice = 5;
    private const ushort ClearedVolume = 6;
    private const long UndefinedStatisticQuantity = long.MaxValue;

    private readonly Dictionary<DateOnly, SessionState> _sessions = [];
    private readonly object _sync = new();

    public bool TryAccumulateTrade(
        string contractId,
        DateOnly valueDate,
        TradeRecord64 record,
        bool replay,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        lock (_sync)
        {
            var state = GetState(valueDate);
            snapshot = state.ToSnapshot(contractId, valueDate);
            if (state.VolumeQuality == FuturesSessionVolumeQuality.OfficialFinal
                || (state.HasTradeSequence && record.Header.Sequence <= state.TradeSequence))
                return false;

            state.HasTradeSequence = true;
            state.TradeSequence = record.Header.Sequence;
            state.Volume = checked(state.Volume + record.Size);
            state.VolumeQuality = replay
                ? FuturesSessionVolumeQuality.Bootstrapping
                : FuturesSessionVolumeQuality.ObservedComplete;
            state.EventTimestampNanoseconds = record.Header.EventTimestampNanoseconds;
            snapshot = state.ToSnapshot(contractId, valueDate);
            return true;
        }
    }

    public FuturesSessionStatisticsSnapshot CompleteTradeReplay(
        string contractId,
        DateOnly valueDate)
    {
        lock (_sync)
        {
            var state = GetState(valueDate);
            if (state.VolumeQuality != FuturesSessionVolumeQuality.OfficialFinal)
                state.VolumeQuality = FuturesSessionVolumeQuality.ObservedComplete;
            return state.ToSnapshot(contractId, valueDate);
        }
    }

    public bool TryApplyStatistic(
        string contractId,
        DateOnly currentValueDate,
        StatisticsRecord64 record,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        lock (_sync)
        {
            var isClearedVolume = record.StatisticType == ClearedVolume;
            var valueDate = isClearedVolume
                ? GetReferenceDate(record.ReferenceTimestampNanoseconds, currentValueDate)
                : currentValueDate;
            var state = GetState(valueDate);
            snapshot = state.ToSnapshot(contractId, valueDate);
            if (record.UpdateAction != NewStatistic)
                return false;

            bool changed;
            if (isClearedVolume)
            {
                if (record.Quantity < 0
                    || record.Quantity == UndefinedStatisticQuantity)
                    return false;
                changed = state.Volume != record.Quantity
                    || state.VolumeQuality != FuturesSessionVolumeQuality.OfficialFinal;
                state.Volume = record.Quantity;
                state.VolumeQuality = FuturesSessionVolumeQuality.OfficialFinal;
            }
            else
            {
                if ((record.Header.Flags & UndefinedPriceFlag) != 0 || record.Price <= 0)
                    return false;
                var value = record.Price / PriceScale;
                changed = record.StatisticType switch
                {
                    OpeningPrice => TrySet(
                        ref state.Open, ref state.OpenSequence, value, record.Header.Sequence),
                    SessionLowPrice => TrySet(
                        ref state.Low, ref state.LowSequence, value, record.Header.Sequence),
                    SessionHighPrice => TrySet(
                        ref state.High, ref state.HighSequence, value, record.Header.Sequence),
                    _ => false
                };
            }

            if (!changed)
                return false;
            state.EventTimestampNanoseconds = record.Header.EventTimestampNanoseconds;
            snapshot = state.ToSnapshot(contractId, valueDate);
            return snapshot.HasAnyData;
        }
    }

    public bool TryRead(
        string contractId,
        DateOnly valueDate,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(valueDate, out var state))
            {
                snapshot = default;
                return false;
            }
            snapshot = state.ToSnapshot(contractId, valueDate);
            return snapshot.HasAnyData;
        }
    }

    public FuturesSessionStatisticsSnapshot[] ReadAll(string contractId)
    {
        lock (_sync)
            return _sessions
                .Select(pair => pair.Value.ToSnapshot(contractId, pair.Key))
                .Where(static snapshot => snapshot.HasAnyData)
                .ToArray();
    }

    public void Reset()
    {
        lock (_sync)
            _sessions.Clear();
    }

    private SessionState GetState(DateOnly valueDate)
    {
        if (_sessions.TryGetValue(valueDate, out var state))
            return state;
        state = new SessionState();
        _sessions.Add(valueDate, state);
        return state;
    }

    private static DateOnly GetReferenceDate(long timestampNanoseconds, DateOnly fallback)
    {
        if (timestampNanoseconds <= 0)
            return fallback;
        try
        {
            return DateOnly.FromDateTime(
                DateTimeOffset.UnixEpoch.AddTicks(timestampNanoseconds / 100L).UtcDateTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return fallback;
        }
    }

    private static bool TrySet(
        ref decimal? target,
        ref uint targetSequence,
        decimal value,
        uint sequence)
    {
        if (targetSequence != 0 && sequence <= targetSequence)
            return false;
        targetSequence = sequence;
        if (target == value)
            return false;
        target = value;
        return true;
    }

    private sealed class SessionState
    {
        internal decimal? Open;
        internal decimal? High;
        internal decimal? Low;
        internal uint OpenSequence;
        internal uint HighSequence;
        internal uint LowSequence;
        internal bool HasTradeSequence;
        internal uint TradeSequence;
        internal long Volume;
        internal FuturesSessionVolumeQuality VolumeQuality;
        internal long EventTimestampNanoseconds;

        internal FuturesSessionStatisticsSnapshot ToSnapshot(
            string contractId,
            DateOnly valueDate) => new(
            contractId,
            valueDate,
            Open ?? 0m,
            High ?? 0m,
            Low ?? 0m,
            Math.Max(TradeSequence, Math.Max(OpenSequence, Math.Max(HighSequence, LowSequence))),
            EventTimestampNanoseconds,
            Volume,
            VolumeQuality);
    }
}
