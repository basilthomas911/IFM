using Microsoft.Extensions.Logging;
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

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;

/// <summary>Restores the Daily, Weekly and Monthly strategy inputs through normal indicator actors and qualified original history.</summary>
public sealed class ApiServerPerformanceWarmupBaseline(ApiServerPerformanceSeedReaderBaseline reader, IActorService actors,
    IMarketDataApi market, IDbContextFactory databases, IMarketSessionCalendar calendar, TimeProvider clock,
    ILogger<ApiServerPerformanceWarmupBaseline> logger)
{
    readonly Dictionary<(DateOnly Date, string ContractId, TimeFrameType Frame), DateTimeOffset> completed = [];
    readonly System.Collections.Concurrent.ConcurrentDictionary<(DateOnly Date, string ContractId, TimeFrameType Frame), byte> shortSeeds = new();
    readonly SemaphoreSlim fifteenSecondSeed = new(1, 1);
    readonly SemaphoreSlim oneMinuteSeed = new(1, 1);

    /// <summary>Seeds a fast timeframe before its live processors attach.</summary>
    public async Task<bool> WarmShortFrameAsync(string contractId, DateOnly date, TimeFrameType frame, CancellationToken token)
    {
        var gate = frame switch
        {
            TimeFrameType.FifteenSeconds => fifteenSecondSeed,
            TimeFrameType.OneMinute => oneMinuteSeed,
            _ => throw new ArgumentOutOfRangeException(nameof(frame))
        };
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var key = (date, contractId, frame);
            if (shortSeeds.ContainsKey(key)) return true;
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
            budget.CancelAfter(TimeSpan.FromSeconds(90));
            if (!await WarmFrameAsync(contractId, date, frame, budget.Token, seedShortFrame: true).ConfigureAwait(false))
                return false;
            shortSeeds.TryAdd(key, 0);
            logger.LogInformation("Historical startup seed verified for {ContractId} {TimeFrame} on {ValueDate}: RSI13/14, ADX, ATR, MACD and Bollinger state.",
                contractId, frame, date);
            return true;
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            logger.LogWarning(error, "Historical startup seed failed for {ContractId} {TimeFrame} on {ValueDate}.", contractId, frame, date);
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Restores session baselines and reconciles each newly closed 15-minute and hourly interval.</summary>
    public async Task<bool> WarmAsync(DateOnly date, CancellationToken token)
    {
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
        return (await fastFrames.ConfigureAwait(false)).All(static result => result) && results.All(static result => result);

        async Task<bool> TryWarmFrameAsync(TimeFrameType frame)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
            // Four-hour recovery can use the host's full three-minute RPC budget,
            // with time left to restore the indicators after the bars arrive.
            budget.CancelAfter(TimeSpan.FromMinutes(frame == TimeFrameType.FourHours ? 4 : 2));
            try
            {
                if (await WarmFrameAsync(contract.ContractId, date, frame, budget.Token).ConfigureAwait(false))
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

    async Task<bool> WarmFrameAsync(string contractId, DateOnly date, TimeFrameType frame, CancellationToken token, bool seedShortFrame = false)
    {
        if (frame == TimeFrameType.Daily)
        {
            var vx = await databases.MarketDataDb.GetLatestFuturesVxTermStructureSignalAsync(
                date, new FuturesVxTermStructureConfiguration().ConfigurationId, token).ConfigureAwait(false);
            if (vx is { IsWarm: true, IsValid: true })
                RegimeDiscoverySignalCacheAdapter.Publish(vx);
        }
        FuturesTradeSessionBarReadModel[]? acquiredSeed = null;
        if (!seedShortFrame && await RestoreCommittedIndicatorsAsync(databases, contractId, date, frame, token).ConfigureAwait(false) is { } oldestObservation)
        {
            // Market structure needs the current bar and the preceding 20 completed bars.
            var structure = await reader.ReadAsync(contractId, frame, 21, token).ConfigureAwait(false);
            if (structure.Length >= 21 && oldestObservation >= structure[^1].IntervalStartUtc)
            {
                RegimeDiscoverySignalCacheAdapter.RestoreBars(structure);
                return true;
            }
            if (structure.Length == 48)
                acquiredSeed = structure;
        }
        var bars = acquiredSeed ?? await reader.ReadAsync(contractId, frame, 48, token).ConfigureAwait(false);
        if (bars.Length != 48) return false;
        var activation = new FuturesIntradaySignalActivation(frame, new(contractId, date, frame, 13),
            new(contractId, date, frame, 14), new(contractId, date, frame, 14), new(contractId, date, frame));
        foreach (var period in new[] { 13, 14 })
        {
            var id = activation.Rsi with { PeriodLength = period };
            Require(await actors.RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(new(id)
            {
                CommandId = Guid.CreateVersion7(), PostEvents = true,
                Subject = new(ActorType.Command, StartFuturesRsiSignalCommand.Actor, StartFuturesRsiSignalCommand.Verb, id.Format()),
                HistoricalSeed = new(48, bars[^1].IntervalEndUtc, bars, "RSI.STORED_TRADES_SEED"),
                ForceHistoricalInitialization = true
            }, token).ConfigureAwait(false));
        }
        Require(await actors.RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(new(activation.Adx)
        {
            CommandId = Guid.CreateVersion7(), PostEvents = true, HistoricalSeed = bars,
            Subject = new(ActorType.Command, StartFuturesAdxSignalCommand.Actor, StartFuturesAdxSignalCommand.Verb, activation.Adx.Format())
        }, token).ConfigureAwait(false));
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
        Require(await actors.RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(new(activation.Macd)
        {
            CommandId = Guid.CreateVersion7(), PostEvents = true, HistoricalSeed = bars,
            Subject = new(ActorType.Command, StartFuturesMacdSignalCommand.Actor, StartFuturesMacdSignalCommand.Verb, activation.Macd.Format())
        }, token).ConfigureAwait(false));
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
            foreach (var bar in bars)
            {
                var id = new FuturesTradeSessionBarEntityId(bar.MarketSeriesIdentity, frame);
                // Use the historical acquisition store. Minute OHLCV cannot
                // supply the exact trade count required by the live publisher.
                _ = await ((IHistoricalObservationStore)databases.MarketDataDb).TryWriteObservationAsync(bar, token).ConfigureAwait(false);
                // The normal EMA completion supplies the matching centerlines
                // to Bollinger. Regime trend EMAs still use the Daily baseline.
                Require(await actors.RequestAsync<GenerateFuturesEmaSignalCommand, FuturesTradeSessionBarEntityId>(new()
                {
                    CommandId = Guid.CreateVersion7(), PostEvents = true, EntityId = id, Observation = bar,
                    Subject = new(ActorType.Command, GenerateFuturesEmaSignalCommand.Actor, GenerateFuturesEmaSignalCommand.Verb, id.Format())
                }, token).ConfigureAwait(false));
            }
        }

        if (await RestoreBollingerAsync(databases, contractId, date, frame, token, minimumBandAsOf).ConfigureAwait(false))
            return true;

        logger.LogWarning("Stored {TimeFrame} Bollinger inputs for {ContractId} are not yet warm; initialization remains retryable.", frame, contractId);
        return false;
    }

    /// <summary>Republishes committed warm observations independently of historical replay.</summary>
    static async Task<DateTimeOffset?> RestoreCommittedIndicatorsAsync(IDbContextFactory databases, string contractId,
        DateOnly date, TimeFrameType frame, CancellationToken token)
    {
        var db = databases.MarketDataDb;
        var adx = await db.GetLastFuturesAdxSignalAsync(contractId, date, frame, 14, token).ConfigureAwait(false);
        var atr = await db.GetLastFuturesAtrSignalAsync(contractId, date, frame, 14, token).ConfigureAwait(false);
        var macd = await db.GetLastFuturesMacdSignalAsync(contractId, date, frame, 9, 12, 26, token).ConfigureAwait(false);
        var rsi = await db.GetLastFuturesRsiSignalAsync(contractId, date, frame, 14, token).ConfigureAwait(false);
        var tdi = await db.GetLastFuturesTdiSignalAsync(contractId, date, frame, FuturesTdiConfiguration.StandardConfigurationId, token).ConfigureAwait(false);
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
}

