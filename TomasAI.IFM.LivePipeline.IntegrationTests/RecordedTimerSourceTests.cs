using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Time;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class RecordedTimerSourceTests
{
    [Fact]
    public async Task Historical_clock_suppresses_only_the_three_original_timer_sources_and_flatten_does_not_read_storage()
    {
        var clock=new HistoricalReplayClock(DateTimeOffset.Parse("2026-11-20T20:55:00Z"));
        Assert.True(clock.UsesRecordedSource("FuturesTradeSessionBarSignal","Barrier"));
        Assert.True(clock.UsesRecordedSource("FuturesTradePositionRealtime","EvaluateFuturesExitClock"));
        Assert.True(clock.UsesRecordedSource("IronCondorTradePositionRealtime","IronCondorMonitoringInputsChanged"));
        Assert.False(clock.UsesRecordedSource("FuturesTradePositionRealtime","PriceChanged"));
        var database=Substitute.For<IDbContextFactory>();var supervisor=Substitute.For<IActorSupervisor>();
        using var service=new DailyFuturesFlattenService(database,supervisor,clock,NullLogger<DailyFuturesFlattenService>.Instance);
        await service.StartAsync(CancellationToken.None);await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));await service.StopAsync(CancellationToken.None);
        Assert.Empty(database.ReceivedCalls());Assert.Empty(supervisor.ReceivedCalls());
    }
    [Fact]
    public void Historical_generation_authority_is_default_denied_and_immutable()
    {
        var clock=new HistoricalReplayClock(DateTimeOffset.UtcNow);var proof=new RecordedRealtimeGenerationAcceptance{Subject=new(TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime,"IronCondorTradePositionRealtime","IronCondorMonitoringInputsChanged","thread"),EventId=Guid.NewGuid()};
        Assert.False(clock.Allows(proof,Guid.NewGuid()));var authority=Substitute.For<IRecordedRealtimeGenerationAuthority>();authority.Allows(proof,NSubstitute.Arg.Any<Guid>()).Returns(true);
        clock.SetGenerationAuthority(authority);Assert.True(clock.Allows(proof,Guid.NewGuid()));Assert.False(clock.Allows(proof,Guid.Empty));
        Assert.Throws<InvalidOperationException>(()=>clock.SetGenerationAuthority(Substitute.For<IRecordedRealtimeGenerationAuthority>()));
    }
}
