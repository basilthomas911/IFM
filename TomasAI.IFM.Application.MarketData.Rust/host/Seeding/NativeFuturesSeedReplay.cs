// Application bar identity, validation and window policy retained from the current Host.
// Socket/DBN processing is native and cancellable; no legacy feed factory is used.
using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Seeding;

/// <summary>Combines bounded prior-session Historical OHLCV with read-only current-session Live replay into complete seed bars.</summary>
public sealed class NativeFuturesSeedReplay(
    INativeSeedReplaySource replay,
    DatabentoMarketDataRuntimeOptions options,
    IMarketSessionCalendar calendar,
    TimeProvider clock,
    ILogger<NativeFuturesSeedReplay>? logger = null,
    IMarketDataHistoricalProvider? historical = null,
    IDbContextFactory? databases = null)
{
    const int BarCount = 48;
    readonly ConcurrentDictionary<(string ContractId, TimeFrameType Frame), SemaphoreSlim> acquisitions = new();
    readonly SharedShortSeedReplay shortReplay = new();

    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, CancellationToken cancellationToken)
        => ReadCoreAsync(contractId, TimeFrameType.FiveMinutes, 0.10m, cancellationToken);

    /// <summary>Repairs intraday seed coverage without authorizing a paid historical acquisition.</summary>
    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, TimeFrameType timeFrame, CancellationToken cancellationToken)
        => ReadCoreAsync(contractId, timeFrame, 0m, cancellationToken);

    /// <summary>Independent EMA200 bootstrap; ordinary indicator recovery retains its 48-bar bound.</summary>
    public Task<FuturesTradeSessionBarReadModel[]> ReadEmaAsync(string contractId, TimeFrameType frame, int count, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 200);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 512);
        return ReadCoreAsync(contractId, frame, 0m, token, count, true);
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadCoreAsync(
        string contractId, TimeFrameType timeFrame, decimal maximumCostUsd, CancellationToken cancellationToken, int count = BarCount, bool emaBootstrap = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeFrame is not (TimeFrameType.FifteenSeconds or TimeFrameType.OneMinute or TimeFrameType.FiveMinutes or TimeFrameType.FifteenMinutes or TimeFrameType.OneHour or TimeFrameType.FourHours)
            && !(emaBootstrap && timeFrame == TimeFrameType.Daily))
            throw new ArgumentOutOfRangeException(nameof(timeFrame));
        if (options.FeedOptions.DataSource != FeedDataSourceMode.DatabentoLive)
            return [];
        var registration = options.Contracts.SingleOrDefault(value =>
            StringComparer.Ordinal.Equals(value.DomainContractId, contractId));
        if (registration is null)
            throw new InvalidOperationException($"No Databento registration exists for {contractId}.");

        if (databases is null || maximumCostUsd != 0m)
            return await ReadQualifiedAsync(null).ConfigureAwait(false);

        // Waiters recheck the durable coverage after the owner finishes. A timed-out
        // caller cannot start a competing replay or erase previously qualified bars.
        var acquisition = acquisitions.GetOrAdd((contractId, timeFrame), static _ => new SemaphoreSlim(1));
        await acquisition.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadQualifiedAsync(databases).ConfigureAwait(false);
        }
        finally
        {
            acquisition.Release();
        }

        async Task<FuturesTradeSessionBarReadModel[]> ReadQualifiedAsync(IDbContextFactory? cache)
        {
            var bars = await ReadSeedAsync(registration, contractId, timeFrame, maximumCostUsd, cache, cancellationToken, count, emaBootstrap).ConfigureAwait(false);
            // CFE's live accumulator publishes traded intervals only. Quiet intervals
            // must not prevent its equally sparse original-trade history from warming bands.
            if (bars.Length < 40 && timeFrame is TimeFrameType.FifteenSeconds or TimeFrameType.OneMinute
                && StringComparer.Ordinal.Equals(registration.Dataset ?? options.FeedOptions.Dataset, "XCBF.PITCH"))
                return await ReadSparseVxAsync(registration, contractId, timeFrame, cache, cancellationToken).ConfigureAwait(false);
            return bars;
        }
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadSparseVxAsync(DatabentoContractRegistration registration,
        string contractId, TimeFrameType frame, IDbContextFactory? cache, CancellationToken token)
    {
        if (historical is null) return [];
        const int recordLimit = 250_000;
        var now = clock.GetUtcNow();
        var cutoff = RsiHistoricalSeedWindowModel.Create(frame, 1, now, calendar)[0].EndUtc;
        var date = calendar.GetValueDate(now);
        var historyEnd = calendar.GetSession(date).StartUtc;
        var sourceVersion = DatabentoIndicatorSeedSourceVersion.Create(registration, options.FeedOptions.Dataset);
        var series = MarketSeriesIdentity.ForContract(contractId);
        var buckets = new SortedDictionary<DateTimeOffset, SparseBucket>();
        var seen = new HashSet<(string Publisher, string Instrument, long Sequence, DateTimeOffset At)>();
        var total = 0;

        // Current-session replay is bounded by the same successful final marker as
        // dense ES recovery. Historical ranges and Live are disjoint at historyEnd.
        if (historyEnd < cutoff)
        {
            await using var live = replay.Open(registration, historyEnd, TimeSpan.FromMinutes(2), token);
            await ReadAsync(live, historyEnd, cutoff).ConfigureAwait(false);
        }
        for (var day = 0; day < 7 && buckets.Count < BarCount; day++)
        {
            var start = historyEnd.AddDays(-1);
            var request = new HistoricalProviderRequest
            {
                Dataset = registration.Dataset ?? options.FeedOptions.Dataset, Symbols = [registration.ProviderContractName],
                Schema = HistoricalDataSchema.Trades, Symbology = HistoricalSymbology.RawSymbol,
                StartUtc = start, EndUtc = historyEnd, RecordLimit = recordLimit + 1,
                RequestHash = $"sparse-vx-seed-v1|{sourceVersion}|{frame}|{start:O}|{historyEnd:O}"
            };
            var estimate = await historical.EstimateAsync(request, token).ConfigureAwait(false);
            if (estimate.EstimatedCostUsd != 0m)
                throw new InvalidOperationException($"Sparse VX seed requires a zero-dollar estimate; provider estimated ${estimate.EstimatedCostUsd}. No data was purchased.");
            if (estimate.EstimatedRecords > recordLimit - total)
                throw new InvalidOperationException("Sparse VX seed exceeds its bounded original-trade record budget.");
            await using var range = await historical.OpenRangeAsync(request, 1024, token).ConfigureAwait(false);
            await ReadAsync(range, start, historyEnd).ConfigureAwait(false);
            historyEnd = start;
        }
        if (buckets.Count < 40) return [];
        var validation = new FuturesTradeSessionBarReadModelValidationRules();
        var result = buckets.Values.TakeLast(BarCount)
            .Select(value => value.Trades.ToObservation(series, contractId, value.Window, cutoff, frame) with
            { CalculationVersion = "databento-historical-trade-seed-v1" })
            .ToArray();
        if (result.Any(bar => validation.Execute(bar).Length != 0))
            throw new InvalidDataException("Sparse VX history contains an invalid original-trade interval.");
        await CacheBarsAsync(cache, sourceVersion, result, token).ConfigureAwait(false);
        logger?.LogInformation("Qualified sparse VX seed for {ContractId} {Frame}: {Count} nonempty original-trade bars through {Through}; quiet intervals remain absent.",
            contractId, frame, result.Length, result[^1].LastMarketEventUtc);
        return result;

        async Task ReadAsync(IHistoricalRecordReader reader, DateTimeOffset start, DateTimeOffset end)
        {
            var complete = false;
            while (await reader.ReadNextAsync(token).ConfigureAwait(false) is { } batch)
            {
                complete = batch.IsFinal;
                foreach (var trade in batch.Records)
                {
                    if (++total > recordLimit) throw new InvalidDataException("Sparse VX seed exceeded its original-trade record budget.");
                    if (trade.Kind != HistoricalProviderRecordKind.Trade || trade.CloseOrPrice <= 0 || trade.VolumeOrSize <= 0
                        || trade.Action is NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear
                        || (trade.Conditions & NormalizedTradeConditionFlags.UndefinedPrice) != 0
                        || trade.EventTimestampUtc < start || trade.EventTimestampUtc >= end
                        || !seen.Add((trade.PublisherId, trade.InstrumentId, trade.SourceSequence, trade.EventTimestampUtc))) continue;
                    var sessionDate = calendar.GetValueDate(trade.EventTimestampUtc);
                    if (!calendar.IsTradingDate(sessionDate)) continue;
                    var session = calendar.GetSession(sessionDate);
                    if (trade.EventTimestampUtc < session.StartUtc || trade.EventTimestampUtc >= session.EndUtc) continue;
                    var ticks = frame == TimeFrameType.OneMinute ? TimeSpan.TicksPerMinute : TimeSpan.TicksPerSecond * 15;
                    var intervalStart = session.StartUtc.AddTicks((trade.EventTimestampUtc - session.StartUtc).Ticks / ticks * ticks);
                    var intervalEnd = intervalStart.AddTicks(ticks);
                    if (intervalEnd > session.EndUtc) intervalEnd = session.EndUtc;
                    if (intervalStart < start || intervalEnd > end || intervalEnd > cutoff) continue;
                    if (!buckets.TryGetValue(intervalStart, out var bucket))
                        buckets.Add(intervalStart, bucket = new(new(sessionDate, intervalStart, intervalEnd)));
                    bucket.Trades.Add(trade.CloseOrPrice, checked((uint)trade.VolumeOrSize), trade.EventTimestampUtc, checked((uint)trade.SourceSequence));
                }
            }
            if (!complete) throw new InvalidDataException("Sparse VX history ended without a successful final source marker.");
        }
    }

    sealed class SparseBucket(MarketSessionBounds window)
    {
        public MarketSessionBounds Window { get; } = window;
        public BarBucket Trades;
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadSeedAsync(DatabentoContractRegistration registration,
        string contractId, TimeFrameType timeFrame, decimal maximumCostUsd, IDbContextFactory? cache, CancellationToken token, int count = BarCount, bool emaBootstrap = false)
    {
        var now = clock.GetUtcNow();
        var cutoff = now;
        var windows = RsiHistoricalSeedWindowModel.Create(
            timeFrame, count, cutoff, calendar);
        if (windows.Length > 0) cutoff = windows[^1].EndUtc;
        // Initialization needs 48 actual trading bars, including across maintenance/session breaks.
        if (windows.Length != count)
            return [];

        var retained = new Dictionary<DateTimeOffset, FuturesTradeSessionBarReadModel>();
        var sourceVersion = DatabentoIndicatorSeedSourceVersion.Create(registration, options.FeedOptions.Dataset);
        if (cache is not null)
        {
            var validation = new FuturesTradeSessionBarReadModelValidationRules();
            foreach (var group in windows.GroupBy(window => window.ValueDate))
            {
                var expected = group.ToDictionary(window => window.EndUtc);
                foreach (var ends in expected.Keys.Chunk(64))
                {
                IndicatorSeedRequestQualification.RequireSelection(sourceVersion, ends);
                var bars = await cache.MarketDataDb.GetQualifiedIndicatorSeedBarsAsync(contractId, timeFrame,
                    group.Key, ends, sourceVersion, token).ConfigureAwait(false);
                foreach (var bar in bars)
                {
                    if (expected.TryGetValue(bar.IntervalEndUtc, out var window)
                        && bar is { IsComplete: true, IsValid: true } && bar.IntervalStartUtc == window.StartUtc
                        && validation.Execute(bar).Length == 0)
                        retained.Add(bar.IntervalStartUtc, bar);
                }
                }
            }
            logger?.LogInformation("Indicator seed cache supplied {CachedBars} of {RequiredBars} bars for {ContractId} {TimeFrame}.",
                retained.Count, windows.Length, contractId, timeFrame);
        }

        // Live trade replay cannot cross the weekend. Read prior sessions through Historical,
        // leaving recent current-session bars to Live (Historical may not have those yet).
        var valueDate = calendar.GetValueDate(now);
        var sessionStart = calendar.IsTradingDate(valueDate) ? calendar.GetSession(valueDate).StartUtc : cutoff;
        var missing = windows.Where(window => !retained.ContainsKey(window.StartUtc)).ToArray();
        var historicalWindows = missing.Where(window => window.StartUtc < sessionStart
            || window.StartUtc < now.AddHours(-23)).ToArray();
        // CFE OHLCV is partitioned by on/off-market publisher. Their minute
        // open/close ordering cannot be recovered by merging OHLCV rows.
        // Use original trades, exactly as the live VX lane does.
        var useTrades = timeFrame == TimeFrameType.FifteenSeconds
            || StringComparer.Ordinal.Equals(registration.Dataset ?? options.FeedOptions.Dataset, "XCBF.PITCH");
        var historicalBars = useTrades
            ? await ReadHistoricalTradesAsync(registration, contractId, historicalWindows, cutoff, timeFrame, cache, sourceVersion, token).ConfigureAwait(false)
            : await ReadHistoricalAsync(registration, contractId, historicalWindows, cutoff, timeFrame, maximumCostUsd, cache, sourceVersion, token, emaBootstrap).ConfigureAwait(false);
        if (historicalBars.Length != historicalWindows.Length && cache is null)
            return [];
        foreach (var bar in historicalBars)
            retained.Add(bar.IntervalStartUtc, bar);
        var replayWindows = missing.Except(historicalWindows).ToArray();
        if (replayWindows.Length == 0)
            return retained.Count == windows.Length ? windows.Select(window => retained[window.StartUtc]).ToArray() : [];
        var replayStart = replayWindows[0].StartUtc;
        logger?.LogInformation("Indicator seed acquisition: ContractId={ContractId}; RetainedBars={RetainedBars}; LiveBars={LiveBars}; LiveStartUtc={LiveStartUtc}; CutoffUtc={CutoffUtc}",
            contractId, retained.Count, replayWindows.Length, replayStart, cutoff);
        var replayRequest = new SharedShortSeedReplay.Window(timeFrame, replayWindows, cutoff);
        var replayBars = timeFrame is TimeFrameType.FifteenSeconds or TimeFrameType.OneMinute
            ? await shortReplay.ReadAsync(contractId, sourceVersion, replayRequest,
                (requests, replayToken) => ReadReplayAsync(registration, contractId, requests, replayToken), token).ConfigureAwait(false)
            : (await ReadReplayAsync(registration, contractId, [replayRequest], token).ConfigureAwait(false))[0];
        await CacheBarsAsync(cache, sourceVersion, replayBars, token).ConfigureAwait(false);
        foreach (var bar in replayBars)
            retained.Add(bar.IntervalStartUtc, bar);
        return retained.Count == windows.Length ? windows.Select(window => retained[window.StartUtc]).ToArray() : [];
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadHistoricalAsync(
        DatabentoContractRegistration registration, string contractId,
        MarketSessionBounds[] windows, DateTimeOffset cutoff, TimeFrameType timeFrame, decimal maximumCostUsd,
        IDbContextFactory? cache, string sourceVersion, CancellationToken token, bool allowQuietMinutes = false)
    {
        if (windows.Length == 0) return [];
        if (historical is null)
        {
            logger?.LogWarning("Historical indicator seed provider unavailable for {ContractId}; no out-of-range Live replay will be attempted.", contractId);
            return [];
        }
        var result = new List<FuturesTradeSessionBarReadModel>(windows.Length);
        var validation = new FuturesTradeSessionBarReadModelValidationRules();
        var remainingBudget = maximumCostUsd;
        foreach (var selected in ContiguousRanges(windows))
        {
            var start = selected[0].StartUtc;
            var end = selected[^1].EndUtc;
            var minuteCount = selected.Sum(window => (int)(window.EndUtc - window.StartUtc).TotalMinutes);
            var request = new HistoricalProviderRequest
            {
                Dataset = registration.Dataset ?? options.FeedOptions.Dataset,
                Symbols = [registration.ProviderContractName], Schema = HistoricalDataSchema.OhlcvOneMinute,
                Symbology = HistoricalSymbology.RawSymbol, StartUtc = start, EndUtc = end,
                // Permit bounded exact redelivery without confusing it with a
                // second original minute. Conflicting records remain errors.
                RecordLimit = checked(minuteCount * 2 + 1),
                RequestHash = $"indicator-seed-v1|{sourceVersion}|{contractId}|{start:O}|{end:O}"
            };
            var estimate = await historical.EstimateAsync(request, token).ConfigureAwait(false);
            if (estimate.EstimatedCostUsd < 0 || estimate.EstimatedCostUsd > remainingBudget)
                throw new InvalidOperationException($"Indicator historical seed estimate ${estimate.EstimatedCostUsd} exceeds the remaining ${remainingBudget} acquisition budget; no data was purchased.");
            remainingBudget -= estimate.EstimatedCostUsd;
            var minutes = new Dictionary<DateTimeOffset, HistoricalProviderRecord>();
            await using var reader = await historical.OpenRangeAsync(request, 256, token).ConfigureAwait(false);
            var recordCount = 0;
            var sourceComplete = false;
            while (await reader.ReadNextAsync(token).ConfigureAwait(false) is { } batch)
            {
                sourceComplete = batch.IsFinal;
                foreach (var record in batch.Records)
                {
                    if (++recordCount > minuteCount * 2)
                        throw new InvalidOperationException("Indicator historical seed exceeded its bounded record limit.");
                    if (record.Kind != HistoricalProviderRecordKind.Ohlcv
                        || record.EventTimestampUtc < start || record.EventTimestampUtc >= end
                        || record.EventTimestampUtc.UtcTicks % TimeSpan.TicksPerMinute != 0
                        || record.Open <= 0 || record.CloseOrPrice <= 0 || record.VolumeOrSize < 0
                        || record.High < Math.Max(record.Open, record.CloseOrPrice)
                        || record.Low > Math.Min(record.Open, record.CloseOrPrice) || record.Low <= 0)
                        throw new InvalidOperationException("Invalid historical indicator seed minute.");
                    if (!minutes.TryAdd(record.EventTimestampUtc, record) && minutes[record.EventTimestampUtc] is { } previous
                        // OHLCV SourceSequence is the native reader's local row
                        // ordinal, not a provider revision. It cannot turn exact
                        // source redelivery into a conflicting market record.
                        && (previous with { SourceSequence = record.SourceSequence }) != record)
                        throw new InvalidOperationException($"Conflicting historical indicator seed minute for {contractId} at {record.EventTimestampUtc:O}: "
                            + $"previous instrument={previous.InstrumentId}, publisher={previous.PublisherId}, OHLCV={previous.Open}/{previous.High}/{previous.Low}/{previous.CloseOrPrice}/{previous.VolumeOrSize}; "
                            + $"incoming instrument={record.InstrumentId}, publisher={record.PublisherId}, OHLCV={record.Open}/{record.High}/{record.Low}/{record.CloseOrPrice}/{record.VolumeOrSize}.");
                }
            }
            if (allowQuietMinutes && !sourceComplete) throw new InvalidDataException("EMA history ended without a successful final source marker.");
            var qualified = new List<FuturesTradeSessionBarReadModel>(selected.Length);
            foreach (var window in selected)
            {
                var records = Enumerable.Range(0, (int)(window.EndUtc - window.StartUtc).TotalMinutes).Select(index =>
                    minutes.GetValueOrDefault(window.StartUtc.AddMinutes(index))).ToArray();
                if (!allowQuietMinutes && records.Any(record => record is null))
                {
                    logger?.LogWarning("Historical indicator seed incomplete: ContractId={ContractId}; IntervalStartUtc={IntervalStartUtc}", contractId, window.StartUtc);
                    if (cache is null) return [];
                    continue;
                }
                records = records.Where(record => record is not null).ToArray();
                if (records.Length == 0) continue;
                var series = MarketSeriesIdentity.ForContract(contractId);
                var bar = new FuturesTradeSessionBarReadModel
                {
                    MarketSeriesIdentity = series,
                    ObservationId = FuturesTradeSessionBarId.Create(series, timeFrame, window.EndUtc, 0),
                    ContractId = contractId, ValueDate = window.ValueDate, TimeFrame = timeFrame,
                    IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
                    Open = records[0]!.Open, High = records.Max(record => record!.High),
                    Low = records.Min(record => record!.Low), Close = records[^1]!.CloseOrPrice,
                    Volume = records.Sum(record => (decimal)record!.VolumeOrSize),
                    // OHLCV does not contain exact trade counts or price-volume sums. Do not invent them.
                    FirstMarketEventUtc = allowQuietMinutes ? records[0]!.EventTimestampUtc : window.StartUtc,
                    LastMarketEventUtc = allowQuietMinutes ? records[^1]!.EventTimestampUtc : window.EndUtc.AddMinutes(-1),
                    CalculatedAtUtc = cutoff, IsComplete = true, IsValid = true,
                    CalculationVersion = "databento-historical-minute-seed-v1",
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                };
                if (validation.Execute(bar).Length == 0)
                    qualified.Add(bar);
            }
            await CacheBarsAsync(cache, sourceVersion, qualified.ToArray(), token).ConfigureAwait(false);
            result.AddRange(qualified);
            logger?.LogInformation("Historical indicator seed acquired: ContractId={ContractId}; StartUtc={StartUtc}; EndUtc={EndUtc}; Bars={Bars}",
                contractId, start, end, selected.Length);
        }
        return result.ToArray();
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadHistoricalTradesAsync(
        DatabentoContractRegistration registration, string contractId, MarketSessionBounds[] windows,
        DateTimeOffset cutoff, TimeFrameType timeFrame, IDbContextFactory? cache, string sourceVersion, CancellationToken token)
    {
        if (windows.Length == 0 || historical is null) return [];
        const int recordLimit = 250_000;
        var series = MarketSeriesIdentity.ForContract(contractId);
        var validation = new FuturesTradeSessionBarReadModelValidationRules();
        var bars = new List<FuturesTradeSessionBarReadModel>(windows.Length);
        var cfe = StringComparer.Ordinal.Equals(registration.Dataset ?? options.FeedOptions.Dataset, "XCBF.PITCH");
        // One cold VX history can span several sessions. Repeating metadata,
        // entitlement and range acquisition for every session exhausts the RPC
        // deadline. A completed bounded source range can cover calendar gaps;
        // only trades within requested exchange intervals contribute to bars.
        var pending = new Stack<MarketSessionBounds[]>(cfe ? [windows] : ContiguousRanges(windows).Reverse());
        while (pending.TryPop(out var selected))
        {
            var start = selected[0].StartUtc;
            var end = selected[^1].EndUtc;
            var request = new HistoricalProviderRequest
            {
                Dataset = registration.Dataset ?? options.FeedOptions.Dataset,
                Symbols = [registration.ProviderContractName], Schema = HistoricalDataSchema.Trades,
                Symbology = HistoricalSymbology.RawSymbol, StartUtc = start, EndUtc = end,
                RecordLimit = recordLimit + 1,
                RequestHash = $"indicator-trade-seed-v1|{sourceVersion}|{contractId}|{start:O}|{end:O}"
            };
            var estimate = await historical.EstimateAsync(request, token).ConfigureAwait(false);
            if (estimate.EstimatedCostUsd != 0m)
                throw new InvalidOperationException($"Original-trade historical seed for {contractId} {timeFrame} requires a zero-dollar estimate; provider estimated ${estimate.EstimatedCostUsd}. No data was purchased.");
            if (estimate.EstimatedRecords > recordLimit && selected.Length > 1)
            {
                var middle = selected.Length / 2;
                pending.Push(selected[middle..]);
                pending.Push(selected[..middle]);
                continue;
            }
            var buckets = new BarBucket[selected.Length];
            var count = 0;
            var complete = false;
            await using var reader = await historical.OpenRangeAsync(request, 1024, token).ConfigureAwait(false);
            while (await reader.ReadNextAsync(token).ConfigureAwait(false) is { } batch)
            {
                complete = batch.IsFinal;
                count += batch.Records.Count;
                if (count > recordLimit)
                    throw new InvalidOperationException("Original-trade historical seed exceeded its bounded record limit.");
                foreach (var trade in batch.Records)
                {
                    if (trade.Kind != HistoricalProviderRecordKind.Trade || trade.CloseOrPrice <= 0 || trade.VolumeOrSize <= 0)
                        continue;
                    var index = RsiHistoricalSeedWindowModel.FindIntervalIndex(selected, trade.EventTimestampUtc);
                    if (index >= 0)
                        buckets[index].Add(trade.CloseOrPrice, checked((uint)trade.VolumeOrSize),
                            trade.EventTimestampUtc, checked((uint)trade.SourceSequence));
                }
            }
            if (!complete) throw new InvalidDataException("Historical seed trade range ended without a final source marker.");
            var qualified = new List<FuturesTradeSessionBarReadModel>(selected.Length);
            for (var index = 0; index < selected.Length; index++)
            {
                if (buckets[index].Count == 0)
                {
                    if (cache is null) return [];
                    continue;
                }
                var bar = buckets[index].ToObservation(series, contractId, selected[index], cutoff, timeFrame) with
                {
                    CalculationVersion = "databento-historical-trade-seed-v1",
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                };
                if (validation.Execute(bar).Length == 0)
                    qualified.Add(bar);
            }
            await CacheBarsAsync(cache, sourceVersion, qualified.ToArray(), token).ConfigureAwait(false);
            bars.AddRange(qualified);
        }
        return bars.ToArray();
    }

    static IEnumerable<MarketSessionBounds[]> ContiguousRanges(MarketSessionBounds[] windows)
    {
        var start = 0;
        for (var index = 1; index <= windows.Length; index++)
        {
            if (index < windows.Length && windows[index - 1].ValueDate == windows[index].ValueDate
                && windows[index - 1].EndUtc == windows[index].StartUtc)
                continue;
            yield return windows[start..index];
            start = index;
        }
    }

    static async Task CacheBarsAsync(IDbContextFactory? cache, string sourceVersion,
        FuturesTradeSessionBarReadModel[] bars, CancellationToken token)
    {
        if (cache is null || bars.Length == 0) return;
        foreach (var group in bars.GroupBy(bar => bar.ValueDate))
        {
            foreach (var batch in group.Chunk(64))
            {
            IndicatorSeedRequestQualification.RequireBatch(sourceVersion, batch);
            await cache.MarketDataDb.StoreQualifiedIndicatorSeedBarsAsync(sourceVersion, batch, token).ConfigureAwait(false);
            }
        }
    }

    async Task<FuturesTradeSessionBarReadModel[][]> ReadReplayAsync(DatabentoContractRegistration registration,
        string contractId, SharedShortSeedReplay.Window[] requests, CancellationToken token)
    {
        var accumulators = requests.Select(request => new ReplayAccumulator(request)).ToArray();
        var replayStart = requests.Min(request => request.Intervals[0].StartUtc);
        logger?.LogInformation("Opening one indicator replay for {ContractId} at {StartUtc} with {FrameCount} frame requests.",
            contractId, replayStart, requests.Length);
        await using var reader = replay.Open(registration, replayStart, TimeSpan.FromMinutes(2), token);
        var completed = false;
        while (await reader.ReadNextAsync(token).ConfigureAwait(false) is { } batch)
        {
            completed = batch.IsFinal;
            foreach (var row in batch.Records)
            {
                if (row.Kind != HistoricalProviderRecordKind.Trade || row.CloseOrPrice <= 0 || row.VolumeOrSize <= 0)
                    continue;
                foreach (var accumulator in accumulators)
                    accumulator.Add(row);
            }
        }
        if (!completed)
            throw new InvalidDataException("Native indicator replay ended without a successful replay-complete marker.");

        // Complete coverage is published only after the provider replay-complete
        // marker. A hole invalidates just the affected frame.
        var series = MarketSeriesIdentity.ForContract(contractId);
        var validation = new FuturesTradeSessionBarReadModelValidationRules();
        var results = new FuturesTradeSessionBarReadModel[requests.Length][];
        for (var requestIndex = 0; requestIndex < requests.Length; requestIndex++)
        {
            var request = requests[requestIndex];
            var current = accumulators[requestIndex].Buckets;
            results[requestIndex] = request.Intervals.Select((window, index) => (Window: window, Bucket: current[index]))
                .Where(value => value.Bucket.Count > 0)
                .Select(value => value.Bucket.ToObservation(series, contractId, value.Window, request.Cutoff, request.Frame))
                .Where(bar => validation.Execute(bar).Length == 0).ToArray();
            if (results[requestIndex].Length != request.Intervals.Length)
                logger?.LogWarning("Native initialization replay has missing bars; Contract={Contract} TimeFrame={TimeFrame}",
                    contractId, request.Frame);
        }
        return results;
    }

    sealed class ReplayAccumulator(SharedShortSeedReplay.Window request)
    {
        readonly MarketSessionBounds[] intervals = request.Intervals;
        readonly DateTimeOffset start = request.Intervals[0].StartUtc;
        readonly DateTimeOffset cutoff = request.Cutoff;
        int current;

        public BarBucket[] Buckets { get; } = new BarBucket[request.Intervals.Length];

        public void Add(HistoricalProviderRecord row)
        {
            var at = row.EventTimestampUtc;
            if (at < start || at >= cutoff) return;
            if (!Contains(intervals[current], at))
            {
                var next = current + 1;
                var index = next < intervals.Length && Contains(intervals[next], at)
                    ? next
                    : RsiHistoricalSeedWindowModel.FindIntervalIndex(intervals, at);
                if (index < 0) return;
                current = index;
            }
            Buckets[current].Add(row.CloseOrPrice, checked((uint)row.VolumeOrSize), at, checked((uint)row.SourceSequence));
        }

        static bool Contains(MarketSessionBounds interval, DateTimeOffset at) =>
            at >= interval.StartUtc && at < interval.EndUtc;
    }

    public struct BarBucket
    {
        public decimal Open, High, Low, Close, Volume, PriceVolume;
        public long Count, FirstSequence, LastSequence;
        public DateTimeOffset FirstTime, LastTime;

        public void Add(decimal price, uint size, DateTimeOffset time, uint sequence)
        {
            if (Count == 0)
            {
                Open = High = Low = price;
                FirstTime = LastTime = time;
                FirstSequence = LastSequence = sequence;
            }
            else
            {
                if (time < FirstTime || (time == FirstTime && sequence < FirstSequence))
                {
                    Open = price;
                    FirstTime = time;
                    FirstSequence = sequence;
                }
                if (time > LastTime || (time == LastTime && sequence > LastSequence))
                {
                    Close = price;
                    LastTime = time;
                    LastSequence = sequence;
                }
            }
            High = Math.Max(High, price);
            Low = Math.Min(Low, price);
            if (Count == 0)
                Close = price;
            Volume += size;
            PriceVolume += price * size;
            Count++;
        }

        public readonly FuturesTradeSessionBarReadModel ToObservation(
            MarketSeriesIdentity series, string contractId,
            MarketSessionBounds window, DateTimeOffset cutoff, TimeFrameType timeFrame = TimeFrameType.FiveMinutes) => new()
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(series,
                timeFrame, window.EndUtc, LastSequence),
            ContractId = contractId,
            ValueDate = window.ValueDate,
            TimeFrame = timeFrame,
            IntervalStartUtc = window.StartUtc,
            IntervalEndUtc = window.EndUtc,
            Open = Open,
            High = High,
            Low = Low,
            Close = Close,
            Volume = Volume,
            TradeCount = Count,
            PriceVolumeSum = PriceVolume,
            FirstSourceSequence = FirstSequence,
            LastSourceSequence = LastSequence,
            FirstMarketEventUtc = FirstTime,
            LastMarketEventUtc = LastTime,
            CalculatedAtUtc = cutoff,
            IsComplete = true,
            IsValid = true,
            CalculationVersion = "databento-four-hour-trade-replay-v1",
            CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
        };
    }
}
