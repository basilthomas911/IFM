using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

/// <summary>Restores the Daily, Weekly and Monthly strategy inputs through normal indicator actors and qualified original history.</summary>
public sealed class StoredSessionIndicatorWarmup(StoredSessionIndicatorSeedReader reader, IActorService actors,
    IMarketDataApi market, IDbContextFactory databases, ILogger<StoredSessionIndicatorWarmup> logger)
{
    readonly HashSet<(DateOnly Date, TimeFrameType Frame)> completed = [];

    /// <summary>Initializes each required intraday/Daily family once per session; missing history remains explicit and retryable.</summary>
    public async Task<bool> WarmAsync(DateOnly date, CancellationToken token)
    {
        if (!market.TryGetOnTheRunFuturesContract("ES", out var contract)) return false;
        var all = true;
        foreach (var frame in new[] { TimeFrameType.FifteenMinutes, TimeFrameType.OneHour, TimeFrameType.Daily, TimeFrameType.FourHours })
        {
            if (completed.Contains((date, frame))) continue;
            var bars = await reader.ReadAsync(contract.ContractId, frame, 48, token).ConfigureAwait(false);
            if (bars.Length != 48) { all = false; continue; }
            var activation = new FuturesIntradaySignalActivation(frame, new(contract.ContractId, date, frame, 13),
                new(contract.ContractId, date, frame, 14), new(contract.ContractId, date, frame, 14), new(contract.ContractId, date, frame));
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
                    var id = new FuturesAtrSignalId(contract.ContractId, bar.ValueDate, frame, 14,
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
            foreach (var bar in bars)
            {
                // Daily EMA20/50/200 already come from the full stored baseline.
                // Do not replace its 200+ sessions with this 48-session oscillator seed.
                if (frame == TimeFrameType.Daily) continue;
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
            // Actor acceptance includes the transactional read-model write. Use
            // it here so structure restoration does not race the event projector.
            var atr = await databases.MarketDataDb.GetLastFuturesAtrSignalAsync(contract.ContractId,
                frame == TimeFrameType.Daily ? bars[^1].ValueDate : date, frame, 14, token).ConfigureAwait(false);
            if (atr is not { IsWarm: true, AtrRatio: not null })
                throw new InvalidOperationException("Stored ATR initialization has not produced a warm read model.");
            RegimeDiscoverySignalCacheAdapter.Publish(atr);
            RegimeDiscoverySignalCacheAdapter.RestoreBars(bars);
            if (!await RestoreBollingerAsync(databases, contract.ContractId, date, frame, token).ConfigureAwait(false))
            {
                all = false;
                logger.LogWarning("Stored {TimeFrame} Bollinger inputs for {ContractId} are not yet warm; initialization remains retryable.", frame, contract.ContractId);
                continue;
            }
            completed.Add((date, frame));
            logger.LogInformation("Stored history restored {ContractId} {TimeFrame} RSI13/14, ADX, ATR, MACD, warm Bollinger and structure inputs for {ValueDate}.", contract.ContractId, frame, date);
        }
        return all;
    }

    /// <summary>Restores required Bollinger cache evidence even when replayed EMA observations were already committed before restart.</summary>
    public static async Task<bool> RestoreBollingerAsync(IDbContextFactory databases, string contractId, DateOnly date,
        TimeFrameType frame, CancellationToken token)
    {
        if (frame == TimeFrameType.Daily && RegimeDiscoverySignalCacheAdapter.TryGetLatestBb(contractId, frame, out var baseline)
            && baseline is { IsWarm: true, Width20: > 0, Width20Ratio: > 0, Position20: not null, Metadata.IsValid: true }) return true;
        var signal = await databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(
            MarketSeriesIdentity.ForContract(contractId), date, frame, token).ConfigureAwait(false);
        if (signal is not { IsWarm: true, Width20: > 0, Width20Ratio: > 0, Position20: not null, Metadata.IsValid: true }) return false;
        RegimeDiscoverySignalCacheAdapter.Publish(signal);
        return true;
    }

    static void Require(ServiceResult<Guid> result)
    {
        if (!result.Success) throw new InvalidOperationException($"Stored indicator initialization rejected ({result.ErrorCode}): {result.ErrorMessage}");
    }
}
