using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using System.Collections.Immutable;
using Xunit;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class LivePipelineProbeIntegrationTests
{
    [Fact]
    public async Task Shared_messaging_probe_requires_a_real_connection_and_round_trip()
    {
        await using var messaging = new NatsConnectionManager();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Assert.Null(await messaging.ProbeAsync(deadline.Token));
        await messaging.GetClientAsync(Environment.GetEnvironmentVariable("IFM_TEST_NATS_URL") ?? "nats://localhost:4222", deadline.Token);
        Assert.NotNull(await messaging.ProbeAsync(deadline.Token));
    }

    [Fact]
    public async Task Buffered_native_and_aggregation_work_must_drain_even_when_producer_is_quiet()
    {
        await using var f = await Fixture.Create();
        var original = f.Epoch.GetHealth();
        var native = new FeedHealthSnapshot(default, default, 100, 10, 10, 20, 10, 1, 0, 0, 0, null)
            { TransportReady = true, TradingReady = true, ChannelBatchCount = 1 };
        var dataset = new DatabentoDatasetFeedHealth("test", Guid.NewGuid(), native, default);
        f.Epoch.GetHealth().Returns(original with { DatasetFeedStatuses = [dataset] });
        await f.Probe.CheckAsync(default);
        var stalled = await f.Probe.CheckAsync(default);
        Assert.Contains(stalled.Checks, x => x.Component == "Native delivery" && x.Status == "Degraded");
        Assert.Contains(stalled.Checks, x => x.Component == "Aggregation" && x.Status == "Degraded");
        f.Epoch.GetHealth().Returns(original with { DatasetFeedStatuses = [dataset with
            { Health = native with { RingUsedRecords = 0, ChannelBatchCount = 0, RecordsConsumed = 20 } }] });
        var drained = await f.Probe.CheckAsync(default);
        Assert.Contains(drained.Checks, x => x.Component == "Native delivery" && x.Status == "Healthy");
        Assert.Contains(drained.Checks, x => x.Component == "Aggregation" && x.Status == "Healthy");
    }

    [Fact]
    public async Task Already_running_startup_still_ensures_tick_routes_and_chart_timer()
    {
        await using var f = await Fixture.Create();
        var contracts = new[] { "ES", "VX", "VX" }.Select((symbol, i) => new FuturesContractV3ReadModel(
            symbol + "STARTUP" + i, symbol, symbol, symbol + "U6", "FUT", "USD", "CME", "50", f.Date.AddDays(i + 1), true)).ToArray();
        var authority = Substitute.For<IDatabentoContractAuthority>();
        authority.ReconcileAsync(f.Date, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(contracts.Select(c => new FuturesRolloverContractAssignment
        {
            ContractRole = DatabentoContractRole.EsQuarterly, RootSymbol = c.Symbol, ContractId = c.ContractId,
            Description = c.Symbol, LocalSymbol = c.Symbol, SecurityType = "FUT", Currency = "USD", Exchange = "CME", Multiplier = "50",
            LastTradeDate = f.Date.AddDays(10), NextRolloverDate = f.Date.AddDays(9), SourceContractHash = "test",
            CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow, CreatedBy = "test", UpdatedBy = "test"
        }).ToArray());
        var catalog = Substitute.For<ICurrentFuturesContractCatalog>();
        catalog.GetByRootAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => contracts.Where(c => c.Symbol == call.Arg<string>()).ToArray());
        var commands = Substitute.For<IMarketDataFeedCommandApi>();
        commands.StartFuturesBarDataStreamingAsync(Arg.Any<FuturesContractV3ReadModel[]>(), f.Date).Returns(new ServiceResult<Guid>(Guid.NewGuid()));
        commands.StartFuturesTickDataStreamingAsync(Arg.Any<FuturesContractV3ReadModel>(), f.Date, false).Returns(new ServiceResult<Guid>(Guid.NewGuid()));
        var queries = Substitute.For<IMarketDataFeedQueryApi>();
        queries.GetRuntimeStatusAsync().Returns(new ServiceResult<MarketDataFeedRuntimeStatusReadModel>(new MarketDataFeedRuntimeStatusReadModel()
        { IsRunning = true, ActiveValueDate = f.Date, ObservedAtUtc = DateTimeOffset.UtcNow }));
        var activities = new ApiApplicationStartupActivities(f.Sessions, authority, catalog, null!, commands, queries,
            null!, null!, f.Storage, f.Market, null!, null!, null!, new(), new(), TimeProvider.System, NullLogger<ApiApplicationStartupActivities>.Instance);
        var context = new ApplicationStartupContext(f.Date, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await activities.ReconcileCurrentContractsAsync(context, default);
        Assert.Equal(ApplicationStartupActivityOutcome.AlreadySatisfied, await activities.StartMarketDataAsync(context, default));
        await commands.Received(1).StartFuturesBarDataStreamingAsync(Arg.Is<FuturesContractV3ReadModel[]>(x => x.Length == 3), f.Date);
        await commands.Received(3).StartFuturesTickDataStreamingAsync(Arg.Any<FuturesContractV3ReadModel>(), f.Date, false);
        await f.Epoch.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Healthy_feed_does_not_hide_missing_timer_stale_bars_or_missing_analytics()
    {
        await using var fixture = await Fixture.Create();
        var result = await fixture.Probe.CheckAsync(default);
        Assert.Contains(result.Checks, x => x.Component == "Deployment identity" && x.Status == "Healthy");
        Assert.Contains(result.Checks, x => x.Component == "Databento feed" && x.Status == "Healthy");
        Assert.Contains(result.Checks, x => x.Component == "Bar timer" && x.Status == "Degraded");
        Assert.Contains(result.Checks, x => x.Component == "Chart storage/query" && x.Status == "Degraded");
        Assert.Contains(result.Checks, x => x.Component == "Analytics attachments" && x.Status == "Degraded");
        Assert.False(result.AllowsNewDecisions);
    }

    [Fact]
    public async Task Actual_probe_observes_timer_and_storage_recovery_without_restarting_feed()
    {
        await using var fixture = await Fixture.Create();
        fixture.Timer.Start(new(fixture.Date), () => ValueTask.CompletedTask);
        fixture.FreshBars = true;
        var result = await fixture.Probe.CheckAsync(default);
        Assert.Contains(result.Checks, x => x.Component == "Bar timer" && x.Status == "Healthy");
        Assert.All(result.Checks.Where(x => x.Component == "Chart storage/query"), x => Assert.Equal("Healthy", x.Status));
        await fixture.Epoch.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Iti_prerequisite_failure_and_successful_no_signal_evaluation_are_distinct()
    {
        await using var fixture = await Fixture.Create();
        fixture.Evidence.Record("ITI", "ES", "Degraded", "Waiting for a fresh VX trade price.");
        Assert.Contains((await fixture.Probe.CheckAsync(default)).Checks, x => x.Component == "ITI" && x.Status == "Degraded");
        fixture.Evidence.Record("ITI", "ES", "Healthy", "Evaluated; no signal conditions met.", DateTime.UtcNow);
        Assert.Contains((await fixture.Probe.CheckAsync(default)).Checks, x => x.Component == "ITI" && x.Status == "Healthy");
    }

    [Fact]
    public async Task Storage_exception_degrades_component_and_does_not_abort_other_checks()
    {
        await using var fixture = await Fixture.Create();
        fixture.Storage.MarketDataDb.GetLastFuturesBarDataAsync(Arg.Any<string>(), Arg.Any<string>(), fixture.Date)
            .Returns<Task<FuturesBarDataReadModel>>(_ => throw new IOException("Injected storage outage"));
        var result = await fixture.Probe.CheckAsync(default);
        Assert.Contains(result.Checks, x => x.Component == "Chart storage/query" && x.Reason.Contains("Injected storage outage"));
        Assert.Contains(result.Checks, x => x.Component == "ITI");
    }

    [Fact]
    public async Task Targeted_chart_recovery_starts_missing_timer_without_resetting_dataset()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Probe.RecoverDownstreamAsync(
            new("Bar timer", fixture.Date.ToString("yyyy-MM-dd"), "Degraded", "missing", DateTime.UtcNow),
            fixture.Date, default);
        await fixture.FeedCommands.Received(1).StartFuturesBarDataStreamingAsync(
            Arg.Is<FuturesContractV3ReadModel[]>(contracts => contracts.Length == 2), fixture.Date);
        await fixture.FeedCommands.DidNotReceive().StopFuturesBarDataStreamingAsync(fixture.Date);
    }

    [Fact]
    public async Task Targeted_stale_chart_recovery_restarts_only_chart_streaming()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Probe.RecoverDownstreamAsync(
            new("Chart storage/query", "ES", "Degraded", "stale", DateTime.UtcNow),
            fixture.Date, default);
        await fixture.FeedCommands.Received(1).StopFuturesBarDataStreamingAsync(fixture.Date);
        await fixture.FeedCommands.Received(1).StartFuturesBarDataStreamingAsync(
            Arg.Any<FuturesContractV3ReadModel[]>(), fixture.Date);
    }

    [Fact]
    public async Task Targeted_indicator_recovery_starts_only_requested_indicator_and_timeframe()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Probe.RecoverDownstreamAsync(
            new("Analytics attachments", "RSI/FifteenSeconds", "Degraded", "missing", DateTime.UtcNow),
            fixture.Date, default);
        await fixture.AnalyticsCommands.Received(1).StartFuturesRsiSignalAsync(
            Arg.Is<FuturesRsiSignalEntityId>(id => id.TimePeriod == TimeFrameType.FifteenSeconds));
        await fixture.AnalyticsCommands.DidNotReceiveWithAnyArgs()
            .StartFuturesAtrSignalAsync(default!);
    }

    [Fact]
    public async Task Targeted_iti_recovery_restarts_only_iti_realtime_actor()
    {
        await using var fixture = await Fixture.Create();
        var iti = new ActorMailboxId(ActorType.Realtime, FuturesItiSignalRealtimeActor.ActorName);
        fixture.Actors.ActorExists(iti).Returns(true);
        await fixture.Probe.RecoverDownstreamAsync(
            new("ITI route", "ES", "Degraded", "missing", DateTime.UtcNow),
            fixture.Date, default);
        await fixture.Actors.Received(1).StopAsync(iti, Arg.Any<CancellationToken>());
        await fixture.Actors.Received(1).StartAsync(iti, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Minute_monitor_uses_real_probe_to_recover_chart_bars_when_ticks_are_healthy()
    {
        await using var fixture = await Fixture.Create();
        var now = DateTime.UtcNow;
        fixture.Evidence.Record("Tick storage", "ES20260918", "Healthy", "stored", now);
        fixture.Evidence.Record("Tick storage", "VX20260918", "Healthy", "stored", now);
        var time = new LivePipelineIntegrationTests.ManualTime();
        using var monitor = new LivePipelineMonitor(
            fixture.Probe, time, NullLogger<LivePipelineMonitor>.Instance);
        await monitor.CheckOnceAsync(default);
        await fixture.FeedCommands.DidNotReceiveWithAnyArgs()
            .StartFuturesBarDataStreamingAsync(default!, default);
        time.Advance(TimeSpan.FromMinutes(1));
        await monitor.CheckOnceAsync(default);
        await fixture.FeedCommands.Received(1).StartFuturesBarDataStreamingAsync(
            Arg.Is<FuturesContractV3ReadModel[]>(contracts => contracts.Length == 2), fixture.Date);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public DateOnly Date = DateOnly.FromDateTime(DateTime.UtcNow);
        public bool FreshBars;
        public LivePipelineProbe Probe = null!;
        public FuturesBarDataTimer Timer = new();
        public IDatabentoMarketDataEpoch Epoch = Substitute.For<IDatabentoMarketDataEpoch>();
        public IDbContextFactory Storage = Substitute.For<IDbContextFactory>();
        public LivePipelineEvidence Evidence = new(TimeProvider.System);
        public IMarketDataFeedCommandApi FeedCommands = Substitute.For<IMarketDataFeedCommandApi>();
        public IMarketDataAnalyticsCommandApi AnalyticsCommands = Substitute.For<IMarketDataAnalyticsCommandApi>();
        public IActorSupervisor Actors = Substitute.For<IActorSupervisor>();
        DatabentoMarketDataApi market = null!;
        public DatabentoMarketDataApi Market => market;
        public IFuturesMarketSessionAuthority Sessions = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); var now = DateTimeOffset.UtcNow;
            var session = Substitute.For<IFuturesMarketSessionAuthority>();
            f.Sessions = session;
            session.Current.Returns(new MarketSessionReadModel { OperationalValueDate = f.Date, ActiveValueDate = f.Date,
                Revision = 1, AsOfUtc = now.UtcDateTime, SessionStartUtc = now.AddHours(-1).UtcDateTime,
                SessionEndUtc = now.AddHours(1).UtcDateTime, NextTransitionUtc = now.AddHours(1).UtcDateTime,
                State = FuturesMarketState.OffTrading });
            var registry = Substitute.For<IDatabentoContractRegistrationRegistry>();
            foreach (var symbol in new[] { "ES", "VX" })
            {
                var contract = new FuturesContractV3ReadModel(symbol + "20260918", symbol, symbol, symbol + "U6", "FUT", "USD", "CME", "50", f.Date.AddDays(10), true);
                registry.TryGetOnTheRunFuturesContract(symbol, out Arg.Any<FuturesContractV3ReadModel>()).Returns(call => { call[1] = contract; return true; });
                f.Storage.MarketDataDb.GetLastFuturesBarDataAsync(contract.ContractId, symbol, f.Date)
                    .Returns(_ => new FuturesBarDataReadModel { ContractId = contract.ContractId, Symbol = symbol, ValueDate = f.Date,
                        BarDate = f.FreshBars ? DateTime.UtcNow : now.AddHours(-1).UtcDateTime, BarValue = 10 });
            }
            f.Epoch.ValueDate.Returns(f.Date);
            f.Epoch.IsFeedUp(Arg.Any<TimeSpan>()).Returns(true);
            f.Epoch.IsTickDataStreamActive(Arg.Any<string>()).Returns(true);
            f.Epoch.GetHealth().Returns(new DatabentoMarketDataEpochHealth(f.Date, true, true, 2, 2, true, 10, 10, 0,
                ContractStatuses: new[] { "ES", "VX" }.Select(symbol => new TickAggregationContractStatus(symbol + "20260918", AssetTypeId.Futures,
                    true, true, true, true, now, now, now, now, now, now, 10)).ToArray()));
            var factory = Substitute.For<IDatabentoMarketDataEpochFactory>(); factory.Create(f.Date).Returns(f.Epoch);
            f.market = new(factory, new(), contractRegistry: registry); await f.market.StartAsync(f.Date);
            var actors = f.Actors; actors.IsReady.Returns(true);
            actors.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
            var actorRegistry = Substitute.For<IActorRegistry>();
            var operations = new MarketDataOperationsHealthService(new DatasetWorkerAdmissionRegistry());
            var watchdog = new DatabentoMarketDataWatchdogService(Substitute.For<IDatabentoLifecycleRuntime>(),
                Substitute.For<IMarketDataServiceStore>(), session, Substitute.For<IDatabentoWatchdogPublisher>(),
                operations, new(), new(), TimeProvider.System, NullLogger<DatabentoMarketDataWatchdogService>.Instance);
            f.FeedCommands.StartFuturesBarDataStreamingAsync(Arg.Any<FuturesContractV3ReadModel[]>(), f.Date)
                .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
            f.FeedCommands.StopFuturesBarDataStreamingAsync(f.Date)
                .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
            f.AnalyticsCommands.StartFuturesRsiSignalAsync(Arg.Any<FuturesRsiSignalEntityId>())
                .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
            f.AnalyticsCommands.StartFuturesAtrSignalAsync(Arg.Any<FuturesAtrSignalEntityId>())
                .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
            f.AnalyticsCommands.StartFuturesAdxSignalAsync(Arg.Any<FuturesAdxSignalEntityId>())
                .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
            f.AnalyticsCommands.StartFuturesMacdSignalAsync(Arg.Any<FuturesMacdSignalEntityId>())
                .Returns(new ServiceResult<Guid>(Guid.NewGuid()));
            f.Probe = new(new(f.market, session, Substitute.For<IFuturesContractRolloverStore>(), Substitute.For<IFuturesExchangeBusinessCalendar>(), TimeProvider.System),
                new(actors, actorRegistry), f.market, session, f.Timer, f.FeedCommands, f.AnalyticsCommands,
                f.Storage, operations, f.Evidence,
                new DeploymentIdentityMonitor(new(), AppContext.BaseDirectory, false), watchdog, TimeProvider.System,
                actors, null);
            return f;
        }
        public async ValueTask DisposeAsync() { await Timer.StopAllAsync(); await market.DisposeAsync(); }
    }
}
