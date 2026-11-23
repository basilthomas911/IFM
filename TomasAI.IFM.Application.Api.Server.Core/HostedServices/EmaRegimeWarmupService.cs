using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Independent, retryable background EMA initialization; no workflow waits on another horizon's history.</summary>
public sealed class EmaRegimeWarmupService(IDbContextFactory databases, IMarketDataApi market, IActorService actors,
    IMarketSessionCalendar calendar, TimeProvider clock, RegimeDiscoveryExecutionOptions options,
    ILogger<EmaRegimeWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.CalculationPolicy != EmaStackRegimePolicy.Version) return;
        await Task.WhenAll(new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly }
            .Select(horizon => RunHorizonAsync(horizon, stoppingToken))).ConfigureAwait(false);
    }

    async Task RunHorizonAsync(TimeFrameType horizon, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                if (!market.TryGetOnTheRunFuturesContract("ES", out var contract)) continue;
                // Each horizon owns its retry loop as well as its acquisition budget.
                // A long Weekly download cannot hold Daily or Monthly refreshes.
                await RestoreOrSeedAsync(contract.ContractId, horizon, stoppingToken).ConfigureAwait(false);
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task<bool> RestoreOrSeedAsync(string contract, TimeFrameType horizon, CancellationToken token)
    {
        var frame = EmaStackRegimePolicy.Frame(horizon);
        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
            budget.CancelAfter(TimeSpan.FromMinutes(4));
            var ct = budget.Token;
            var date = calendar.GetValueDate(clock.GetUtcNow());
            var series = StoredSessionIndicatorWarmup.LiveIndicatorSeries(contract);
            var signal = await databases.MarketDataDb.GetLatestFuturesEmaSignalForTimeFrameAsync(series, date, frame, ct).ConfigureAwait(false);
            if (signal is { IsWarm: true, Ema20: > 0, Ema50: > 0, Ema200: > 0, Metadata.IsValid: true })
            {
                RegimeDiscoverySignalCacheAdapter.Publish(signal);
                var minimum = RsiHistoricalSeedWindowModel.Create(frame, 1, clock.GetUtcNow().AddSeconds(-30), calendar)[0].StartUtc;
                if (signal.Metadata.MarketDataAsOfUtc >= minimum) return true;
            }
            var original = await market.ReadEmaSeedBarsAsync(contract, frame, 300, ct).ConfigureAwait(false);
            if (original.Length < 200 || original.Any(b => !b.IsComplete || !b.IsValid || b.ContractId != contract || b.TimeFrame != frame)) return false;
            var bars = original.Select(bar => bar with { MarketSeriesIdentity = series,
                ObservationId = FuturesTradeSessionBarId.Create(series, frame, bar.IntervalEndUtc, bar.LastSourceSequence) }).ToArray();
            // Preserve source bars durably as well as the actor's full EMA checkpoint.
            foreach (var bar in bars)
                _ = await ((IHistoricalObservationStore)databases.MarketDataDb).TryWriteObservationAsync(bar, ct).ConfigureAwait(false);
            var entity = new FuturesTradeSessionBarEntityId(series, frame);
            var result = await actors.RequestAsync<SeedFuturesEmaSignalsCommand, FuturesTradeSessionBarEntityId>(new()
            {
                EntityId = entity, CommandId = Guid.CreateVersion7(), PostEvents = true, Observations = bars, InitializeEma200 = true,
                Subject = new(ActorType.Command, SeedFuturesEmaSignalsCommand.Actor, SeedFuturesEmaSignalsCommand.Verb, entity.Format())
            }, ct).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
            signal = await databases.MarketDataDb.GetLatestFuturesEmaSignalForTimeFrameAsync(series, date, frame, ct).ConfigureAwait(false);
            if (signal is not { IsWarm: true, Ema20: > 0, Ema50: > 0, Ema200: > 0, Metadata.IsValid: true }) return false;
            RegimeDiscoverySignalCacheAdapter.Publish(signal);
            logger.LogInformation("EMA-only Regime Discovery restored {Contract} {Horizon} from {Count} completed {Frame} bars through {AsOf}.", contract, horizon, bars.Length, frame, signal.Metadata.MarketDataAsOfUtc);
            return signal.Metadata.MarketDataAsOfUtc >= RsiHistoricalSeedWindowModel.Create(frame, 1, clock.GetUtcNow().AddSeconds(-30), calendar)[0].StartUtc;
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            logger.LogWarning(error, "EMA-only {Horizon} history remains unavailable for {Contract}; other horizons continue independently.", horizon, contract);
            return false;
        }
    }
}
