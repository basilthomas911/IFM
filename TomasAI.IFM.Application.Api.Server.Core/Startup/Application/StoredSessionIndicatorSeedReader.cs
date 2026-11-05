using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

/// <summary>Reconstructs complete indicator seed bars from local original trades; no provider acquisition or fabricated OHLC is permitted.</summary>
public sealed class StoredSessionIndicatorSeedReader(IDbContextFactory databases, IMarketSessionCalendar calendar,
    TimeProvider clock, ILogger<StoredSessionIndicatorSeedReader> logger, IMarketDataApi? market = null)
{
    /// <summary>Reads one bounded sequence of completed trading intervals, or returns unavailable when any interval lacks trades.</summary>
    public async Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, TimeFrameType frame, int count, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 64);
        var watch = Stopwatch.StartNew();
        var cutoff = clock.GetUtcNow();
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
        var bars = new List<FuturesTradeSessionBarReadModel>(windows.Length);
        foreach (var window in windows)
        {
            token.ThrowIfCancellationRequested();
            var stored = await databases.MarketDataDb.GetStoredTradeSessionBarAsync(contractId, frame,
                window.ValueDate, window.EndUtc, token).ConfigureAwait(false);
            if (stored is not null && stored.IntervalStartUtc == window.StartUtc && stored.IntervalEndUtc == window.EndUtc
                && new FuturesTradeSessionBarReadModelValidationRules().Execute(stored).Length == 0)
            {
                bars.Add(stored);
                continue;
            }
            if (frame == TimeFrameType.FourHours && market is not null)
            {
                logger.LogInformation("Four-hour indicator history requires qualified native acquisition for {ContractId}; no full multi-session raw-tick scan will be materialized.", contractId);
                return await market.ReadIndicatorSeedBarsAsync(contractId, frame, token).ConfigureAwait(false);
            }
            var rows = new List<BootstrapTradeRow>();
            // A bounded receive-time overlap admits ordinary delayed writes;
            // qualification below uses exact original exchange timestamps.
            var until = window.EndUtc.AddMinutes(1);
            for (var cursor = window.StartUtc.AddMinutes(-1); cursor < until;)
            {
                var midnight = new DateTimeOffset(cursor.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
                var end = until < midnight ? until : midnight;
                var lower = TimeOnly.FromDateTime(cursor.UtcDateTime);
                var upper = end == midnight ? TimeOnly.MaxValue : TimeOnly.FromDateTime(end.UtcDateTime);
                rows.AddRange(await databases.TradeTickQuoteDb.GetRawFuturesTradeWindowAsync(contractId, window.ValueDate,
                    lower, upper, token).ConfigureAwait(false));
                cursor = end;
            }
            var bar = Aggregate(contractId, frame, window, cutoff, rows);
            if (bar is null)
            {
                logger.LogWarning("Stored indicator seed unavailable: {ContractId} {TimeFrame} {IntervalStartUtc}; no complete local trade interval.", contractId, frame, window.StartUtc);
                // Provider I/O stays in the native Host. It qualifies complete
                // replay and original minute OHLCV, with a zero-dollar ceiling.
                return market is null ? [] : await market.ReadIndicatorSeedBarsAsync(contractId, frame, token).ConfigureAwait(false);
            }
            bars.Add(bar);
        }
        logger.LogInformation("Stored indicator seed reconstructed {Count} {TimeFrame} bars for {ContractId} in {ElapsedMilliseconds} ms.", bars.Count, frame, contractId, watch.Elapsed.TotalMilliseconds);
        return bars.ToArray();
    }

    /// <summary>Builds exact OHLCV and source provenance, discarding duplicates, non-trades, and observations outside the requested interval.</summary>
    public static FuturesTradeSessionBarReadModel? Aggregate(string contractId, TimeFrameType frame,
        MarketSessionBounds window, DateTimeOffset cutoff, IEnumerable<BootstrapTradeRow> source)
    {
        var rows = source.Where(row => row.ValueDate == window.ValueDate && row.Action == (short)'T' && row.Price > 0 && row.Size > 0)
            .Select(row => (Row: row, Time: At(row.EventNanoseconds)))
            .Where(value => value.Time >= window.StartUtc && value.Time < window.EndUtc)
            .DistinctBy(value => (value.Row.PublisherId, value.Row.InstrumentId, value.Row.Sequence, value.Row.EventNanoseconds))
            .OrderBy(value => value.Time).ThenBy(value => value.Row.Sequence).ToArray();
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
