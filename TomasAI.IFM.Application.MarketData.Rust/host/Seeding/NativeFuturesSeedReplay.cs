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
    readonly ConcurrentDictionary<string, SemaphoreSlim> fourHourAcquisitions = new(StringComparer.Ordinal);

    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, CancellationToken cancellationToken)
        => ReadCoreAsync(contractId, TimeFrameType.FiveMinutes, 0.10m, cancellationToken);

    /// <summary>Repairs intraday seed coverage without authorizing a paid historical acquisition.</summary>
    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, TimeFrameType timeFrame, CancellationToken cancellationToken)
        => ReadCoreAsync(contractId, timeFrame, 0m, cancellationToken);

    async Task<FuturesTradeSessionBarReadModel[]> ReadCoreAsync(
        string contractId, TimeFrameType timeFrame, decimal maximumCostUsd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeFrame is not (TimeFrameType.FiveMinutes or TimeFrameType.FifteenMinutes or TimeFrameType.OneHour or TimeFrameType.FourHours))
            throw new ArgumentOutOfRangeException(nameof(timeFrame));
        if (options.FeedOptions.DataSource != FeedDataSourceMode.DatabentoLive)
            return [];
        var registration = options.Contracts.SingleOrDefault(value =>
            StringComparer.Ordinal.Equals(value.DomainContractId, contractId));
        if (registration is null)
            throw new InvalidOperationException($"No Databento registration exists for {contractId}.");

        if (timeFrame != TimeFrameType.FourHours || databases is null)
            return await ReadSeedAsync(registration, contractId, timeFrame, maximumCostUsd, null, cancellationToken).ConfigureAwait(false);

        // Waiters recheck the durable coverage after the owner finishes. A timed-out
        // caller cannot start a competing replay or erase previously qualified bars.
        var acquisition = fourHourAcquisitions.GetOrAdd(contractId, static _ => new SemaphoreSlim(1));
        await acquisition.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadSeedAsync(registration, contractId, timeFrame, maximumCostUsd, databases, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            acquisition.Release();
        }
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadSeedAsync(DatabentoContractRegistration registration,
        string contractId, TimeFrameType timeFrame, decimal maximumCostUsd, IDbContextFactory? cache, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        var cutoff = now;
        var windows = RsiHistoricalSeedWindowModel.Create(
            timeFrame, BarCount, cutoff, calendar);
        if (windows.Length > 0) cutoff = windows[^1].EndUtc;
        // Initialization needs 48 actual trading bars, including across maintenance/session breaks.
        if (windows.Length != BarCount)
            return [];

        var retained = new Dictionary<DateTimeOffset, FuturesTradeSessionBarReadModel>();
        if (cache is not null)
        {
            var validation = new FuturesTradeSessionBarReadModelValidationRules();
            foreach (var window in windows)
            {
                var bar = await cache.MarketDataDb.GetStoredTradeSessionBarAsync(contractId, timeFrame,
                    window.ValueDate, window.EndUtc, token).ConfigureAwait(false);
                if (bar is { IsComplete: true, IsValid: true } && bar.IntervalStartUtc == window.StartUtc
                    && bar.IntervalEndUtc == window.EndUtc && validation.Execute(bar).Length == 0)
                    retained.Add(window.StartUtc, bar);
            }
            logger?.LogInformation("Four-hour seed cache supplied {CachedBars} of {RequiredBars} bars for {ContractId}.",
                retained.Count, windows.Length, contractId);
        }

        // Live trade replay cannot cross the weekend. Read prior sessions through Historical,
        // leaving recent current-session bars to Live (Historical may not have those yet).
        var valueDate = calendar.GetValueDate(now);
        var sessionStart = calendar.IsTradingDate(valueDate) ? calendar.GetSession(valueDate).StartUtc : cutoff;
        var missing = windows.Where(window => !retained.ContainsKey(window.StartUtc)).ToArray();
        var historicalWindows = missing.Where(window => window.StartUtc < sessionStart
            || window.StartUtc < now.AddHours(-23)).ToArray();
        var historicalBars = await ReadHistoricalAsync(registration, contractId,
            historicalWindows, cutoff, timeFrame, maximumCostUsd, cache, token).ConfigureAwait(false);
        if (historicalBars.Length != historicalWindows.Length)
            return [];
        foreach (var bar in historicalBars)
            retained.Add(bar.IntervalStartUtc, bar);
        var replayWindows = missing.Except(historicalWindows).ToArray();
        if (replayWindows.Length == 0)
            return windows.Select(window => retained[window.StartUtc]).ToArray();
        var replayStart = replayWindows[0].StartUtc;
        logger?.LogInformation("Indicator seed acquisition: ContractId={ContractId}; RetainedBars={RetainedBars}; LiveBars={LiveBars}; LiveStartUtc={LiveStartUtc}; CutoffUtc={CutoffUtc}",
            contractId, retained.Count, replayWindows.Length, replayStart, cutoff);
        var replayBars = await ReadReplayAsync(registration, contractId, replayWindows, replayStart, cutoff, timeFrame, token).ConfigureAwait(false);
        foreach (var bar in replayBars)
        {
            await CacheBarAsync(cache, bar, token).ConfigureAwait(false);
            retained.Add(bar.IntervalStartUtc, bar);
        }
        return retained.Count == windows.Length ? windows.Select(window => retained[window.StartUtc]).ToArray() : [];
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadHistoricalAsync(
        DatabentoContractRegistration registration, string contractId,
        MarketSessionBounds[] windows, DateTimeOffset cutoff, TimeFrameType timeFrame, decimal maximumCostUsd,
        IDbContextFactory? cache, CancellationToken token)
    {
        if (windows.Length == 0) return [];
        if (historical is null)
        {
            logger?.LogWarning("Historical indicator seed provider unavailable for {ContractId}; no out-of-range Live replay will be attempted.", contractId);
            return [];
        }
        var result = new List<FuturesTradeSessionBarReadModel>(windows.Length);
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
                RecordLimit = minuteCount + 1,
                RequestHash = $"indicator-seed-v1|{contractId}|{start:O}|{end:O}"
            };
            var estimate = await historical.EstimateAsync(request, token).ConfigureAwait(false);
            if (estimate.EstimatedCostUsd < 0 || estimate.EstimatedCostUsd > remainingBudget)
                throw new InvalidOperationException($"Indicator historical seed estimate ${estimate.EstimatedCostUsd} exceeds the remaining ${remainingBudget} acquisition budget; no data was purchased.");
            remainingBudget -= estimate.EstimatedCostUsd;
            var minutes = new Dictionary<DateTimeOffset, HistoricalProviderRecord>();
            await using var reader = await historical.OpenRangeAsync(request, 256, token).ConfigureAwait(false);
            var recordCount = 0;
            while (await reader.ReadNextAsync(token).ConfigureAwait(false) is { } batch)
                foreach (var record in batch.Records)
                {
                    if (++recordCount > minuteCount)
                        throw new InvalidOperationException("Indicator historical seed exceeded its bounded record limit.");
                    if (record.Kind != HistoricalProviderRecordKind.Ohlcv
                        || record.EventTimestampUtc < start || record.EventTimestampUtc >= end
                        || record.EventTimestampUtc.UtcTicks % TimeSpan.TicksPerMinute != 0
                        || record.Open <= 0 || record.CloseOrPrice <= 0 || record.VolumeOrSize < 0
                        || record.High < Math.Max(record.Open, record.CloseOrPrice)
                        || record.Low > Math.Min(record.Open, record.CloseOrPrice) || record.Low <= 0)
                        throw new InvalidOperationException("Invalid historical indicator seed minute.");
                    if (!minutes.TryAdd(record.EventTimestampUtc, record))
                        throw new InvalidOperationException("Duplicate historical indicator seed minute.");
                }
            foreach (var window in selected)
            {
                var records = Enumerable.Range(0, (int)(window.EndUtc - window.StartUtc).TotalMinutes).Select(index =>
                    minutes.GetValueOrDefault(window.StartUtc.AddMinutes(index))).ToArray();
                if (records.Any(record => record is null))
                {
                    logger?.LogWarning("Historical indicator seed incomplete: ContractId={ContractId}; IntervalStartUtc={IntervalStartUtc}", contractId, window.StartUtc);
                    return [];
                }
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
                    FirstMarketEventUtc = window.StartUtc, LastMarketEventUtc = window.EndUtc.AddMinutes(-1),
                    CalculatedAtUtc = cutoff, IsComplete = true, IsValid = true,
                    CalculationVersion = "databento-historical-minute-seed-v1",
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                };
                await CacheBarAsync(cache, bar, token).ConfigureAwait(false);
                result.Add(bar);
            }
            logger?.LogInformation("Historical indicator seed acquired: ContractId={ContractId}; StartUtc={StartUtc}; EndUtc={EndUtc}; Bars={Bars}",
                contractId, start, end, selected.Length);
        }
        return result.ToArray();
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

    static async ValueTask CacheBarAsync(IDbContextFactory? cache, FuturesTradeSessionBarReadModel bar, CancellationToken token)
    {
        if (cache is not null)
            _ = await ((IHistoricalObservationStore)cache.MarketDataDb).TryWriteObservationAsync(bar, token).ConfigureAwait(false);
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadReplayAsync(DatabentoContractRegistration registration,string contractId,
        MarketSessionBounds[] windows,DateTimeOffset replayStart,DateTimeOffset cutoff,TimeFrameType timeFrame,CancellationToken token)
    {
        var buckets=new BarBucket[windows.Length];
        await using var reader=replay.Open(registration,replayStart,TimeSpan.FromMinutes(2),token);
        while(await reader.ReadNextAsync(token).ConfigureAwait(false) is{} batch)
            foreach(var row in batch.Records)
            {
                if(row.Kind!=HistoricalProviderRecordKind.Trade||row.CloseOrPrice<=0||row.VolumeOrSize<=0)continue;
                var at=row.EventTimestampUtc;
                if(at<windows[0].StartUtc||at>=cutoff)continue;
                var index=RsiHistoricalSeedWindowModel.FindIntervalIndex(windows,at);
                if((uint)index>=windows.Length||at<windows[index].StartUtc||at>=windows[index].EndUtc)continue;
                buckets[index].Add(row.CloseOrPrice,checked((uint)row.VolumeOrSize),at,checked((uint)row.SourceSequence));
            }
        // A successful final read proves the explicit provider replay-complete
        // marker. Truncated transport never produces apparently complete bars.
        var series=MarketSeriesIdentity.ForContract(contractId);
        var validation=new FuturesTradeSessionBarReadModelValidationRules();
        var bars = windows.Select((window, index) => (Window: window, Bucket: buckets[index]))
            .Where(value => value.Bucket.Count > 0)
            .Select(value => value.Bucket.ToObservation(series, contractId, value.Window, cutoff, timeFrame))
            .Where(bar => validation.Execute(bar).Length == 0).ToArray();
        if (bars.Length != windows.Length)
            logger?.LogWarning("Native initialization replay has missing bars; Contract={Contract}",contractId);
        return bars;
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
