using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

[CollectionDefinition("API restart environment",DisableParallelization=true)]
public sealed class ApiRestartEnvironmentCollection;

[Collection("API restart environment")]
public sealed class ApiRecoveryRestartHandoffTests
{
    [Fact]
    public async Task API_request_is_written_only_after_UI_error_publish_succeeds_and_contains_saved_trades_and_owned_identity()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-restart-handoff-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var variables=new[]{ApiRecoveryRestartProtocol.DirectoryVariable,ApiRecoveryRestartProtocol.SessionVariable,ApiRecoveryRestartProtocol.ControllerVariable};
        var previous=variables.ToDictionary(name=>name,Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(variables[0],root);Environment.SetEnvironmentVariable(variables[1],"owned-session");Environment.SetEnvironmentVariable(variables[2],"1");
            var date=DateOnly.FromDateTime(DateTime.UtcNow);var market=Substitute.For<IMarketDataApi>();
            market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true,date,[]));
            market.ReadTradeFeedRecoveryAsync(date,Arg.Any<CancellationToken>()).Returns([new TradeFeedRecoveryRegistration("101.701.1901.1501",date,["P1","P2","C1","C2"],DateTimeOffset.UtcNow)]);
            var sessions=Substitute.For<IFuturesMarketSessionAuthority>();sessions.Current.Returns(new MarketSessionReadModel{ActiveValueDate=date,SessionEndUtc=DateTime.UtcNow.AddHours(6)});
            var clock=new LivePipelineIntegrationTests.ManualTime();var runtime=Substitute.For<IOptionPipelineRecoveryRuntime>();runtime.Inspect().Returns(new OptionPipelineInspection(date,"test fault",[]));
            runtime.RecoverAndVerifyAsync(Arg.Any<Guid>(),Arg.Any<OptionPipelineInspection>(),Arg.Any<DateTimeOffset>(),Arg.Any<CancellationToken>())
                .Returns(new TaskCompletionSource().Task);
            var recovery=new OptionPipelineRecoveryCoordinator(runtime,NullLogger<OptionPipelineRecoveryCoordinator>.Instance,clock);
            recovery.Observe(default);clock.Advance(TimeSpan.FromMinutes(2));await recovery.Completion!.WaitAsync(TimeSpan.FromSeconds(5));
            var failure=recovery.Failure!;var producer=Substitute.For<IJSActorProducer>();producer.IsRunning.Returns(true);
            var attempted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            producer.SendAsync<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataDatasetRecoveryFailedEvent,TomasAI.IFM.Domain.MarketData.Feed.Shared.MarketDataFeedId>(
                Arg.Any<ActorSubject>(),Arg.Any<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataDatasetRecoveryFailedEvent>(),Arg.Any<CancellationToken>())
                .Returns(_=>{attempted.TrySetResult();return ValueTask.FromException(new IOException("UI publication not acknowledged"));});
            using var service=new OptionFeedRecoveryNotificationService(market,producer,NullLogger<OptionFeedRecoveryNotificationService>.Instance,recovery:recovery,
                escalation:new ApiRecoveryRestartEscalation(market,sessions));
            await service.StartAsync(default);
            try
            {
                await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Empty(Directory.GetFiles(root));
                producer.SendAsync<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataDatasetRecoveryFailedEvent,TomasAI.IFM.Domain.MarketData.Feed.Shared.MarketDataFeedId>(
                    Arg.Any<ActorSubject>(),Arg.Any<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataDatasetRecoveryFailedEvent>(),Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));var path=ApiRecoveryRestartProtocol.RequestPath(root,failure.CommandId);
                while(!File.Exists(path))await Task.Delay(20,timeout.Token);
                var request=JsonSerializer.Deserialize<ApiRecoveryRestartRequest>(await File.ReadAllTextAsync(path))!;
                using var process=Process.GetCurrentProcess();Assert.Equal(process.Id,request.ProcessId);Assert.Equal(process.StartTime.ToUniversalTime(),request.ProcessStartedUtc);
                Assert.Equal("owned-session",request.SessionId);Assert.Equal(date,request.ValueDate);Assert.Equal(["101.701.1901.1501"],request.TradeIds);
            }
            finally{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));await service.StopAsync(timeout.Token);}
        }
        finally
        {
            foreach(var name in variables)Environment.SetEnvironmentVariable(name,previous[name]);
            if(root.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("ifm-restart-handoff-",StringComparison.Ordinal))Directory.Delete(root,true);
        }
    }

    [Fact]
    public void Restart_proof_rejects_saved_or_unpublished_positions_and_requires_all_four_fresh_legs_and_plan()
    {
        var date=DateOnly.FromDateTime(DateTime.UtcNow);var at=DateTimeOffset.UtcNow;
        var trade=new EstablishedTradeDefinition{Id=new(101,701,1901,1501),StrategyKind=TradeStrategyKind.IronCondor,
            Legs=Enumerable.Range(0,4).Select(i=>new TradeLegDefinition{TradeLegId=Guid.NewGuid(),ContractId="option-"+i,SignedQuantity=i%2==0?-1:1}).ToArray()};
        var registry=new TradeRealtimeCacheRegistry();var cache=registry.Register(trade,date);
        var sessions=Substitute.For<IFuturesMarketSessionAuthority>();sessions.Current.Returns(new MarketSessionReadModel{ActiveValueDate=date});
        var supervisor=Substitute.For<IActorSupervisor>();supervisor.IsReady.Returns(true);
        var restored=new TradeFeedRecoveryStartupService.Restoration(date,at.AddSeconds(-1).UtcDateTime,true,[trade.Id.Format()]);
        Assert.Empty(ApiRecoveryReadinessProbe.Capture(restored,sessions,supervisor,registry).ProvenTradeIds);
        var position=new StrategyPositionSnapshot{Id=StrategyPositionId.Create(trade.Id,TradeStrategyKind.IronCondor),StrategyKind=TradeStrategyKind.IronCondor,
            ValueDate=date,IsOpen=true,RouteGeneration=1,PositionSequence=1,AsOfUtc=at.UtcDateTime,
            Legs=trade.Legs.Select(leg=>{var tick=new FuturesOptionTickDataV2ReadModel{ContractId=leg.ContractId,ValueDate=date,ObservedAtUtc=at,CalculatedAtUtc=at,
                ValidUntilUtc=at.AddMinutes(1),OptionPrice=10,BidPrice=9,AskPrice=11,UnderlyingPrice=6000,ImpliedVolatility=.2,Delta=.1,GreeksAvailable=true,TheoreticalPrice=10};
                return new StrategyPositionLeg{TradeLegId=leg.TradeLegId,ContractId=leg.ContractId,SignedQuantity=leg.SignedQuantity,CurrentPrice=10,OptionTick=tick,OptionRisk=tick};}).ToArray()};
        cache.Apply(position);cache.Apply(new StrategyTradePlanSnapshot{Position=position,ValueDate=date,PlanRevision=1,CalculatedAtUtc=at.UtcDateTime,
            IronCondorTradePlanSnapshot=new(){Position=position,CalculationStatus="Complete"}});
        Assert.Empty(ApiRecoveryReadinessProbe.Capture(restored,sessions,supervisor,registry).ProvenTradeIds);
        registry.RecordPublication(cache.ReadForPublication());
        Assert.Contains(trade.Id.Format(),ApiRecoveryReadinessProbe.Capture(restored,sessions,supervisor,registry).ProvenTradeIds);
        Assert.Empty(ApiRecoveryReadinessProbe.Capture(restored with{StartedUtc=at.AddSeconds(1).UtcDateTime},sessions,supervisor,registry).ProvenTradeIds);
        Assert.False(ApiRecoveryReadinessProbe.Capture(restored with{Restored=false},sessions,supervisor,registry).FeedsRestored);
    }
}
