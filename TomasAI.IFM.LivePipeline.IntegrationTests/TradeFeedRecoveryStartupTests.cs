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
