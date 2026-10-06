using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Reads a separate, read-only Live replay into complete five-minute seed bars.</summary>
public sealed class FourHourDatabentoSeedReplay(
    IDatabentoFeedFactory feeds,
    DatabentoMarketDataRuntimeOptions options,
    IMarketSessionCalendar calendar,
    TimeProvider clock,
    ILogger<FourHourDatabentoSeedReplay>? logger = null)
{
    const int BarCount = 48;
    const decimal PriceScale = 1_000_000_000m;

    public async Task<FuturesTradeSessionBarReadModel[]> ReadAsync(
        string contractId, CancellationToken cancellationToken)
    {
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

        var replayStart = windows[0].StartUtc;
        var feedOptions = options.FeedOptions with
        {
            Dataset = registration.Dataset ?? options.FeedOptions.Dataset,
            TradeReplayStartTimestampNanoseconds = checked((ulong)
                (replayStart.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL),
            StatisticsReplayStartTimestampNanoseconds = 0
        };
        return await Task.Run(() => ReadReplay(feedOptions, registration, contractId,
            windows, cutoff, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    FuturesTradeSessionBarReadModel[] ReadReplay(
        DatabentoFeedOptions feedOptions,
        DatabentoContractRegistration registration,
        string contractId,
        MarketSessionBounds[] windows,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        using var feed = feeds.CreateTickerFeed(feedOptions);
        feed.Subscribe([new TickerSubscription(registration.ProviderContractName,
            DatabentoInputSymbology.RawSymbol,
            MarketDataKinds.Trade | MarketDataKinds.SessionVolume)], options.ProviderQueryTimeout);
        ISynchronousBatchReader<MarketDataBatch64>? reader = null;
        var started = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            feed.Start(options.FeedStartTimeout, _ =>
            {
                var instrument = feed.GetInstruments().Single();
                reader = feed.GetReader(instrument.Instrument);
            });
            started = true;
            var buckets = new BarBucket[BarCount];
            var deadline = clock.GetUtcNow().AddMinutes(2);
            var completed = false;
            while (!completed && clock.GetUtcNow() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!reader!.TryRead(TimeSpan.FromMilliseconds(250), out var batch) || batch is null)
                {
                    if (reader.IsCompleted)
                        break;
                    continue;
                }
                using (batch)
                {
                    foreach (ref readonly var record in batch.Records)
                    {
                        if (record.Header.RecordKind == MarketRecordKind.TradeReplayComplete)
                        {
                            completed = true;
                            break;
                        }
                        if (record.Header.RecordKind != MarketRecordKind.Trade
                            || (record.Header.Flags & 2) == 0
                            || record.Trade.Price <= 0 || record.Trade.Size == 0)
                            continue;
                        var time = DateTimeOffset.UnixEpoch.AddTicks(
                            record.Header.EventTimestampNanoseconds / 100);
                        if (time < windows[0].StartUtc || time >= cutoff)
                            continue;
                        var index = RsiHistoricalSeedWindowModel.FindIntervalIndex(windows, time);
                        if ((uint)index >= BarCount || time < windows[index].StartUtc
                            || time >= windows[index].EndUtc)
                            continue;
                        buckets[index].Add(record.Trade.Price / PriceScale, record.Trade.Size,
                            time, record.Header.Sequence);
                    }
                }
            }
            if (!completed || buckets.Any(static bucket => bucket.Count == 0))
            {
                logger?.LogWarning("{Method} initialization replay incomplete; ContractId={ContractId}; StartUtc={StartUtc}; CutoffUtc={CutoffUtc}; ReplayCompleted={ReplayCompleted}; PopulatedBars={PopulatedBars}; RequiredBars={RequiredBars}",
                    nameof(ReadReplay), contractId, windows[0].StartUtc, cutoff, completed, buckets.Count(bucket => bucket.Count > 0), BarCount);
                return [];
            }
            var series = MarketSeriesIdentity.ForContract(contractId);
            var bars = windows.Select((window, index) => buckets[index].ToObservation(
                series, contractId, window, cutoff)).ToArray();
            var validation = new FuturesTradeSessionBarReadModelValidationRules();
            return bars.All(bar => validation.Execute(bar).Length == 0) ? bars : [];
        }
        finally
        {
            if (started)
                feed.Stop(options.FeedStopTimeout);
        }
    }

    internal struct BarBucket
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
