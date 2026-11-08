using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

/// <summary>Restores the Daily, Weekly and Monthly strategy inputs through normal indicator actors and qualified original history.</summary>
public sealed class StoredSessionIndicatorWarmup(StoredSessionIndicatorSeedReader reader, IActorService actors,
    IMarketDataApi market, IDbContextFactory databases, IMarketSessionCalendar calendar, TimeProvider clock,
    ILogger<StoredSessionIndicatorWarmup> logger, IHostApplicationLifetime? lifetime = null)
{
    readonly System.Collections.Concurrent.ConcurrentDictionary<(DateOnly Date, string ContractId, TimeFrameType Frame), DateTimeOffset> completed = new();
    readonly System.Collections.Concurrent.ConcurrentDictionary<(DateOnly Date, string ContractId, TimeFrameType Frame), byte> shortSeeds = new();
    readonly BoundedInFlight<(DateOnly Date, string ContractId, TimeFrameType Frame), bool> initializing =
        new(64, TimeSpan.FromMinutes(4), lifetime?.ApplicationStopping ?? default);
    static readonly System.Diagnostics.Metrics.Meter StartupMeter = new("TomasAI.IFM.IndicatorStartup", "1.0");
    static readonly System.Diagnostics.Metrics.Histogram<double> GateWait = StartupMeter.CreateHistogram<double>("indicator.startup.gate_wait", "ms");
    readonly System.Collections.Concurrent.ConcurrentDictionary<(DateOnly Date, string ContractId, TimeFrameType Frame), SeedProgress> seedProgress = new();
    sealed record SeedProgress(string Revision)
    {
        public System.Collections.Concurrent.ConcurrentDictionary<string, byte> Completed { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>Seeds a fast timeframe before its live processors attach.</summary>
    public async Task<bool> WarmShortFrameAsync(string contractId, DateOnly date, TimeFrameType frame, CancellationToken token)
    {
        if (frame is not (TimeFrameType.FifteenSeconds or TimeFrameType.OneMinute))
            throw new ArgumentOutOfRangeException(nameof(frame));
        try
        {
            TrimRetainedState(date);
            var key = (date, contractId, frame);
            if (shortSeeds.ContainsKey(key)) return true;
            return await initializing.RunAsync(key, async ownerToken =>
            {
                if (shortSeeds.ContainsKey(key)) return true;
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
                budget.CancelAfter(TimeSpan.FromSeconds(90));
                if (!await WarmFrameAsync(contractId, date, frame, budget.Token, seedShortFrame: true).ConfigureAwait(false)) return false;
                shortSeeds.TryAdd(key, 0);
                TrimRetainedState(date);
                logger.LogInformation("Historical startup seed verified for {ContractId} {TimeFrame} on {ValueDate}: RSI13/14, ADX, ATR, MACD and Bollinger state.", contractId, frame, date);
                return true;
            }, token).ConfigureAwait(false);
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            logger.LogWarning(error, "Historical startup seed failed for {ContractId} {TimeFrame} on {ValueDate}.", contractId, frame, date);
            return false;
        }
    }

    /// <summary>Restores session baselines and reconciles each newly closed 15-minute and hourly interval.</summary>
    public async Task<bool> WarmAsync(DateOnly date, CancellationToken token)
    {
        TrimRetainedState(date);
        if (!market.TryGetOnTheRunFuturesContract("ES", out var contract)) return false;
        // At most four independent timeframe attempts run at once. Each attempt
        // still sends its own indicator commands in order.
        TimeFrameType[] frames = [TimeFrameType.FifteenMinutes, TimeFrameType.OneHour, TimeFrameType.Daily, TimeFrameType.FourHours];
        var cutoff = clock.GetUtcNow().AddSeconds(-30);
        var pending = frames.Select(frame => (Frame: frame, Through: frame is TimeFrameType.FifteenMinutes or TimeFrameType.OneHour
                ? RsiHistoricalSeedWindowModel.Create(frame, 1, cutoff, calendar).LastOrDefault().EndUtc
                : DateTimeOffset.MinValue))
            .Where(target => !completed.TryGetValue((date, contract.ContractId, target.Frame), out var through)
                || target.Through > through).ToArray();
        var fastFrames = Task.WhenAll(
            WarmShortFrameAsync(contract.ContractId, date, TimeFrameType.FifteenSeconds, token),
            WarmShortFrameAsync(contract.ContractId, date, TimeFrameType.OneMinute, token));
        var results = await Task.WhenAll(pending.Select(target => TryWarmFrameAsync(target.Frame))).ConfigureAwait(false);
        for (var index = 0; index < pending.Length; index++)
            if (results[index]) completed[(date, contract.ContractId, pending[index].Frame)] = pending[index].Through;
        TrimRetainedState(date);
        return (await fastFrames.ConfigureAwait(false)).All(static result => result) && results.All(static result => result);

        async Task<bool> TryWarmFrameAsync(TimeFrameType frame)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
            // Four-hour recovery can use the host's full three-minute RPC budget,
            // with time left to restore the indicators after the bars arrive.
            budget.CancelAfter(TimeSpan.FromMinutes(frame == TimeFrameType.FourHours ? 4 : 2));
            try
            {
                if (await initializing.RunAsync((date, contract.ContractId, frame),
                    owner => WarmFrameAsync(contract.ContractId, date, frame, owner), budget.Token).ConfigureAwait(false))
                {
                    logger.LogInformation("Stored history restored {ContractId} {TimeFrame} RSI13/14, ADX, ATR, MACD, warm Bollinger and structure inputs for {ValueDate}.", contract.ContractId, frame, date);
                    return true;
                }
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                logger.LogWarning(error, "Stored {TimeFrame} indicator restoration failed for {ContractId}: {Failure}.", frame, contract.ContractId, error.Message);
            }
            return false;
        }
    }

    /// <summary>Bounds restart hints even when many contracts or out-of-order dates are requested. Eviction only causes an idempotent requalification.</summary>
    void TrimRetainedState(DateOnly date)
    {
        TrimRetained(completed, date);
        TrimRetained(shortSeeds, date);
        TrimRetained(seedProgress, date);
    }

    internal static void TrimRetained<T>(System.Collections.Concurrent.ConcurrentDictionary<(DateOnly Date, string ContractId, TimeFrameType Frame), T> entries,
        DateOnly date)
    {
        const int capacity = 512;
        foreach (var key in entries.Keys.Where(key => key.Date < date.AddDays(-7))) entries.TryRemove(key, out _);
        var excess = entries.Count - capacity;
        if (excess <= 0) return;
        foreach (var key in entries.Keys.OrderBy(key => key.Date).ThenBy(key => key.ContractId, StringComparer.Ordinal).ThenBy(key => key.Frame).Take(excess))
            entries.TryRemove(key, out _);
    }

    async Task<bool> WarmFrameAsync(string contractId, DateOnly date, TimeFrameType frame, CancellationToken token, bool seedShortFrame = false)
    {
        // Independent frames each retain their own admission. A slow frame cannot occupy
        // every permit and prevent the others starting. Six startup frames admit at most twelve commands.
        using var initializeCommands = new SemaphoreSlim(2, 2);
        if (frame == TimeFrameType.Daily)
        {
            var vx = await databases.MarketDataDb.GetLatestFuturesVxTermStructureSignalAsync(
                date, new FuturesVxTermStructureConfiguration().ConfigurationId, token).ConfigureAwait(false);
            if (vx is { IsWarm: true, IsValid: true })
                RegimeDiscoverySignalCacheAdapter.Publish(vx);
        }
        FuturesTradeSessionBarReadModel[]? acquiredSeed = null;
        var cutoff = clock.GetUtcNow();
        if (!seedShortFrame && await RestoreCommittedIndicatorsAsync(databases, contractId, date, frame, token).ConfigureAwait(false) is { } oldestObservation)
        {
            var latestStart = RsiHistoricalSeedWindowModel.Create(frame, 1, cutoff, calendar)[0].StartUtc;
            if (oldestObservation >= latestStart)
            {
            // Market structure needs the current bar and the preceding 20 completed bars.
            var structure = await reader.ReadAtAsync(contractId, frame, 21, cutoff, token).ConfigureAwait(false);
            if (structure.Length >= 21 && oldestObservation >= structure[^1].IntervalStartUtc)
            {
                RegimeDiscoverySignalCacheAdapter.RestoreBars(structure);
                return true;
            }
            if (structure.Length == 48)
                acquiredSeed = structure;
            }
        }
        var bars = acquiredSeed ?? await reader.ReadAtAsync(contractId, frame, 48, cutoff, token).ConfigureAwait(false);
        if (bars.Length != 48) return false;
        var revision = SeedRevision(bars);
        var progress = seedProgress.AddOrUpdate((date, contractId, frame), _ => new(revision),
            (_, previous) => previous.Revision == revision ? previous : new(revision));
        var activation = new FuturesIntradaySignalActivation(frame, new(contractId, date, frame, 13),
            new(contractId, date, frame, 14), new(contractId, date, frame, 14), new(contractId, date, frame));
        var work = new List<Task>();
        foreach (var period in new[] { 13, 14 })
        {
            var id = activation.Rsi with { PeriodLength = period };
            work.Add(StageAsync("RSI" + period, async () => Require(await actors.RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(new(id)
            {
                CommandId = Guid.CreateVersion7(), PostEvents = true,
                Subject = new(ActorType.Command, StartFuturesRsiSignalCommand.Actor, StartFuturesRsiSignalCommand.Verb, id.Format()),
                HistoricalSeed = new(48, bars[^1].IntervalEndUtc, bars, "RSI.STORED_TRADES_SEED"),
                ForceHistoricalInitialization = true
            }, token).ConfigureAwait(false))));
        }
        work.Add(StageAsync("ADX", async () => Require(await actors.RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(new(activation.Adx)
        {
            CommandId = Guid.CreateVersion7(), PostEvents = true, HistoricalSeed = bars,
            Subject = new(ActorType.Command, StartFuturesAdxSignalCommand.Actor, StartFuturesAdxSignalCommand.Verb, activation.Adx.Format())
        }, token).ConfigureAwait(false))));
        work.Add(StageAsync("ATR", async () =>
        {
        if (frame == TimeFrameType.Daily)
        {
            // Daily ATR owns a day-based stream and its existing Daily
            // command family, rather than the session-scoped intraday Start.
            foreach (var bar in bars)
            {
                var id = new FuturesAtrSignalId(contractId, bar.ValueDate, frame, 14,
                    TimeOnly.FromDateTime(bar.LastMarketEventUtc.UtcDateTime));
                var command = new GenerateFuturesAtrDailySignalCommand(id, bar.Close, bar)
                {
                    CommandId = Guid.CreateVersion7(), PostEvents = true,
                    Subject = new(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor,
                        GenerateFuturesAtrDailySignalCommand.Verb, id.ToDailyEntityId().Format())
                };
                Require(await actors.RequestAsync<GenerateFuturesAtrDailySignalCommand, FuturesAtrDailySignalEntityId>(command, token).ConfigureAwait(false));
            }
        }
        else
            Require(await actors.RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(new(activation.Atr)
            {
                CommandId = Guid.CreateVersion7(), PostEvents = true, HistoricalSeed = bars,
                Subject = new(ActorType.Command, StartFuturesAtrSignalCommand.Actor, StartFuturesAtrSignalCommand.Verb, activation.Atr.Format())
            }, token).ConfigureAwait(false));
        }));
        work.Add(StageAsync("MACD", async () => Require(await actors.RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(new(activation.Macd)
        {
            CommandId = Guid.CreateVersion7(), PostEvents = true, HistoricalSeed = bars,
            Subject = new(ActorType.Command, StartFuturesMacdSignalCommand.Actor, StartFuturesMacdSignalCommand.Verb, activation.Macd.Format())
        }, token).ConfigureAwait(false))));
        await Task.WhenAll(work).ConfigureAwait(false);
        // Actor acceptance includes the transactional read-model write. Use
        // it here so structure restoration does not race the event projector.
        var atr = await databases.MarketDataDb.GetLastFuturesAtrSignalAsync(contractId,
            frame == TimeFrameType.Daily ? bars[^1].ValueDate : date, frame, 14, token).ConfigureAwait(false);
        if (atr is not { IsWarm: true, AtrRatio: not null })
            throw new InvalidOperationException("Stored ATR initialization has not produced a warm read model.");
        RegimeDiscoverySignalCacheAdapter.Publish(atr);
        if (seedShortFrame)
            logger.LogInformation("Historical ATR seed committed for {ContractId} {TimeFrame} on {ValueDate}: {Count} bars through {Through}, ATR {Atr}, baseline {Baseline}.",
                contractId, frame, date, bars.Length, bars[^1].IntervalEndUtc, atr.AtrValue, atr.AtrBaseline);
        RegimeDiscoverySignalCacheAdapter.RestoreBars(bars);
        var minimumBandAsOf = frame == TimeFrameType.Daily ? (DateTimeOffset?)null : bars[^1].IntervalStartUtc;
        if (await RestoreBollingerAsync(databases, contractId, date, frame, token, minimumBandAsOf).ConfigureAwait(false))
            return true;

        if (frame != TimeFrameType.Daily)
        {
            await StageAsync("EMA.BATCH", async () =>
            {
                var id = new FuturesTradeSessionBarEntityId(bars[0].MarketSeriesIdentity, frame);
                // Use the historical acquisition store. Minute OHLCV cannot
                // supply the exact trade count required by the live publisher.
                foreach (var bar in bars)
                    _ = await ((IHistoricalObservationStore)databases.MarketDataDb).TryWriteObservationAsync(bar, token).ConfigureAwait(false);
                // The normal EMA completion supplies the matching centerlines
                // to Bollinger. Regime trend EMAs still use the Daily baseline.
                Require(await actors.RequestAsync<SeedFuturesEmaSignalsCommand, FuturesTradeSessionBarEntityId>(new()
                {
                    CommandId = Guid.CreateVersion7(), PostEvents = true, EntityId = id, Observations = bars,
                    Subject = new(ActorType.Command, SeedFuturesEmaSignalsCommand.Actor, SeedFuturesEmaSignalsCommand.Verb, id.Format())
                }, token).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        if (await RestoreBollingerAsync(databases, contractId, date, frame, token, minimumBandAsOf).ConfigureAwait(false))
            return true;

        logger.LogWarning("Stored {TimeFrame} Bollinger inputs for {ContractId} are not yet warm; initialization remains retryable.", frame, contractId);
        return false;

        async Task StageAsync(string stage, Func<Task> command)
        {
            if (progress.Completed.ContainsKey(stage)) return;
            var waiting = System.Diagnostics.Stopwatch.GetTimestamp();
            await initializeCommands.WaitAsync(token).ConfigureAwait(false);
            GateWait.Record(System.Diagnostics.Stopwatch.GetElapsedTime(waiting).TotalMilliseconds,
                new KeyValuePair<string, object?>("work", "command"));
            try
            {
                await command().ConfigureAwait(false);
                progress.Completed.TryAdd(stage, 0);
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                logger.LogWarning(error, "Stored indicator stage {Stage} failed for {ContractId} {TimeFrame} on {ValueDate}; other independent stages finish and successful stages are retained.",
                    stage, contractId, frame, date);
                throw;
            }
            finally { initializeCommands.Release(); }
        }
    }

    /// <summary>Republishes committed warm observations independently of historical replay.</summary>
    async Task<DateTimeOffset?> RestoreCommittedIndicatorsAsync(IDbContextFactory databases, string contractId,
        DateOnly date, TimeFrameType frame, CancellationToken token)
    {
        var db = databases.MarketDataDb;
        // Each method creates an independent command context/connection. Each frame has
        // two permits so a stalled frame cannot starve another; four ordinary frames admit at most eight reads.
        using var restoreReads = new SemaphoreSlim(2, 2);
        var adxRead = ReadAsync(() => db.GetLastFuturesAdxSignalAsync(contractId, date, frame, 14, token));
        var atrRead = ReadAsync(() => db.GetLastFuturesAtrSignalAsync(contractId, date, frame, 14, token));
        var macdRead = ReadAsync(() => db.GetLastFuturesMacdSignalAsync(contractId, date, frame, 9, 12, 26, token));
        var rsiRead = ReadAsync(() => db.GetLastFuturesRsiSignalAsync(contractId, date, frame, 14, token));
        var tdiRead = ReadAsync(() => db.GetLastFuturesTdiSignalAsync(contractId, date, frame, FuturesTdiConfiguration.StandardConfigurationId, token));
        await Task.WhenAll(adxRead, atrRead, macdRead, rsiRead, tdiRead).ConfigureAwait(false);
        var adx = await adxRead; var atr = await atrRead; var macd = await macdRead; var rsi = await rsiRead; var tdi = await tdiRead;
        if (adx is { IsWarm: true }) RegimeDiscoverySignalCacheAdapter.Publish(adx);
        if (atr is { IsWarm: true, AtrRatio: not null }) RegimeDiscoverySignalCacheAdapter.Publish(atr);
        if (macd is { IsWarm: true }) RegimeDiscoverySignalCacheAdapter.Publish(macd);
        if (rsi is { IsWarm: true }) RegimeDiscoverySignalCacheAdapter.Publish(rsi);
        if (tdi is not null) RegimeDiscoverySignalCacheAdapter.Publish(tdi);
        var bandsRestored = await RestoreBollingerAsync(databases, contractId, date, frame, token).ConfigureAwait(false);
        if (!bandsRestored || !RegimeDiscoverySignalCacheAdapter.TryGetLatestBb(contractId, frame, out var bands)
            || adx is not { IsWarm: true, Metadata: not null }
            || atr is not { IsWarm: true, AtrRatio: not null, Metadata: not null }
            || macd is not { IsWarm: true, Metadata: not null } || rsi is not { IsWarm: true, Metadata: not null })
            return null;

        // Warm observations from before the latest closed interval can be shown,
        // but they cannot replace initialization from that newer interval.
        return new[] { adx.Metadata.MarketDataAsOfUtc, atr.Metadata.MarketDataAsOfUtc,
            macd.Metadata.MarketDataAsOfUtc, rsi.Metadata.MarketDataAsOfUtc, bands.Metadata.MarketDataAsOfUtc }.Min();

        async Task<T> ReadAsync<T>(Func<Task<T>> read)
        {
            var waiting = System.Diagnostics.Stopwatch.GetTimestamp();
            await restoreReads.WaitAsync(token).ConfigureAwait(false);
            GateWait.Record(System.Diagnostics.Stopwatch.GetElapsedTime(waiting).TotalMilliseconds,
                new KeyValuePair<string, object?>("work", "read"));
            try { return await read().ConfigureAwait(false); }
            finally { restoreReads.Release(); }
        }
    }
    /// <summary>Restores required Bollinger cache evidence even when replayed EMA observations were already committed before restart.</summary>
    public static async Task<bool> RestoreBollingerAsync(IDbContextFactory databases, string contractId, DateOnly date,
        TimeFrameType frame, CancellationToken token, DateTimeOffset? minimumAsOfUtc = null)
    {
        if (frame == TimeFrameType.Daily && RegimeDiscoverySignalCacheAdapter.TryGetLatestBb(contractId, frame, out var baseline)
            && baseline is { IsWarm: true, Width20: > 0, Width20Ratio: > 0, Position20: not null, Metadata.IsValid: true }) return true;
        var signal = await databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(
            MarketSeriesIdentity.ForContract(contractId), date, frame, token).ConfigureAwait(false);
        if (signal is not { IsWarm: true, Width20: > 0, Width20Ratio: > 0, Position20: not null, Metadata.IsValid: true }) return false;
        RegimeDiscoverySignalCacheAdapter.Publish(signal);
        return minimumAsOfUtc is null || signal.Metadata.MarketDataAsOfUtc >= minimumAsOfUtc;
    }

    static void Require(ServiceResult<Guid> result)
    {
        if (!result.Success) throw new InvalidOperationException($"Stored indicator initialization rejected ({result.ErrorCode}): {result.ErrorMessage}");
    }

    /// <summary>Excludes only the local reconstruction time; every source, value and qualification change invalidates progress.</summary>
    internal static string SeedRevision(FuturesTradeSessionBarReadModel[] bars)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePack.MessagePackSerializer.Serialize(
            bars.Select(bar => bar with { CalculatedAtUtc = default }).ToArray(),
            TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.ContentOptions)));
}
