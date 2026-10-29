// Application bar identity, validation and window policy retained from the current Host.
// Socket/DBN processing is native and cancellable; no legacy feed factory is used.
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Seeding;

/// <summary>Combines bounded prior-session Historical OHLCV with read-only current-session Live replay into complete seed bars.</summary>
public sealed class NativeFuturesSeedReplay(
    NativeSeedReplaySource replay,
    DatabentoMarketDataRuntimeOptions options,
    IMarketSessionCalendar calendar,
    TimeProvider clock,
    ILogger<NativeFuturesSeedReplay>? logger = null,
    IMarketDataHistoricalProvider? historical = null)
{
    const int BarCount = 48;
    const decimal PriceScale = 1_000_000_000m;

    public async Task<FuturesTradeSessionBarReadModel[]> ReadAsync(
        string contractId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.FeedOptions.DataSource != FeedDataSourceMode.DatabentoLive)
            return [];
        var registration = options.Contracts.SingleOrDefault(value =>
            StringComparer.Ordinal.Equals(value.DomainContractId, contractId));
        if (registration is null)
            throw new InvalidOperationException($"No Databento registration exists for {contractId}.");

        var now = clock.GetUtcNow();
        var cutoff = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.FromMinutes(5).Ticks,
            TimeSpan.Zero);
        var windows = RsiHistoricalSeedWindowModel.Create(
            TimeFrameType.FiveMinutes, BarCount, cutoff, calendar);
        // Initialization needs 48 actual trading bars, including across maintenance/session breaks.
        if (windows.Length != BarCount)
            return [];

        // Live trade replay cannot cross the weekend. Read prior sessions through Historical,
        // leaving recent current-session bars to Live (Historical may not have those yet).
        var valueDate = calendar.GetValueDate(now);
        var sessionStart = calendar.IsTradingDate(valueDate) ? calendar.GetSession(valueDate).StartUtc : cutoff;
        var historicalWindows = windows.Where(window => window.StartUtc < sessionStart
            || window.StartUtc < now.AddHours(-23)).ToArray();
        var historicalBars = await ReadHistoricalAsync(registration, contractId,
            historicalWindows, cutoff, cancellationToken).ConfigureAwait(false);
        if (historicalBars.Length != historicalWindows.Length)
            return [];
        var replayWindows = windows.Except(historicalWindows).ToArray();
        if (replayWindows.Length == 0)
            return historicalBars;
        var replayStart = replayWindows[0].StartUtc;
        logger?.LogInformation("Indicator seed acquisition: ContractId={ContractId}; HistoricalBars={HistoricalBars}; LiveBars={LiveBars}; LiveStartUtc={LiveStartUtc}; CutoffUtc={CutoffUtc}",
            contractId, historicalBars.Length, replayWindows.Length, replayStart, cutoff);
        return await ReadReplayAsync(registration,contractId,windows,historicalBars,replayStart,cutoff,cancellationToken).ConfigureAwait(false);
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadHistoricalAsync(
        DatabentoContractRegistration registration, string contractId,
        MarketSessionBounds[] windows, DateTimeOffset cutoff, CancellationToken token)
    {
        if (windows.Length == 0) return [];
        if (historical is null)
        {
            logger?.LogWarning("Historical indicator seed provider unavailable for {ContractId}; no out-of-range Live replay will be attempted.", contractId);
            return [];
        }
        var result = new List<FuturesTradeSessionBarReadModel>(windows.Length);
        var remainingBudget = 0.10m;
        foreach (var group in windows.GroupBy(window => window.ValueDate))
        {
            var selected = group.ToArray();
            var start = selected[0].StartUtc;
            var end = selected[^1].EndUtc;
            var request = new HistoricalProviderRequest
            {
                Dataset = registration.Dataset ?? options.FeedOptions.Dataset,
                Symbols = [registration.ProviderContractName], Schema = HistoricalDataSchema.OhlcvOneMinute,
                Symbology = HistoricalSymbology.RawSymbol, StartUtc = start, EndUtc = end,
                RecordLimit = selected.Length * 5 + 1,
                RequestHash = $"indicator-seed-v1|{contractId}|{start:O}|{end:O}"
            };
            var estimate = await historical.EstimateAsync(request, token).ConfigureAwait(false);
            if (estimate.EstimatedCostUsd < 0 || estimate.EstimatedCostUsd > remainingBudget)
                throw new InvalidOperationException("Indicator historical seed exceeds the $0.10 acquisition budget.");
            remainingBudget -= estimate.EstimatedCostUsd;
            var minutes = new Dictionary<DateTimeOffset, HistoricalProviderRecord>();
            await using var reader = await historical.OpenRangeAsync(request, 256, token).ConfigureAwait(false);
            var recordCount = 0;
            while (await reader.ReadNextAsync(token).ConfigureAwait(false) is { } batch)
                foreach (var record in batch.Records)
                {
                    if (++recordCount > selected.Length * 5)
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
                var records = Enumerable.Range(0, 5).Select(index =>
                    minutes.GetValueOrDefault(window.StartUtc.AddMinutes(index))).ToArray();
                if (records.Any(record => record is null))
                {
                    logger?.LogWarning("Historical indicator seed incomplete: ContractId={ContractId}; IntervalStartUtc={IntervalStartUtc}", contractId, window.StartUtc);
                    return [];
                }
                var series = MarketSeriesIdentity.ForContract(contractId);
                result.Add(new()
                {
                    MarketSeriesIdentity = series,
                    ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.FiveMinutes, window.EndUtc, 0),
                    ContractId = contractId, ValueDate = window.ValueDate, TimeFrame = TimeFrameType.FiveMinutes,
                    IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
                    Open = records[0]!.Open, High = records.Max(record => record!.High),
                    Low = records.Min(record => record!.Low), Close = records[^1]!.CloseOrPrice,
                    Volume = records.Sum(record => (decimal)record!.VolumeOrSize),
                    // OHLCV does not contain exact trade counts or price-volume sums. Do not invent them.
                    FirstMarketEventUtc = window.StartUtc, LastMarketEventUtc = window.EndUtc.AddMinutes(-1),
                    CalculatedAtUtc = cutoff, IsComplete = true, IsValid = true,
                    CalculationVersion = "databento-historical-minute-seed-v1",
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                });
            }
            logger?.LogInformation("Historical indicator seed acquired: ContractId={ContractId}; StartUtc={StartUtc}; EndUtc={EndUtc}; Bars={Bars}",
                contractId, start, end, selected.Length);
        }
        return result.ToArray();
    }

    async Task<FuturesTradeSessionBarReadModel[]> ReadReplayAsync(DatabentoContractRegistration registration,string contractId,
        MarketSessionBounds[] windows,FuturesTradeSessionBarReadModel[] historicalBars,DateTimeOffset replayStart,DateTimeOffset cutoff,CancellationToken token)
    {
        var buckets=new BarBucket[BarCount];
        await using var reader=replay.Open(registration,replayStart,TimeSpan.FromMinutes(2),token);
        while(await reader.ReadNextAsync(token).ConfigureAwait(false) is{} batch)
            foreach(var row in batch.Records)
            {
                if(row.Kind!=HistoricalProviderRecordKind.Trade||row.CloseOrPrice<=0||row.VolumeOrSize<=0)continue;
                var at=row.EventTimestampUtc;
                if(at<windows[0].StartUtc||at>=cutoff)continue;
                var index=RsiHistoricalSeedWindowModel.FindIntervalIndex(windows,at);
                if((uint)index>=BarCount||at<windows[index].StartUtc||at>=windows[index].EndUtc)continue;
                buckets[index].Add(row.CloseOrPrice,checked((uint)row.VolumeOrSize),at,checked((uint)row.SourceSequence));
            }
        // A successful final read proves the explicit provider replay-complete
        // marker. Truncated transport never produces apparently complete bars.
        var retained=historicalBars.ToDictionary(bar=>bar.IntervalStartUtc);
        if(windows.Where((window,index)=>!retained.ContainsKey(window.StartUtc)&&buckets[index].Count==0).Any())
        {logger?.LogWarning("Native initialization replay has missing bars; Contract={Contract}",contractId);return [];}
        var series=MarketSeriesIdentity.ForContract(contractId);
        var bars=windows.Select((window,index)=>retained.TryGetValue(window.StartUtc,out var saved)?saved:buckets[index].ToObservation(series,contractId,window,cutoff)).ToArray();
        var validation=new FuturesTradeSessionBarReadModelValidationRules();
        return bars.All(bar=>validation.Execute(bar).Length==0)?bars:[];
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
            MarketSessionBounds window, DateTimeOffset cutoff) => new()
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(series,
                TimeFrameType.FiveMinutes, window.EndUtc, LastSequence),
            ContractId = contractId,
            ValueDate = window.ValueDate,
            TimeFrame = TimeFrameType.FiveMinutes,
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
