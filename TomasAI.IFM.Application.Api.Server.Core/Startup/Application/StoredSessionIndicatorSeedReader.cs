using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Remote;
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
    DatabentoMarketDataRuntimeOptions? sourceOptions = null, MarketDataReadMirror? remote = null,
    IConfiguration? configuration = null) : ICompletedDailyObservationRepair
{
    internal const string RebuiltBarVersion = "stored-trades-rebuilt-v1";

    /// <summary>Publishes a missing completed Daily observation only after every original FourHours interval is qualified.</summary>
    public async Task RepairAsync(MarketSeriesIdentity series, string contractId, DateOnly valueDate, CancellationToken token)
    {
        if (series.FuturesSeriesId is not { RootSymbol: "ES" } || string.IsNullOrWhiteSpace(contractId)) return;
        var store = (IHistoricalObservationStore)databases.MarketDataDb;
        var session = calendar.GetSession(valueDate);
        if (session.EndUtc > clock.GetUtcNow() || await store.GetRawEodAsync(series, valueDate, token).ConfigureAwait(false)
            is { IsComplete: true, IsValid: true }) return;
        var version = await ResolveSourceVersionAsync(contractId, token).ConfigureAwait(false);
        if (version is null) return;
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FourHours, 8, session.EndUtc, calendar)
            .Where(window => window.ValueDate == valueDate).ToArray();
        var ends = windows.Select(window => window.EndUtc).ToArray();
        if (ends.Length == 0) return;
        var bars = await ReadStoredAsync(contractId, TimeFrameType.FourHours, valueDate, ends, version, token, series).ConfigureAwait(false);
        var observation = AggregateCompletedDaily(series, contractId, session, windows, bars);
        if (observation is null && market is not null && market.GetRuntimeStatus().IsRunning)
        {
            var acquired = await market.ReadIndicatorSeedBarsAsync(contractId, TimeFrameType.FourHours, token).ConfigureAwait(false);
            observation = AggregateCompletedDaily(series, contractId, session, windows, acquired);
        }
        if (observation is null) return;
        if (await store.TryWriteRawEodAsync(observation, token).ConfigureAwait(false))
            logger.LogInformation("Recovered completed Daily session for {ContractId} {ValueDate} from {Count} qualified FourHours intervals; original final observation {AsOfUtc}.",
                contractId, valueDate, windows.Length, observation.LastMarketEventUtc);
    }

    /// <summary>Aggregates complete, gap-free provider-qualified coverage without inventing trades, volume or missing intervals.</summary>
    internal static FuturesEodObservationReadModel? AggregateCompletedDaily(MarketSeriesIdentity series, string contractId,
        MarketSessionBounds session, MarketSessionBounds[] windows, IReadOnlyList<FuturesTradeSessionBarReadModel> source)
    {
        if (windows.Length == 0 || windows[0].StartUtc != session.StartUtc || windows[^1].EndUtc != session.EndUtc) return null;
        var validation = new FuturesTradeSessionBarReadModelValidationRules();
        var bars = new List<FuturesTradeSessionBarReadModel>(windows.Length);
        foreach (var window in windows)
        {
            if (bars.Count > 0 && bars[^1].IntervalEndUtc != window.StartUtc) return null;
            var matches = source.Where(bar => bar.ContractId == contractId && bar.ValueDate == session.ValueDate
                && bar.TimeFrame == TimeFrameType.FourHours && bar.IntervalStartUtc == window.StartUtc && bar.IntervalEndUtc == window.EndUtc
                && bar.IsComplete && bar.IsValid && validation.Execute(bar).Length == 0
                && bar.CalculationVersion is "databento-historical-minute-seed-v1" or "databento-historical-trade-seed-v1" or "databento-four-hour-trade-replay-v1").ToArray();
            if (matches.Length != 1) return null;
            bars.Add(matches[0]);
        }
        return new()
        {
            MarketSeriesIdentity = series, ContractId = contractId, ValueDate = session.ValueDate,
            SessionStartUtc = session.StartUtc, SessionEndUtc = session.EndUtc,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, session.EndUtc, bars[^1].LastSourceSequence),
            Open = bars[0].Open, High = bars.Max(bar => bar.High), Low = bars.Min(bar => bar.Low), Close = bars[^1].Close,
            Volume = bars.Sum(bar => bar.Volume), TradeCount = bars.All(bar => bar.TradeCount > 0) ? bars.Sum(bar => bar.TradeCount) : 0,
            PriceVolumeSum = bars.All(bar => bar.PriceVolumeSum > 0) ? bars.Sum(bar => bar.PriceVolumeSum) : 0, FirstSourceSequence = bars[0].FirstSourceSequence,
            LastSourceSequence = bars[^1].LastSourceSequence, FirstMarketEventUtc = bars[0].FirstMarketEventUtc,
            LastMarketEventUtc = bars[^1].LastMarketEventUtc, IsComplete = true, IsValid = true
        };
    }

    /// <summary>Reads one bounded sequence of completed trading intervals, or returns unavailable when any interval lacks trades.</summary>
    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, TimeFrameType frame, int count, CancellationToken token)
        => ReadAtAsync(contractId, frame, count, clock.GetUtcNow(), token);

    /// <summary>Uses one captured cutoff across overlapping structure and initialization reads.</summary>
    internal async Task<FuturesTradeSessionBarReadModel[]> ReadAtAsync(string contractId, TimeFrameType frame, int count, DateTimeOffset cutoff, CancellationToken token,
        bool allowProvider = true, bool requireQualifiedCurrentSession = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 64);
        var watch = Stopwatch.StartNew();
        var windows = RsiHistoricalSeedWindowModel.Create(frame, count, cutoff, calendar);
        if (windows.Length != count || windows.Length == 0) return [];
        var liveSeries = StoredSessionIndicatorWarmup.LiveIndicatorSeries(contractId);
        var sourceVersion = frame != TimeFrameType.Daily || liveSeries.Kind == MarketSeriesIdentityKind.FuturesContinuation
            ? await ResolveSourceVersionAsync(contractId, token).ConfigureAwait(false) : null;
        MarketSeriesIdentity? continuation = sourceVersion is not null && liveSeries.Kind == MarketSeriesIdentityKind.FuturesContinuation
            ? liveSeries : null;
        if (requireQualifiedCurrentSession && continuation is null)
            return await FallbackAsync().ConfigureAwait(false);
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
                if (continuation is { } live && window == windows[^1] && candidates[0].ContractId == contractId && candidates[0].TradeCount > 0)
                {
                    var rebuilt = await RebuildLongIntervalAsync(contractId, frame, window, cutoff, live, token).ConfigureAwait(false);
                    if (!CoversOriginal(rebuilt, bar)) return [];
                    if (!HasSameTrades(bar, rebuilt!))
                        bar = rebuilt! with { CalculationVersion = RebuiltBarVersion };
                }
                if (new FuturesTradeSessionBarReadModelValidationRules().Execute(bar).Length != 0) return [];
                result.Add(bar);
            }
            logger.LogInformation("Stored Daily baseline supplied {Count} original session bars for {ContractId} in {ElapsedMilliseconds} ms.", result.Count, contractId, watch.Elapsed.TotalMilliseconds);
            return result.ToArray();
        }
        var validation = new FuturesTradeSessionBarReadModelValidationRules();
        var bars = new Dictionary<DateTimeOffset, FuturesTradeSessionBarReadModel>(windows.Length);
        var currentValueDate = calendar.GetValueDate(cutoff);
        var localRepairs = new Dictionary<DateTimeOffset, FuturesTradeSessionBarReadModel>();
        var pendingLiveReplay = false;
        foreach (var group in windows.GroupBy(window => window.ValueDate))
        {
            var expected = group.ToDictionary(window => window.EndUtc);
            var stored = await ReadStoredAsync(contractId, frame, group.Key, expected.Keys.ToArray(), sourceVersion, token, continuation).ConfigureAwait(false);
            foreach (var bar in stored)
            {
                if (!expected.TryGetValue(bar.IntervalEndUtc, out var window) || bar.IntervalStartUtc != window.StartUtc
                    || bar is not { IsComplete: true, IsValid: true } || validation.Execute(bar).Length != 0) continue;
                // A live accumulator can restart inside an open interval and still close it
                // with IsComplete=true. Confirm those bars against the original stored trades.
                if (continuation is { } series && bar.MarketSeriesIdentity == series && bar.CalculationVersion == "trade-session-bar-v1")
                    localRepairs.Add(bar.IntervalEndUtc, bar);
                else
                    bars.Add(bar.IntervalEndUtc, bar);
            }
        }

        var missing = windows.Where(window => !bars.ContainsKey(window.EndUtc)).ToArray();
        if (missing.Length > 0 && frame == TimeFrameType.FourHours && continuation is null && market is not null && allowProvider)
        {
            logger.LogInformation("Four-hour indicator history requires qualified native acquisition for {ContractId}; no full multi-session raw-tick scan will be materialized.", contractId);
            return await market.ReadIndicatorSeedBarsAsync(contractId, frame, token).ConfigureAwait(false);
        }

        foreach (var range in RawRanges(missing, frame))
        {
            // Keep the existing native recovery requirement for a detected live transport gap.
            // Prior-session bars can still be verified from the durable tick partition.
            if (requireQualifiedCurrentSession && range[0].ValueDate == currentValueDate)
            {
                pendingLiveReplay = true;
                continue;
            }
            // Read adjacent short windows together. An overlap admits ordinary
            // delayed writes; exact exchange timestamps qualify each bar below.
            var streamLongInterval = continuation is not null && frame is TimeFrameType.OneHour or TimeFrameType.FourHours;
            using var buckets = streamLongInterval ? null : new SeedWindowBuckets(range);
            if (buckets is not null)
                await foreach (var row in StreamRawRangeAsync(contractId, range[0], range[^1], token).ConfigureAwait(false))
                    buckets.Add(row);
            for (var index = 0; index < range.Length; index++)
            {
                var window = range[index];
                // Legacy rows can lack the full aggregation timestamp. Keep the
                // original Scylla time bounds when those rows cannot be filtered locally.
                var bar = buckets is null
                    ? await RebuildLongIntervalAsync(contractId, frame, window, cutoff, continuation!.Value, token).ConfigureAwait(false)
                    : AggregateCore(contractId, frame, window, cutoff,
                        buckets.HasUnmappedTimes && range.Length > 1
                            ? await ReadRawRangeAsync(contractId, window, window, token).ConfigureAwait(false)
                            : buckets.Rows[index],
                        alreadyDistinct: !(buckets.HasUnmappedTimes && range.Length > 1), continuation);
                localRepairs.TryGetValue(window.EndUtc, out var original);
                // The persisted live bar can contain trades absent from the raw store.
                // Preserve it if it covers the available raw sample more fully.
                if (original is not null && bar is not null
                    && (HasSameTrades(original, bar) || (CoversOriginal(original, bar) && !CoversOriginal(bar, original))))
                {
                    bars.Add(window.EndUtc, original);
                    continue;
                }
                if (!CoversOriginal(bar, original))
                {
                    logger.LogWarning("Original stored trades cannot qualify indicator seed for {ContractId} {TimeFrame} {IntervalStartUtc}: original {OriginalTrades} [{OriginalFirst}, {OriginalLast}], rebuilt {RebuiltTrades} [{RebuiltFirst}, {RebuiltLast}].",
                        contractId, frame, window.StartUtc, original?.TradeCount, original?.FirstMarketEventUtc,
                        original?.LastMarketEventUtc, bar?.TradeCount, bar?.FirstMarketEventUtc, bar?.LastMarketEventUtc);
                    if (continuation is not null && window.ValueDate == currentValueDate)
                    {
                        pendingLiveReplay = true;
                        continue;
                    }
                    return await FallbackAsync().ConfigureAwait(false);
                }
                bars.Add(window.EndUtc, original is null ? bar! : bar! with { CalculationVersion = RebuiltBarVersion });
                if (original is not null)
                    logger.LogInformation("Rebuilt partial indicator seed for {ContractId} {TimeFrame} ending {IntervalEndUtc}: {OriginalTrades} to {StoredTrades} trades.",
                        contractId, frame, window.EndUtc, original.TradeCount, bar!.TradeCount);
            }
        }
        if (pendingLiveReplay)
        {
            if (!allowProvider || market is null) return [];
            // The Host can replay gaps in the current session using Live. Transfer
            // the verified prior-session bars to its qualified coverage first.
            foreach (var group in bars.Values.Where(bar => bar.ValueDate != currentValueDate).GroupBy(bar => bar.ValueDate))
                await databases.MarketDataDb.StoreQualifiedIndicatorSeedBarsAsync(sourceVersion!, group.ToArray(), token).ConfigureAwait(false);
            return await FallbackAsync().ConfigureAwait(false);
        }
        logger.LogInformation("Stored indicator seed reconstructed {Count} {TimeFrame} bars for {ContractId} in {ElapsedMilliseconds} ms.", bars.Count, frame, contractId, watch.Elapsed.TotalMilliseconds);
        return windows.Select(window => bars[window.EndUtc]).ToArray();

        async Task<FuturesTradeSessionBarReadModel[]> FallbackAsync()
        {
            if (market is null || !allowProvider) return [];
            var acquired = await market.ReadIndicatorSeedBarsAsync(contractId, frame, token).ConfigureAwait(false);
            return acquired.Length >= count ? acquired : [];
        }
    }

    async Task<string?> ResolveSourceVersionAsync(string contractId, CancellationToken token)
    {
        if (sourceOptions?.FeedOptions.DataSource == FeedDataSourceMode.DatabentoLive
            && sourceOptions.Contracts.SingleOrDefault(registration => registration.DomainContractId == contractId) is { } registration)
            return DatabentoIndicatorSeedSourceVersion.Create(registration, sourceOptions.FeedOptions.Dataset);
        if (remote is null) return null;
        if (remote.GetIndicatorSeedSourceVersion(contractId) is { } version) return version;
        // Startup already qualified this contract, but the Host's feed snapshot
        // can arrive after the first seed read. Its stored definition carries the
        // same provider symbol without a Historical range request.
        if (StoredSessionIndicatorWarmup.LiveIndicatorSeries(contractId).Kind != MarketSeriesIdentityKind.FuturesContinuation
            || await databases.SecuritiesDb.GetFuturesContractAsync(contractId, token).ConfigureAwait(false) is not { LocalSymbol.Length: > 0 } contract)
            return null;
        var dataset = configuration?["AppSettings:Databento:FuturesContractDatasets:ES"]
            ?? configuration?["AppSettings:Databento:Dataset"] ?? "GLBX.MDP3";
        return DatabentoIndicatorSeedSourceVersion.Create(contract.LocalSymbol, dataset);
    }

    static bool CoversOriginal(FuturesTradeSessionBarReadModel? rebuilt, FuturesTradeSessionBarReadModel? original)
        => rebuilt is not null && (original is null || (rebuilt.TradeCount >= original.TradeCount
            && (rebuilt.FirstMarketEventUtc - original.FirstMarketEventUtc).Ticks < 10
            && (original.LastMarketEventUtc - rebuilt.LastMarketEventUtc).Ticks < 10));

    // The persisted canonical timestamp has PostgreSQL microsecond precision.
    static bool HasSameTrades(FuturesTradeSessionBarReadModel left, FuturesTradeSessionBarReadModel right)
        => left.TradeCount == right.TradeCount && Math.Abs((left.FirstMarketEventUtc - right.FirstMarketEventUtc).Ticks) < 10
            && Math.Abs((left.LastMarketEventUtc - right.LastMarketEventUtc).Ticks) < 10 && left.FirstSourceSequence == right.FirstSourceSequence
            && left.LastSourceSequence == right.LastSourceSequence && left.Open == right.Open && left.High == right.High
            && left.Low == right.Low && left.Close == right.Close && left.Volume == right.Volume
            && left.PriceVolumeSum == right.PriceVolumeSum;

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
            var ticks = databases.TradeTickQuoteDb;
            RawTickRequestQualification.RequireWindow(contractId, first.ValueDate, lower, upper);
            if (ticks.SupportsRawFuturesTradeStreaming)
            {
                await foreach (var row in ticks.StreamRawFuturesTradeWindowAsync(contractId, first.ValueDate, lower, upper, token).ConfigureAwait(false))
                { token.ThrowIfCancellationRequested(); yield return row; }
            }
            else
                foreach (var row in await ticks.GetRawFuturesTradeWindowAsync(contractId, first.ValueDate, lower, upper, token).ConfigureAwait(false))
                { token.ThrowIfCancellationRequested(); yield return row; }
            cursor = end;
        }
    }

    async Task<FuturesTradeSessionBarReadModel?> RebuildLongIntervalAsync(string contractId, TimeFrameType frame,
        MarketSessionBounds window, DateTimeOffset cutoff, MarketSeriesIdentity series, CancellationToken token)
    {
        var seen = new HashSet<(ushort Publisher, uint Instrument, long Sequence, long Time)>();
        BootstrapTradeRow first = default!, last = default!;
        DateTimeOffset firstAt = default, lastAt = default;
        decimal high = decimal.MinValue, low = decimal.MaxValue, volume = 0, priceVolume = 0;
        await foreach (var row in StreamRawRangeAsync(contractId, window, window, token).ConfigureAwait(false))
        {
            if (row.ValueDate != window.ValueDate || row.Action != (short)'T' || row.Price <= 0 || row.Size <= 0) continue;
            var at = At(row.EventNanoseconds);
            if (at < window.StartUtc || at >= window.EndUtc
                || !seen.Add((row.PublisherId, row.InstrumentId, row.Sequence, row.EventNanoseconds))) continue;
            if (seen.Count == 1 || (at, row.Sequence).CompareTo((firstAt, first.Sequence)) < 0)
                (first, firstAt) = (row, at);
            if (seen.Count == 1 || (at, row.Sequence).CompareTo((lastAt, last.Sequence)) >= 0)
                (last, lastAt) = (row, at);
            high = Math.Max(high, row.Price);
            low = Math.Min(low, row.Price);
            volume += row.Size;
            priceVolume += row.Price * row.Size;
        }
        return seen.Count == 0 ? null : CreateBar(contractId, frame, window, cutoff, series, first, last,
            firstAt, lastAt, high, low, volume, seen.Count, priceVolume);
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
        DateOnly date, DateTimeOffset[] ends, string? sourceVersion, CancellationToken token, MarketSeriesIdentity? continuation)
    {
        if (sourceVersion is not null)
        {
            IndicatorSeedRequestQualification.RequireSelection(sourceVersion, ends);
            return await databases.MarketDataDb.GetQualifiedIndicatorSeedBarsAsync(contractId, frame, date, ends, sourceVersion, token, continuation).ConfigureAwait(false);
        }
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ends.Length, 64);
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
        MarketSessionBounds window, DateTimeOffset cutoff, IEnumerable<BootstrapTradeRow> source, bool alreadyDistinct,
        MarketSeriesIdentity? seriesIdentity = null)
    {
        var candidates = source.Where(row => row.ValueDate == window.ValueDate && row.Action == (short)'T' && row.Price > 0 && row.Size > 0)
            .Select(row => (Row: row, Time: At(row.EventNanoseconds)))
            .Where(value => value.Time >= window.StartUtc && value.Time < window.EndUtc);
        var distinct = alreadyDistinct ? candidates : candidates.DistinctBy(value => (value.Row.PublisherId, value.Row.InstrumentId, value.Row.Sequence, value.Row.EventNanoseconds));
        var rows = distinct.OrderBy(value => value.Time).ThenBy(value => value.Row.Sequence).ToArray();
        if (rows.Length == 0) return null;
        var series = seriesIdentity ?? MarketSeriesIdentity.ForContract(contractId);
        return CreateBar(contractId, frame, window, cutoff, series, rows[0].Row, rows[^1].Row,
            rows[0].Time, rows[^1].Time, rows.Max(value => value.Row.Price), rows.Min(value => value.Row.Price),
            rows.Sum(value => (decimal)value.Row.Size), rows.LongLength, rows.Sum(value => value.Row.Price * value.Row.Size));
    }

    static FuturesTradeSessionBarReadModel CreateBar(string contractId, TimeFrameType frame, MarketSessionBounds window,
        DateTimeOffset cutoff, MarketSeriesIdentity series, BootstrapTradeRow first, BootstrapTradeRow last,
        DateTimeOffset firstAt, DateTimeOffset lastAt, decimal high, decimal low, decimal volume, long tradeCount,
        decimal priceVolume) => new()
        {
            MarketSeriesIdentity = series, ContractId = contractId, TimeFrame = frame, ValueDate = window.ValueDate,
            ObservationId = FuturesTradeSessionBarId.Create(series, frame, window.EndUtc, last.Sequence),
            IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
            Open = first.Price, High = high, Low = low, Close = last.Price,
            Volume = volume, TradeCount = tradeCount, PriceVolumeSum = priceVolume,
            FirstSourceSequence = first.Sequence, LastSourceSequence = last.Sequence,
            FirstMarketEventUtc = firstAt, LastMarketEventUtc = lastAt, CalculatedAtUtc = cutoff,
            IsComplete = true, IsValid = true, SchemaVersion = 1,
            CalculationVersion = "stored-trades-seed-v1", CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
        };

    static DateTimeOffset At(long nanoseconds) => DateTimeOffset.FromUnixTimeMilliseconds(nanoseconds / 1_000_000)
        .AddTicks(nanoseconds % 1_000_000 / 100);
}
