using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class TradeFeedRecoveryStartupTests
{
    [Fact]
    public async Task Retry_preserves_success_receipts_and_bounds_concurrent_restore_requests()
    {
        var date = new DateOnly(2026, 11, 7);
        var market = Substitute.For<IMarketDataApi>();
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true);
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, date, [], Guid.NewGuid()));
        var rows = Enumerable.Range(0, 8).Select(index => new TradeFeedRecoveryRegistration(index.ToString(), date, ["C", "P"], DateTimeOffset.UtcNow)).ToArray();
        var reads = 0;
        market.ReadTradeFeedRecoveryAsync(date, Arg.Any<CancellationToken>()).Returns(_ =>
            Interlocked.Increment(ref reads) % 2 == 1 ? rows : rows.Select(row => row with { OptionContracts = row.OptionContracts.Reverse().ToArray() }).ToArray());
        var counts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        var active = 0; var maximum = 0;
        market.LoadTradeFeedRecoveryAsync(Arg.Any<string>(), date, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var id = call.ArgAt<string>(0);
            var attempt = counts.AddOrUpdate(id, 1, (_, count) => count + 1);
            var current = Interlocked.Increment(ref active);
            int old;
            do { old = Volatile.Read(ref maximum); if (current <= old) break; }
            while (Interlocked.CompareExchange(ref maximum, current, old) != old);
            try { await Task.Delay(20, call.ArgAt<CancellationToken>(2)); return id != "0" || attempt > 1; }
            finally { Interlocked.Decrement(ref active); }
        });
        using var service = new TradeFeedRecoveryStartupService(market, sessions, supervisor, Substitute.For<IJSActorProducer>(), NullLogger<TradeFeedRecoveryStartupService>.Instance);
        await service.StartAsync(default);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (service.Current is not { Restored: true }) await Task.Delay(10, deadline.Token);
            Assert.Equal(2, counts["0"]);
            Assert.All(counts.Where(pair => pair.Key != "0"), pair => Assert.Equal(1, pair.Value));
            Assert.InRange(maximum, 2, 4);
            await market.Received(3).ReadTradeFeedRecoveryAsync(date, Arg.Any<CancellationToken>());
        }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await service.StopAsync(stop.Token); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_uses_current_date_and_attempts_other_trades_even_if_one_fails(bool firstFails)
    {
        var date = new DateOnly(2026,10,22);
        var market = Substitute.For<IMarketDataApi>();
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate=date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true);
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true,date,[]));
        market.ReadTradeFeedRecoveryAsync(date,Arg.Any<CancellationToken>()).Returns([
            new TradeFeedRecoveryRegistration("101.701.1701.1101",date,["P1","P2","C1","C2"],DateTimeOffset.UtcNow),
            new TradeFeedRecoveryRegistration("101.701.1701.1102",date,["P3","P4","C3","C4"],DateTimeOffset.UtcNow)]);
        market.LoadTradeFeedRecoveryAsync("101.701.1701.1101",date,Arg.Any<CancellationToken>())
            .Returns(_ => firstFails ? Task.FromException<bool>(new IOException("one trade unavailable")) : Task.FromResult(true));
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        market.LoadTradeFeedRecoveryAsync("101.701.1701.1102",date,Arg.Any<CancellationToken>())
            .Returns(_ => { second.TrySetResult(); return Task.FromResult(true); });
        using var service = new TradeFeedRecoveryStartupService(market,sessions,supervisor,Substitute.For<IJSActorProducer>(),NullLogger<TradeFeedRecoveryStartupService>.Instance);
        await service.StartAsync(default);
        try { await second.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await service.StopAsync(stop.Token); }
        await market.DidNotReceive().ReadTradeFeedRecoveryAsync(date.AddDays(-1),Arg.Any<CancellationToken>());
        await market.Received().LoadTradeFeedRecoveryAsync("101.701.1701.1101",date,Arg.Any<CancellationToken>());
    }
}
