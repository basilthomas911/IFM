using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

/// <summary>Reconstructs complete indicator seed bars from local original trades; no provider acquisition or fabricated OHLC is permitted.</summary>
public sealed class StoredSessionIndicatorSeedReader(IDbContextFactory databases, IMarketSessionCalendar calendar,
    TimeProvider clock, ILogger<StoredSessionIndicatorSeedReader> logger, IMarketDataApi? market = null,
    DatabentoMarketDataRuntimeOptions? sourceOptions = null)
{
    /// <summary>Reads one bounded sequence of completed trading intervals, or returns unavailable when any interval lacks trades.</summary>
    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, TimeFrameType frame, int count, CancellationToken token)
        => ReadAtAsync(contractId, frame, count, clock.GetUtcNow(), token);

    /// <summary>Uses one captured cutoff across overlapping structure and initialization reads.</summary>
    internal async Task<FuturesTradeSessionBarReadModel[]> ReadAtAsync(string contractId, TimeFrameType frame, int count, DateTimeOffset cutoff, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 64);
        var watch = Stopwatch.StartNew();
        var windows = RsiHistoricalSeedWindowModel.Create(frame, count, cutoff, calendar);
        if (frame == TimeFrameType.Daily)
        {
            var series = MarketSeriesIdentity.ForFuturesSeries(new("ES", "calendar-front", "unadjusted", 1));
            var daily = await ((IHistoricalObservationStore)databases.MarketDataDb).GetRawEodRangeAsync(series, windows[0].ValueDate, windows[^1].ValueDate, token).ConfigureAwait(false);
            var result = new List<FuturesTradeSessionBarReadModel>(count);
            foreach (var window in windows)
            {
                var candidates = daily.Where(value => value.ValueDate == window.ValueDate && value.IsComplete && value.IsValid
                    && value.SessionStartUtc == window.StartUtc && value.SessionEndUtc == window.EndUtc).ToArray();
                if (candidates.Length != 1)
                {
                    logger.LogWarning(
                        "Daily indicator seed unavailable for {ContractId}: expected one complete valid observation for {ValueDate} and session {StartUtc} to {EndUtc}, found {CandidateCount}.",
                        contractId, window.ValueDate, window.StartUtc, window.EndUtc, candidates.Length);
                    return [];
                }
                // Match the existing unadjusted Daily baseline's current-contract
                // alias while retaining its continuation identity and observation ID.
                var bar = FuturesEodObservationMapper.ToDailyBar(candidates[0]) with { ContractId = contractId };
                if (new FuturesTradeSessionBarReadModelValidationRules().Execute(bar).Length != 0) return [];
                result.Add(bar);
            }
            logger.LogInformation("Stored Daily baseline supplied {Count} original session bars for {ContractId} in {ElapsedMilliseconds} ms.", result.Count, contractId, watch.Elapsed.TotalMilliseconds);
            return result.ToArray();
        }
        var validation = new FuturesTradeSessionBarReadModelValidationRules();
        var bars = new Dictionary<DateTimeOffset, FuturesTradeSessionBarReadModel>(windows.Length);
        var sourceVersion = sourceOptions?.FeedOptions.DataSource == FeedDataSourceMode.DatabentoLive
            && sourceOptions.Contracts.SingleOrDefault(registration => registration.DomainContractId == contractId) is { } registration
                ? DatabentoIndicatorSeedSourceVersion.Create(registration, sourceOptions.FeedOptions.Dataset)
                : null;
        foreach (var group in windows.GroupBy(window => window.ValueDate))
        {
            var expected = group.ToDictionary(window => window.EndUtc);
            var stored = await ReadStoredAsync(contractId, frame, group.Key, expected.Keys.ToArray(), sourceVersion, token).ConfigureAwait(false);
            foreach (var bar in stored)
            {
                if (expected.TryGetValue(bar.IntervalEndUtc, out var window) && bar.IntervalStartUtc == window.StartUtc
                    && bar is { IsComplete: true, IsValid: true } && validation.Execute(bar).Length == 0)
                    bars.Add(bar.IntervalEndUtc, bar);
            }
        }

        var missing = windows.Where(window => !bars.ContainsKey(window.EndUtc)).ToArray();
        if (missing.Length > 0 && frame == TimeFrameType.FourHours && market is not null)
        {
            logger.LogInformation("Four-hour indicator history requires qualified native acquisition for {ContractId}; no full multi-session raw-tick scan will be materialized.", contractId);
            return await market.ReadIndicatorSeedBarsAsync(contractId, frame, token).ConfigureAwait(false);
        }

        foreach (var range in RawRanges(missing, frame))
        {
            // Read adjacent short windows together. An overlap admits ordinary
            // delayed writes; exact exchange timestamps qualify each bar below.
            using var buckets = new SeedWindowBuckets(range);
            await foreach (var row in StreamRawRangeAsync(contractId, range[0], range[^1], token).ConfigureAwait(false))
                buckets.Add(row);
            for (var index = 0; index < range.Length; index++)
            {
                var window = range[index];
                // Legacy rows can lack the full aggregation timestamp. Keep the
                // original Scylla time bounds when those rows cannot be filtered locally.
                var source = buckets.HasUnmappedTimes && range.Length > 1
                    ? await ReadRawRangeAsync(contractId, window, window, token).ConfigureAwait(false)
                    : (IEnumerable<BootstrapTradeRow>)buckets.Rows[index];
                var bar = AggregateCore(contractId, frame, window, cutoff, source,
                    alreadyDistinct: !(buckets.HasUnmappedTimes && range.Length > 1));
                if (bar is null)
                {
                    if (market is not null)
                    {
                        var acquired = await market.ReadIndicatorSeedBarsAsync(contractId, frame, token).ConfigureAwait(false);
                        if (acquired.Length >= count)
                        {
                            logger.LogInformation("Qualified native history supplied {Count} {TimeFrame} seed bars for {ContractId} after a local interval gap.", acquired.Length, frame, contractId);
                            return acquired;
                        }
                    }
                    logger.LogWarning("Indicator seed unavailable from local trades and qualified native history: {ContractId} {TimeFrame} {IntervalStartUtc}.", contractId, frame, window.StartUtc);
                    return [];
                }
                bars.Add(bar.IntervalEndUtc, bar);
            }
        }
        logger.LogInformation("Stored indicator seed reconstructed {Count} {TimeFrame} bars for {ContractId} in {ElapsedMilliseconds} ms.", bars.Count, frame, contractId, watch.Elapsed.TotalMilliseconds);
        return windows.Select(window => bars[window.EndUtc]).ToArray();
    }

    async Task<List<BootstrapTradeRow>> ReadRawRangeAsync(string contractId, MarketSessionBounds first,
        MarketSessionBounds last, CancellationToken token)
    {
        var rows = new List<BootstrapTradeRow>();
        await foreach (var row in StreamRawRangeAsync(contractId, first, last, token).ConfigureAwait(false)) rows.Add(row);
        return rows;
    }

    async IAsyncEnumerable<BootstrapTradeRow> StreamRawRangeAsync(string contractId, MarketSessionBounds first,
        MarketSessionBounds last, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var until = last.EndUtc.AddMinutes(1);
        for (var cursor = first.StartUtc.AddMinutes(-1); cursor < until;)
        {
            var midnight = new DateTimeOffset(cursor.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
            var end = until < midnight ? until : midnight;
            var lower = TimeOnly.FromDateTime(cursor.UtcDateTime);
            var upper = end == midnight ? TimeOnly.MaxValue : TimeOnly.FromDateTime(end.UtcDateTime);
            if (databases.TradeTickQuoteDb is IStreamingRawFuturesTradeReader streaming)
            {
                await foreach (var row in streaming.StreamRawFuturesTradeWindowAsync(contractId, first.ValueDate, lower, upper, token).ConfigureAwait(false))
                { token.ThrowIfCancellationRequested(); yield return row; }
            }
            else
                foreach (var row in await databases.TradeTickQuoteDb.GetRawFuturesTradeWindowAsync(contractId, first.ValueDate, lower, upper, token).ConfigureAwait(false))
                { token.ThrowIfCancellationRequested(); yield return row; }
            cursor = end;
        }
    }

    /// <summary>Partitions original input once. Stable per-window order preserves duplicate selection and decimal summation.</summary>
    internal sealed class SeedWindowBuckets(MarketSessionBounds[] windows, System.Buffers.ArrayPool<BootstrapTradeRow>? pool = null) : IDisposable
    {
        // The private pool retains at most sixteen arrays per size bucket, capped at 65,536 references.
        // Larger windows allocate normally rather than growing a long-lived shared pool without bounds.
        static readonly System.Buffers.ArrayPool<BootstrapTradeRow> RowPool = System.Buffers.ArrayPool<BootstrapTradeRow>.Create(65536, 16);
        internal PooledRows[] Rows { get; } = windows.Select(_ => new PooledRows(pool ?? RowPool)).ToArray();
        internal bool HasUnmappedTimes { get; private set; }
        readonly HashSet<(ushort Publisher, uint Instrument, long Sequence, long Time)>[] identities = windows
            .Select(_ => new HashSet<(ushort, uint, long, long)>()).ToArray();

        internal void Add(BootstrapTradeRow row)
        {
            if (row.AggregationTimeUtc is null && !HasUnmappedTimes)
            {
                HasUnmappedTimes = true;
                if (windows.Length > 1) Dispose(); // Legacy qualification will reread individual original windows.
            }
            if (HasUnmappedTimes && windows.Length > 1) return;
            if (row.Action != (short)'T' || row.Price <= 0 || row.Size <= 0) return;
            var time = At(row.EventNanoseconds);
            var low = 0; var high = windows.Length - 1;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                var window = windows[middle];
                if (time < window.StartUtc) high = middle - 1;
                else if (time >= window.EndUtc) low = middle + 1;
                else
                {
                    if (row.ValueDate != window.ValueDate) return;
                    if (windows.Length > 1 && row.AggregationTimeUtc is { } aggregation && !Within(window, aggregation)) return;
                    if (identities[middle].Add((row.PublisherId, row.InstrumentId, row.Sequence, row.EventNanoseconds))) Rows[middle].Add(row);
                    return;
                }
            }
        }

        public void Dispose() { foreach (var rows in Rows) rows.Dispose(); }
    }

    /// <summary>Owns bounded reusable reference buffers; accepted bar values never borrow their storage.</summary>
    internal sealed class PooledRows(System.Buffers.ArrayPool<BootstrapTradeRow> pool) : IEnumerable<BootstrapTradeRow>, IDisposable
    {
        BootstrapTradeRow[] buffer = [];
        int count;
        bool rented, disposed;
        public void Add(BootstrapTradeRow row)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (count == buffer.Length)
            {
                var capacity = Math.Max(16, checked(count * 2));
                var usePool = capacity <= 65536;
                var next = usePool ? pool.Rent(capacity) : new BootstrapTradeRow[capacity];
                Array.Copy(buffer, next, count);
                if (rented) pool.Return(buffer, clearArray: true);
                buffer = next; rented = usePool;
            }
            buffer[count++] = row;
        }
        public IEnumerator<BootstrapTradeRow> GetEnumerator()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            for (var index = 0; index < count; index++) yield return buffer[index];
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (rented) pool.Return(buffer, clearArray: true);
            buffer = []; count = 0;
        }
    }

    static bool Within(MarketSessionBounds window, TimeOnly time)
    {
        var start = window.StartUtc.UtcDateTime.AddMinutes(-1);
        var end = window.EndUtc.UtcDateTime.AddMinutes(1);
        var lower = TimeOnly.FromDateTime(start); var upper = TimeOnly.FromDateTime(end);
        return start.Date == end.Date ? time >= lower && time <= upper
            : time >= lower || (upper != TimeOnly.MinValue && time <= upper);
    }

    async Task<IReadOnlyList<FuturesTradeSessionBarReadModel>> ReadStoredAsync(string contractId, TimeFrameType frame,
        DateOnly date, DateTimeOffset[] ends, string? sourceVersion, CancellationToken token)
    {
        if (sourceVersion is not null)
            return await databases.MarketDataDb.GetQualifiedIndicatorSeedBarsAsync(contractId, frame, date, ends, sourceVersion, token).ConfigureAwait(false);
        if (ends.Length != 1)
            return await databases.MarketDataDb.GetStoredTradeSessionBarsAsync(contractId, frame, date, ends, token).ConfigureAwait(false);
        var bar = await databases.MarketDataDb.GetStoredTradeSessionBarAsync(contractId, frame, date, ends[0], token).ConfigureAwait(false);
        return bar is null ? [] : [bar];
    }

    static IEnumerable<MarketSessionBounds[]> RawRanges(MarketSessionBounds[] windows, TimeFrameType frame)
    {
        var start = 0;
        for (var index = 1; index <= windows.Length; index++)
        {
            if (index < windows.Length && frame is TimeFrameType.FifteenSeconds or TimeFrameType.OneMinute
                && windows[start].ValueDate == windows[index].ValueDate
                && windows[index - 1].EndUtc == windows[index].StartUtc
                && windows[index].EndUtc - windows[start].StartUtc <= TimeSpan.FromMinutes(4))
                continue;
            yield return windows[start..index];
            start = index;
        }
    }

    /// <summary>Builds exact OHLCV and source provenance, discarding duplicates, non-trades, and observations outside the requested interval.</summary>
    public static FuturesTradeSessionBarReadModel? Aggregate(string contractId, TimeFrameType frame,
        MarketSessionBounds window, DateTimeOffset cutoff, IEnumerable<BootstrapTradeRow> source)
        => AggregateCore(contractId, frame, window, cutoff, source, alreadyDistinct: false);

    internal static FuturesTradeSessionBarReadModel? AggregateCore(string contractId, TimeFrameType frame,
        MarketSessionBounds window, DateTimeOffset cutoff, IEnumerable<BootstrapTradeRow> source, bool alreadyDistinct)
    {
        var candidates = source.Where(row => row.ValueDate == window.ValueDate && row.Action == (short)'T' && row.Price > 0 && row.Size > 0)
            .Select(row => (Row: row, Time: At(row.EventNanoseconds)))
            .Where(value => value.Time >= window.StartUtc && value.Time < window.EndUtc);
        var distinct = alreadyDistinct ? candidates : candidates.DistinctBy(value => (value.Row.PublisherId, value.Row.InstrumentId, value.Row.Sequence, value.Row.EventNanoseconds));
        var rows = distinct.OrderBy(value => value.Time).ThenBy(value => value.Row.Sequence).ToArray();
        if (rows.Length == 0) return null;
        var series = MarketSeriesIdentity.ForContract(contractId);
        return new()
        {
            MarketSeriesIdentity = series, ContractId = contractId, TimeFrame = frame, ValueDate = window.ValueDate,
            ObservationId = FuturesTradeSessionBarId.Create(series, frame, window.EndUtc, rows[^1].Row.Sequence),
            IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
            Open = rows[0].Row.Price, High = rows.Max(value => value.Row.Price), Low = rows.Min(value => value.Row.Price), Close = rows[^1].Row.Price,
            Volume = rows.Sum(value => (decimal)value.Row.Size), TradeCount = rows.LongLength,
            PriceVolumeSum = rows.Sum(value => value.Row.Price * value.Row.Size),
            FirstSourceSequence = rows[0].Row.Sequence, LastSourceSequence = rows[^1].Row.Sequence,
            FirstMarketEventUtc = rows[0].Time, LastMarketEventUtc = rows[^1].Time, CalculatedAtUtc = cutoff,
            IsComplete = true, IsValid = true, SchemaVersion = 1,
            CalculationVersion = "stored-trades-seed-v1", CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
        };
    }

    static DateTimeOffset At(long nanoseconds) => DateTimeOffset.FromUnixTimeMilliseconds(nanoseconds / 1_000_000)
        .AddTicks(nanoseconds % 1_000_000 / 100);
}
