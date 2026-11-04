using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

/// <summary>Exercises the native-host API session lifecycle without waiting for market observations.</summary>
public sealed class SessionAnalyticsStartupTests
{
    [Fact]
    public async Task Native_session_rollover_attaches_new_analytics_once_without_waiting_for_quotes()
    {
        var initializer = Substitute.For<IMarketSessionAnalyticsInitializer>();
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        var market = Substitute.For<IMarketDataApi>();
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.IsReady.Returns(true);
        market.IsDatabentoFeedUp().Returns(true);
        var first = new DateOnly(2026, 11, 3);
        var next = first.AddDays(1);
        var attachedFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attachedNext = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = first, OperationalValueDate = first });
        initializer.InitializeAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            (call.Arg<DateOnly>() == first ? attachedFirst : attachedNext).TrySetResult();
            return Task.CompletedTask;
        });
        using var service = new SessionAnalyticsStartupService(initializer, sessions, market, supervisor,
            NullLogger<SessionAnalyticsStartupService>.Instance);
        await service.StartAsync(default);
        try
        {
            await attachedFirst.Task.WaitAsync(TimeSpan.FromSeconds(5));
            sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = next, OperationalValueDate = next });
            await attachedNext.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await initializer.Received(1).InitializeAsync(first, Arg.Any<CancellationToken>());
            await initializer.Received(1).InitializeAsync(next, Arg.Any<CancellationToken>());
        }
        finally { await service.StopAsync(default); }
    }
}
