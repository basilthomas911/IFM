using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class EmaRegimeWarmupTests
{
    [Fact]
    public async Task Blocked_weekly_download_does_not_delay_daily_or_monthly_restore_and_restart_reuses_storage()
    {
        var now = new DateTimeOffset(2026, 11, 19, 16, 31, 0, TimeSpan.Zero);
        var clock = new Clock(now);
        var calendar = new CmeFuturesMarketSessionCalendar();
        var db = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<IMarketDataApi>();
        var actors = Substitute.For<IActorService>();
        var contract = "ES-INDEPENDENT-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        foreach (var frame in new[] { TimeFrameType.FiveMinutes, TimeFrameType.Daily })
        {
            var window = RsiHistoricalSeedWindowModel.Create(frame, 1, now, calendar)[0];
            db.MarketDataDb.GetLatestFuturesEmaSignalForTimeFrameAsync(series, calendar.GetValueDate(now), frame, Arg.Any<CancellationToken>())
                .Returns(new FuturesEmaSignalReadModel { Ema20 = 30, Ema50 = 20, Ema200 = 10, IsWarm = true,
                    Metadata = new() { ContractId = contract, SignalKey = new(series, MarketAnalyticsSignalKind.Ema, frame, "ema"),
                        MarketDataAsOfUtc = window.EndUtc.AddSeconds(-1), IsValid = true } });
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<FuturesTradeSessionBarReadModel[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        market.ReadEmaSeedBarsAsync(contract, TimeFrameType.FourHours, 300, Arg.Any<CancellationToken>()).Returns(call =>
        { entered.TrySetResult(); return pending.Task.WaitAsync(call.Arg<CancellationToken>()); });
        EmaRegimeWarmupService Create() => new(db, market, actors, calendar, clock,
            new RegimeDiscoveryExecutionOptions { CalculationPolicy = EmaStackRegimePolicy.Version }, NullLogger<EmaRegimeWarmupService>.Instance);
        using var stop = new CancellationTokenSource();
        using var worker = Create();
        var weekly = worker.RestoreOrSeedAsync(contract, TimeFrameType.Weekly, stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(await worker.RestoreOrSeedAsync(contract, TimeFrameType.Daily, default).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(await worker.RestoreOrSeedAsync(contract, TimeFrameType.Monthly, default).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(weekly.IsCompleted);
        using var restarted = Create();
        Assert.True(await restarted.RestoreOrSeedAsync(contract, TimeFrameType.Daily, default));
        Assert.True(await restarted.RestoreOrSeedAsync(contract, TimeFrameType.Monthly, default));
        await market.DidNotReceive().ReadEmaSeedBarsAsync(contract, TimeFrameType.FiveMinutes, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await market.DidNotReceive().ReadEmaSeedBarsAsync(contract, TimeFrameType.Daily, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => weekly);
    }
    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
