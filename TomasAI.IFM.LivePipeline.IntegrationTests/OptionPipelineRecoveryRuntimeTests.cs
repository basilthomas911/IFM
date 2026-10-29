using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionPipelineRecoveryRuntimeTests
{
    [Theory]
    [InlineData(MarketDataRecoveryOwnership.TokioDatasetOwner)]
    [InlineData(MarketDataRecoveryOwnership.Unknown)]
    public async Task Native_or_unknown_dataset_does_not_start_Csharp_recovery_or_restart_actors(MarketDataRecoveryOwnership ownership)
    {
        var market=Substitute.For<IMarketDataApi>();
        market.DatasetRecoveryOwnership.Returns(ownership);
        var actors=Substitute.For<IActorSupervisor>();
        var sessions=Substitute.For<IFuturesMarketSessionAuthority>();
        var runtime=new OptionPipelineRecoveryRuntime(actors,market,sessions,new TradeRealtimeCacheRegistry());
        var inspection=runtime.Inspect();
        Assert.Null(inspection.ValueDate);
        Assert.Null(inspection.Failure);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>runtime.RecoverAndVerifyAsync(
            Guid.NewGuid(),new(DateOnly.FromDateTime(DateTime.UtcNow),"injected",[]),DateTimeOffset.UtcNow.AddMinutes(2),default));
        Assert.DoesNotContain(market.ReceivedCalls(),call=>call.GetMethodInfo().Name is "GetBackendStatusAsync" or "RecoverMarketDataAsync" or "RepairOptionObservationsAsync");
        Assert.Empty(actors.ReceivedCalls());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Native_idle_requires_explicit_workload_evidence_and_never_shortens_deadline(bool complete)
    {
        var clock=new LivePipelineIntegrationTests.ManualTime();var start=clock.GetUtcNow();
        var date=DateOnly.FromDateTime(start.UtcDateTime);
        var before=new MarketDataDatasetStatus("Options","GLBX.MDP3",Guid.NewGuid(),Guid.NewGuid(),123,true,true,"Healthy",null,date);
        var after=before with{WorkerInstanceId=Guid.NewGuid(),GenerationId=Guid.NewGuid()};var current=before;
        var host=Guid.NewGuid();var market=Substitute.For<IMarketDataApi>();
        MarketDataBackendStatus Status()=>new(true,date,[current],host,EnabledOptionTrades:[],RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner);
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(_=>Status());
        market.GetRuntimeStatus().Returns(new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataFeedRuntimeStatusReadModel{
            EnabledOptionTrades=[],OptionProcessingError="Injected",OptionProcessingFailureUtc=start});
        var requested=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        market.RecoverMarketDataAsync(Arg.Any<MarketDataRecoveryRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{
            current=after;requested.TrySetResult();return new MarketDataRecoveryResult(call.Arg<MarketDataRecoveryRequest>().OperationId,"Options","GLBX.MDP3",true,true,true,null,after.WorkerInstanceId,after.GenerationId);});
        market.GetBackendReadSnapshotAsync(Arg.Any<CancellationToken>()).Returns(_=>new MarketDataReadSnapshot(Status(),[],[],[],
            NativeOptionWorkload:complete?new(after.GenerationId,clock.GetUtcNow(),false,[],[],[]):null));
        var sessions=Substitute.For<IFuturesMarketSessionAuthority>();sessions.Current.Returns(new MarketSessionReadModel{ActiveValueDate=date});
        var supervisor=Substitute.For<IActorSupervisor>();supervisor.IsReady.Returns(true);supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
        var runtime=new OptionPipelineRecoveryRuntime(supervisor,market,sessions,new TradeRealtimeCacheRegistry(),clock);
        var coordinator=new OptionPipelineRecoveryCoordinator(runtime,NullLogger<OptionPipelineRecoveryCoordinator>.Instance,clock);
        coordinator.Observe(default);await requested.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if(complete){await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(3));Assert.Equal("Healthy",coordinator.State);}
        else{
            clock.Advance(TimeSpan.FromSeconds(119));await Task.Delay(30);Assert.Equal("Recovering",coordinator.State);Assert.Null(coordinator.Failure);
            clock.Advance(TimeSpan.FromSeconds(1));await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(3));Assert.Equal("Failed",coordinator.State);
            Assert.Contains("deadline expired",coordinator.Failure!.ErrorMessage);
        }
    }

    [Theory]
    [InlineData("fresh")]
    [InlineData("native-fresh")]
    [InlineData("missing-subscription")]
    [InlineData("no-quote")]
    [InlineData("hung-repair")]
    [InlineData("wrong-generation")]
    public async Task Missing_leg_repair_uses_original_deadline_and_requires_fresh_end_to_end_proof(string outcome)
    {
        var clock = new LivePipelineIntegrationTests.ManualTime(); var start = clock.GetUtcNow();
        var date = DateOnly.FromDateTime(start.UtcDateTime); var registry = new TradeRealtimeCacheRegistry();
        var trade = Trade(); var cache = registry.Register(trade, date);
        cache.Apply(Position(trade, date, 10, start.AddMinutes(-3)));
        var market = Substitute.For<IMarketDataApi>();
        market.GetRuntimeStatus().Returns(new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataFeedRuntimeStatusReadModel {
            EnabledOptionTrades = [trade.Id.Format()], OptionProcessingError = "Injected failure", OptionProcessingFailureUtc = start });
        var before = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 100, true, true, "Healthy", null, date);
        var after = before with { ProcessId = outcome=="native-fresh"?before.ProcessId:200, WorkerInstanceId=Guid.NewGuid(), GenerationId = Guid.NewGuid() }; var status = before;
        var host=Guid.NewGuid();
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(_ => new MarketDataBackendStatus(true, date, [status],host,RecoveryOwnership:outcome=="native-fresh"?MarketDataRecoveryOwnership.TokioDatasetOwner:MarketDataRecoveryOwnership.Process));
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        market.RecoverMarketDataAsync(Arg.Any<MarketDataRecoveryRequest>(), Arg.Any<CancellationToken>()).Returns(call => {
            status = after; restored.TrySetResult();
            return new MarketDataRecoveryResult(call.Arg<MarketDataRecoveryRequest>().OperationId, "Options", "GLBX.MDP3", true, true, true, null, after.WorkerInstanceId, after.GenerationId);
        });
        var repair = new TaskCompletionSource<OptionObservationRepairRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalled = new TaskCompletionSource<OptionObservationRepairResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        market.RepairOptionObservationsAsync(Arg.Any<OptionObservationRepairRequest>(), Arg.Any<CancellationToken>()).Returns(call => {
            var request = call.Arg<OptionObservationRepairRequest>(); repair.TrySetResult(request);
            if (outcome == "hung-repair") return stalled.Task;
            if (outcome is "fresh" or "native-fresh" or "wrong-generation")
            {
                var fresh = Position(trade, date, 12, clock.GetUtcNow(), after.GenerationId);
                if (outcome == "wrong-generation") fresh = fresh with { Legs = fresh.Legs.Select((leg, index) => index == 3
                    ? leg with { OptionTick = leg.OptionTick! with { SourceGeneration = before.GenerationId },
                        OptionRisk = leg.OptionRisk! with { SourceGeneration = before.GenerationId } } : leg).ToArray() };
                cache.Apply(fresh);
                cache.Apply(new StrategyTradePlanSnapshot { Position = fresh, ValueDate = date, PlanRevision = 2,
                    CalculatedAtUtc = clock.GetUtcNow().UtcDateTime, IronCondorTradePlanSnapshot = new() { Position = fresh, CalculationStatus = "Complete" } });
                registry.RecordPublication(cache.ReadForPublication());
            }
            return Task.FromResult(outcome == "missing-subscription" ? new OptionObservationRepairResult([], ["ES-option-3: no active exact-contract subscription"])
                : new OptionObservationRepairResult(["ES-option-3"], []));
        });
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>(); sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true); supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
        var runtime = new OptionPipelineRecoveryRuntime(supervisor, market, sessions, registry, clock);
        var coordinator = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance, clock);
        coordinator.Observe(default); await restored.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var partial = Position(trade, date, 11, start.AddSeconds(1), after.GenerationId);
        partial = partial with { Legs = partial.Legs.Select((leg, index) => index == 3
            ? leg with { OptionTick = Position(trade, date, 10, start.AddMinutes(-3)).Legs[3].OptionTick,
                OptionRisk = Position(trade, date, 10, start.AddMinutes(-3)).Legs[3].OptionRisk } : leg).ToArray() };
        cache.Apply(partial);
        for (var i = 0; i < 20 && !repair.Task.IsCompleted; i++) { clock.Advance(TimeSpan.FromSeconds(1)); await Task.Delay(15); }
        var requested = await repair.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(["ES-option-3"], requested.Contracts); Assert.Equal(after.GenerationId, requested.GenerationId);
        Assert.Equal(start.AddMinutes(2), requested.DeadlineUtc);
        if (outcome is "fresh" or "native-fresh")
        {
            clock.Advance(TimeSpan.FromSeconds(1)); await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("Healthy", coordinator.State); Assert.Null(coordinator.Failure);
        }
        else
        {
            Assert.Null(coordinator.Failure); Assert.Equal("Recovering", coordinator.State);
            clock.Advance(start.AddSeconds(119) - clock.GetUtcNow()); await Task.Delay(20); Assert.Null(coordinator.Failure);
            clock.Advance(TimeSpan.FromSeconds(1)); await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("Failed", coordinator.State); Assert.Contains("deadline expired", coordinator.Failure!.ErrorMessage);
            Assert.Contains("ES-option-3", coordinator.Failure.ErrorMessage); Assert.Contains("Quote=", coordinator.Failure.ErrorMessage);
            if (outcome == "missing-subscription") Assert.Contains("no active exact-contract subscription", coordinator.Failure.ErrorMessage);
        }
        await market.Received(1).RepairOptionObservationsAsync(Arg.Any<OptionObservationRepairRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Worker_readiness_alone_cannot_pass_until_four_fresh_legs_complete_plan_and_publication_exist()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var registry = new TradeRealtimeCacheRegistry();
        var trade = Trade(); var id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor);
        var cache = registry.Register(trade, date);
        var old = Position(trade, date, 10, DateTimeOffset.UtcNow.AddMinutes(-3)); cache.Apply(old);
        var market = Substitute.For<IMarketDataApi>();
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>(); sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true); supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
        var before = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 100, true, true, "Healthy", null, date);
        var after = before with { ProcessId = 200, GenerationId = Guid.NewGuid() };
        var status = before;
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(_ => new MarketDataBackendStatus(true, date, [status]));
        market.RecoverMarketDataAsync(Arg.Any<MarketDataRecoveryRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<MarketDataRecoveryRequest>();
            Assert.Equal("Options", request.Role); Assert.True(request.ReplaceProcessImmediately); Assert.NotNull(request.DeadlineUtc);
            status = after;
            return new MarketDataRecoveryResult(request.OperationId, "Options", "GLBX.MDP3", true, true, true, null, after.WorkerInstanceId, after.GenerationId);
        });
        var runtime = new OptionPipelineRecoveryRuntime(supervisor, market, sessions, registry);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var recovery = runtime.RecoverAndVerifyAsync(Guid.NewGuid(), new(date, "stalled", [id.Format()]), DateTimeOffset.UtcNow.AddMinutes(2), timeout.Token);
        await Task.Delay(50, timeout.Token); Assert.False(recovery.IsCompleted);
        var now = DateTimeOffset.UtcNow;
        var fresh = Position(trade, date, 11, now, after.GenerationId);
        foreach (var leg in fresh.Legs.Take(3)) cache.Apply(leg.OptionTick!);
        cache.Apply(fresh);
        var plan = new StrategyTradePlanSnapshot { Position = fresh, ValueDate = date, PlanRevision = 1, CalculatedAtUtc = now.UtcDateTime,
            IronCondorTradePlanSnapshot = new() { Position = fresh, CalculationStatus = "Complete" } };
        cache.Apply(plan);
        await Task.Delay(300, timeout.Token); Assert.False(recovery.IsCompleted, "publication must be verified independently of calculations");
        registry.RecordPublication(cache.ReadForPublication());
        await recovery.WaitAsync(timeout.Token);
        Assert.True(registry.HasPublishedPosition(trade.Id, 1, 11));
        Assert.False(registry.HasPublishedPosition(trade.Id, 2, 1));
        Assert.False(registry.HasPublishedPosition(trade.Id, 1, 12));
    }

    [Fact]
    public async Task An_old_Host_that_ignores_immediate_replacement_cannot_report_success()
    {
        var market = Substitute.For<IMarketDataApi>();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var status = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 123, true, true, "Healthy", null, date);
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, date, [status]));
        market.RecoverMarketDataAsync(Arg.Any<MarketDataRecoveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MarketDataRecoveryResult(Guid.NewGuid(), "Options", "GLBX.MDP3", true, true, true, null, status.WorkerInstanceId, Guid.NewGuid()));
        var supervisor = Substitute.For<IActorSupervisor>();
        var runtime = new OptionPipelineRecoveryRuntime(supervisor, market, Substitute.For<IFuturesMarketSessionAuthority>(), new TradeRealtimeCacheRegistry());
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RecoverAndVerifyAsync(Guid.NewGuid(), new(date, "stalled", []), DateTimeOffset.UtcNow.AddMinutes(2), CancellationToken.None));
        Assert.Contains("matching updated Host", failure.Message);
    }

    [Fact]
    public void Explicit_processing_failure_is_detected_even_when_every_quote_is_old()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var cache = new TradeRealtimeCacheRegistry(); var trade = Trade();
        cache.Register(trade, date).Apply(Position(trade, date, 1, DateTimeOffset.UtcNow.AddHours(-1)));
        var market = Substitute.For<IMarketDataApi>();
        market.GetRuntimeStatus().Returns(new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataFeedRuntimeStatusReadModel
        { ActiveValueDate = date, EnabledOptionTrades = [trade.Id.Format()], OptionProcessingError = "LeaseEnded",
            OptionProcessingFailureUtc = DateTimeOffset.UtcNow });
        var session = Substitute.For<IFuturesMarketSessionAuthority>(); session.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true); supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
        var runtime = new OptionPipelineRecoveryRuntime(supervisor, market, session, cache);
        var inspection = runtime.Inspect();
        Assert.Equal("LeaseEnded", inspection.Failure); Assert.Single(inspection.Positions);
    }

    [Fact]
    public void Disabled_live_intent_does_not_trigger_stale_quote_recovery()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var cache = new TradeRealtimeCacheRegistry(); var trade = Trade();
        cache.Register(trade, date).Apply(Position(trade, date, 1, DateTimeOffset.UtcNow.AddHours(-1)));
        var market = Substitute.For<IMarketDataApi>();
        market.GetRuntimeStatus().Returns(new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataFeedRuntimeStatusReadModel { EnabledOptionTrades = [] });
        var session = Substitute.For<IFuturesMarketSessionAuthority>(); session.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true); supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
        var runtime = new OptionPipelineRecoveryRuntime(supervisor, market, session, cache);
        Assert.Null(runtime.Inspect().Failure); Assert.Empty(runtime.Inspect().Positions);
    }

    [Fact]
    public void Enabled_trade_with_all_quotes_stale_remains_monitored_and_starts_recovery()
    {
        var clock = new LivePipelineIntegrationTests.ManualTime(); var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var cache = new TradeRealtimeCacheRegistry(); var trade = Trade();
        cache.Register(trade, date).Apply(Position(trade, date, 1, clock.GetUtcNow().AddHours(-1)));
        var market = Substitute.For<IMarketDataApi>();
        market.GetRuntimeStatus().Returns(new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataFeedRuntimeStatusReadModel { EnabledOptionTrades = [trade.Id.Format()] });
        var session = Substitute.For<IFuturesMarketSessionAuthority>(); session.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true); supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
        var runtime = new OptionPipelineRecoveryRuntime(supervisor, market, session, cache, clock);
        Assert.Null(runtime.Inspect().Failure);
        clock.Advance(TimeSpan.FromSeconds(31));
        var inspection = runtime.Inspect(); Assert.Single(inspection.Positions); Assert.Contains("no current quote progress", inspection.Failure);
    }

    [Fact]
    public async Task Processing_failure_from_stale_trade_reaches_deadline_UI_event_without_hanging()
    {
        var clock = new LivePipelineIntegrationTests.ManualTime(); var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var cache = new TradeRealtimeCacheRegistry(); var trade = Trade();
        cache.Register(trade, date).Apply(Position(trade, date, 1, clock.GetUtcNow().AddHours(-1)));
        var market = Substitute.For<IMarketDataApi>();
        market.GetRuntimeStatus().Returns(new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataFeedRuntimeStatusReadModel
        { EnabledOptionTrades = [trade.Id.Format()], OptionProcessingError = "LeaseEnded", OptionProcessingFailureUtc = clock.GetUtcNow() });
        var status = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 100, true, false, "Healthy", "LeaseEnded", date);
        market.GetBackendStatusAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataBackendStatus(true, date, [status]));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalled = new TaskCompletionSource<MarketDataRecoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        market.RecoverMarketDataAsync(Arg.Any<MarketDataRecoveryRequest>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return stalled.Task; });
        var session = Substitute.For<IFuturesMarketSessionAuthority>(); session.Current.Returns(new MarketSessionReadModel { ActiveValueDate = date });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true); supervisor.RuntimeContext.Returns(new SupervisorRuntimeContext());
        var runtime = new OptionPipelineRecoveryRuntime(supervisor, market, session, cache, clock);
        var coordinator = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance, clock);
        coordinator.Observe(default); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        clock.Advance(TimeSpan.FromSeconds(119)); Assert.Null(coordinator.Failure);
        clock.Advance(TimeSpan.FromSeconds(1)); await coordinator.Completion!.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("Failed", coordinator.State); Assert.Contains("deadline expired", coordinator.Failure!.ErrorMessage);
        Assert.Contains(trade.Id.Format(), coordinator.Failure.ErrorMessage);
        Assert.Equal(date, coordinator.Failure.EntityId.ValueDate);
    }

    static EstablishedTradeDefinition Trade() => new() { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
        Legs = Enumerable.Range(0, 4).Select(index => new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = $"ES-option-{index}",
            SignedQuantity = index % 2 == 0 ? -1 : 1, PutCall = index < 2 ? (byte)2 : (byte)1 }).ToArray() };
    static StrategyPositionSnapshot Position(EstablishedTradeDefinition trade, DateOnly date, long sequence, DateTimeOffset at, Guid sourceGeneration = default) => new()
    {
        Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor), StrategyKind = TradeStrategyKind.IronCondor,
        ValueDate = date, IsOpen = true, RouteGeneration = 1, PositionSequence = sequence, AsOfUtc = at.UtcDateTime,
        Legs = trade.Legs.Select(leg => {
            var tick = new FuturesOptionTickDataV2ReadModel { ContractId = leg.ContractId, ValueDate = date, SourceGeneration = sourceGeneration, ObservedAtUtc = at, CalculatedAtUtc = at,
                ValidUntilUtc = at.AddMinutes(1), OptionPrice = 10, BidPrice = 9, AskPrice = 11, UnderlyingPrice = 6000,
                ImpliedVolatility = 0.2, Delta = 0.1, GreeksAvailable = true, TheoreticalPrice = 10 };
            return new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                PutCall = leg.PutCall, CurrentPrice = 10, OptionTick = tick, OptionRisk = tick };
        }).ToArray()
    };
}
