using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;
public sealed class EstablishedIronCondorViewModelTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Loaded_trade_restores_saved_live_intent_and_only_explicit_off_deletes_it(bool saved, bool historical)
    {
        var date = new DateOnly(2026,10,22);
        var trade = new EstablishedTradeDefinition {
            Id = new(101,701,1701,1101), StrategyKind = TradeStrategyKind.IronCondor, Status = EstablishedTradeStatus.Open,
            EstablishedAtUtc = date.ToDateTime(new TimeOnly(12,0),DateTimeKind.Utc),
            Legs = Enumerable.Range(1,4).Select(i=>new TradeLegDefinition { TradeLegId=Guid.NewGuid(), ContractId="ES-option-"+i, Expiry=date.AddDays(30) }).ToArray()
        };
        var recovery = Substitute.For<ITradeFeedRecoveryApi>();
        recovery.LoadAsync(trade.Id,date,Arg.Any<CancellationToken>()).Returns(saved);
        recovery.EnableAsync(trade.Id,date,Arg.Any<CancellationToken>()).Returns(true);
        recovery.DisableAsync(trade.Id,date,Arg.Any<CancellationToken>()).Returns(true);
        var queries = Substitute.For<IEstablishedTradeQueryApi>();
        queries.GetAsync(trade.Id,TradeStrategyKind.IronCondor,Arg.Any<CancellationToken>()).Returns(new ServiceOk<EstablishedTradeDefinition>(StrategyPositionHistoryProjection.EnrichTrade(trade)));
        queries.GetRealtimeAsync(trade.Id,date,Arg.Any<CancellationToken>()).Returns(new ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot>((TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot)null!));
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services);
        services.EstablishedTrades.Returns(queries); ConfigureTradeLimits(services);
        var positions = Substitute.For<IStrategyPositionQueryApi>();
        positions.GetHistoryAsync(Arg.Any<StrategyPositionId>(),TradeStrategyKind.IronCondor,Arg.Any<DateTime>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<StrategyPositionSnapshot[]>([]));
        services.StrategyPositions.Returns(new TomasAI.IFM.UI.Net.Services.Trade.StrategyPositionService(Substitute.For<IStrategyPositionCommandApi>(),positions));
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var market = Substitute.For<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi>();
        market.GetMarketSessionAsync().Returns(new ServiceOk<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.MarketSessionReadModel>(ServerSession()));
        market.GetValueDateAsync().Returns(new ServiceOk<ScalarReadModel<DateOnly>>(new(date)));
        services.MarketDataQueries.Returns(new TomasAI.IFM.UI.Net.Services.MarketData.MarketDataQueryService(market,Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi>()));
        services.FeedCommands.Returns(new TomasAI.IFM.UI.Net.Services.MarketDataFeed.MarketDataFeedCommandService(
            Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedCommandApi>(),market,
            Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesEodDataUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesTradeSignalUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesOptionTickDataUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IMarketDataFeedResetUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IMarketDataFeedStatusUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesBarDataUIEventConsumer>(),recovery));
        services.TradePositionEvents.Returns(new TomasAI.IFM.UI.Net.Services.Trade.TradePositionFeedEventService(Substitute.For<TomasAI.IFM.UI.EventConsumer.ITradePositionUIEventConsumer>()));
        services.TradePlanEvents.Returns(new TomasAI.IFM.UI.Net.Services.Trade.TradePlanEventService(Substitute.For<TomasAI.IFM.UI.EventConsumer.ITradePlanUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services); root.AppEnvironment.Returns("Development");
        var vm = new IronCondorViewModel(root,new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel{OrderId=1701}),new PortfolioFundOrderTradeEditorModel{TradeId=1101},
            historical ? date.AddDays(-1) : null,[],historicalReadOnly:historical,establishedTrade:StrategyPositionHistoryProjection.EnrichTrade(trade),brokerEnvironment:BrokerEnvironment.Emulator);
        vm.ApplyMarketSessionSnapshot(ServerSession());
        await vm.LoadEstablishedTradeAsync();
        Assert.Equal(saved && !historical,vm.IsLiveFeedEnabled);
        if (!historical) await recovery.Received(1).LoadAsync(trade.Id,date,Arg.Any<CancellationToken>());
        else await recovery.DidNotReceive().LoadAsync(Arg.Any<TradeEntityId>(),Arg.Any<DateOnly>(),Arg.Any<CancellationToken>());
        if (saved && !historical) {
            await recovery.Received(1).EnableAsync(trade.Id,date,Arg.Any<CancellationToken>());
            await vm.StopAsync(default); // Internal cleanup, including API disconnection, retains intent.
            await recovery.DidNotReceive().DisableAsync(Arg.Any<TradeEntityId>(),Arg.Any<DateOnly>(),Arg.Any<CancellationToken>());
            await vm.DisableLiveFeedAsync();
            await recovery.Received(1).DisableAsync(trade.Id,date,Arg.Any<CancellationToken>());
        }
        await vm.DisposeAsync();
    }

    [Fact]
    public async Task Persisted_EOD_refreshes_open_trade_after_feed_stop_without_reopening_or_restarting_live_feed()
    {
        var date = new DateOnly(2026, 10, 21);
        var trade = new EstablishedTradeDefinition
        {
            Id = new(101, 701, 1701, 1101), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = date.ToDateTime(new TimeOnly(14, 0), DateTimeKind.Utc),
            Legs = [new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = "option", Expiry = date.AddDays(30), SignedQuantity = -1 }]
        };
        var id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor);
        var intraday = new StrategyPositionSnapshot { Id = id, StrategyKind = TradeStrategyKind.IronCondor,
            ValueDate = date, Phase = StrategyPositionPhase.MarkToMarket, RouteGeneration = 9, PositionSequence = 900,
            AsOfUtc = date.ToDateTime(new TimeOnly(20, 59), DateTimeKind.Utc), DailyPnl = 75 };
        var eod = intraday with { Phase = StrategyPositionPhase.EndOfDay, RouteGeneration = 1, PositionSequence = 10 };
        StrategyPositionSnapshot[] persisted = [intraday];
        var positions = Substitute.For<IStrategyPositionQueryApi>();
        positions.GetHistoryAsync(id, TradeStrategyKind.IronCondor, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ServiceOk<StrategyPositionSnapshot[]>(persisted.Select(p => StrategyPositionHistoryProjection.EnrichPosition(p, trade)).ToArray()));
        var trades = Substitute.For<IEstablishedTradeQueryApi>();
        trades.GetAsync(trade.Id, TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>()).Returns(new ServiceOk<EstablishedTradeDefinition>(StrategyPositionHistoryProjection.EnrichTrade(trade)));
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services);
        ConfigureTradeLimits(services);
        services.EstablishedTrades.Returns(trades);
        services.StrategyPositions.Returns(new TomasAI.IFM.UI.Net.Services.Trade.StrategyPositionService(Substitute.For<IStrategyPositionCommandApi>(), positions));
        var status = Substitute.For<TomasAI.IFM.UI.EventConsumer.IMarketDataFeedStatusUIEventConsumer>();
        Func<IEvent, ValueTask>? onStatus = null;
        status.StartMonitoringAsync(Arg.Any<Guid>(), Arg.Any<Func<IEvent, ValueTask>>())
            .Returns(call => { onStatus = call.ArgAt<Func<IEvent, ValueTask>>(1); return ValueTask.CompletedTask; });
        services.FeedCommands.Returns(new TomasAI.IFM.UI.Net.Services.MarketDataFeed.MarketDataFeedCommandService(
            Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedCommandApi>(),
            Substitute.For<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi>(),
            Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesEodDataUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesTradeSignalUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesOptionTickDataUIEventConsumer>(),
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IMarketDataFeedResetUIEventConsumer>(), status,
            Substitute.For<TomasAI.IFM.UI.EventConsumer.IFuturesBarDataUIEventConsumer>()));
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
        var vm = new IronCondorViewModel(root, new PortfolioFundEditorModel(701, "Fund", "", 0, false, DateTime.UtcNow, "test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade: StrategyPositionHistoryProjection.EnrichTrade(trade));
        vm.ApplyMarketSessionSnapshot(ServerSession());
        await vm.LoadEstablishedTradeAsync();
        onStatus.Should().NotBeNull("a loaded trade listens even without live quote monitoring");
        vm.TradeHistory.Last().TradeStatus.Should().Be(TradeStatus.IntraDay);
        typeof(IronCondorViewModel).GetProperty(nameof(vm.IsLiveFeedEnabled))!.SetValue(vm, true);
        await onStatus!(new TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataFeedStoppedCompleteEvent { ValueDate = date });
        vm.IsLiveFeedEnabled.Should().BeFalse();
        await status.DidNotReceive().StopMonitoringAsync(Arg.Any<Guid>());
        await positions.Received(1).GetHistoryAsync(id, TradeStrategyKind.IronCondor, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        var committed = new TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.ScheduledTaskRunStageRecordedCompleteEvent
        {
            ScheduledTaskRun = new()
            {
                TaskKey = TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.ScheduledTaskKeys.FuturesMarketClose,
                Stage = "FeedsStopped", ValueDate = date
            }
        };
        await onStatus(committed); // Earlier stages must not query history before EOD persistence.
        await positions.Received(1).GetHistoryAsync(id, TradeStrategyKind.IronCondor, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        persisted = [intraday, eod];
        committed = committed with { ScheduledTaskRun = committed.ScheduledTaskRun with { Stage = "PositionsFinalized", CompletedEndOfDayValueDate = date } };
        var bytes = MessagePack.MessagePackSerializer.Serialize(committed);
        await onStatus(MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.ScheduledTaskRunStageRecordedCompleteEvent>(bytes));
        vm.TradeHistory.Last().ValueDate.Should().Be(date);
        vm.TradeHistory.Last().TradeStatus.Should().Be(TradeStatus.EndOfDay, "sealed EOD wins even over a higher transient generation/sequence");
        vm.TradeHistory.Should().HaveCount(2);
        vm.IsLiveFeedEnabled.Should().BeFalse();
        await onStatus(committed); // Duplicate completion does not fetch the same saved EOD repeatedly.
        await positions.Received(2).GetHistoryAsync(id, TradeStrategyKind.IronCondor, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await trades.Received(1).GetAsync(trade.Id, TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Terminal_option_failure_turns_live_off_reports_error_and_only_allows_confirmed_restart(bool cleanupConfirmed)
    {
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services);
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services); root.AppEnvironment.Returns("Development");
        var trade = new EstablishedTradeDefinition { Id = new(101,701,1701,1101), StrategyKind = TradeStrategyKind.IronCondor,
            Status = EstablishedTradeStatus.Open };
        var vm = new IronCondorViewModel(root, new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade: StrategyPositionHistoryProjection.EnrichTrade(trade), brokerEnvironment: BrokerEnvironment.Emulator);
        vm.ApplyMarketSessionSnapshot(ServerSession());
        typeof(IronCondorViewModel).GetProperty(nameof(vm.IsLiveFeedEnabled))!.SetValue(vm, true);
        var failed = new TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.OptionMarketDataFeedFailedEvent
        {
            Dataset = "GLBX.MDP3", WorkerInstanceId = Guid.NewGuid(), GenerationId = Guid.NewGuid(),
            CommandId = Guid.NewGuid(), CleanupConfirmed = cleanupConfirmed, ErrorDate = DateTime.UtcNow,
            ErrorMessage = "Injected option recovery timeout", ErrorCode = 503
        };
        var wire = MessagePack.MessagePackSerializer.Serialize(failed);
        await vm.ApplyMarketDataFeedStatusAsync(MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.OptionMarketDataFeedFailedEvent>(wire));
        vm.IsLiveFeedEnabled.Should().BeFalse();
        vm.CanEnableLiveFeed.Should().Be(cleanupConfirmed);
        vm.LiveFeedStatusMessage.Should().Contain(failed.ErrorMessage);
        vm.LastError.Should().NotBeNull();
        await vm.ApplyMarketDataFeedStatusAsync(failed with { ErrorDate = failed.ErrorDate.AddSeconds(-1), ErrorMessage = "stale notification" });
        vm.LiveFeedStatusMessage.Should().NotContain("stale notification");
    }
    [Fact]
    public async Task Feed_stop_notification_turns_monitoring_off_and_feed_restart_requires_manual_reactivation()
    {
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services);
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services); root.AppEnvironment.Returns("Development");
        var trade = new EstablishedTradeDefinition { Id = new(101,701,1701,1101), StrategyKind = TradeStrategyKind.IronCondor,
            Status = EstablishedTradeStatus.Open };
        var vm = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade: StrategyPositionHistoryProjection.EnrichTrade(trade), brokerEnvironment: BrokerEnvironment.Emulator);
        vm.ApplyMarketSessionSnapshot(ServerSession());
        typeof(IronCondorViewModel).GetProperty(nameof(vm.IsLiveFeedEnabled))!.SetValue(vm,true);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var stopped = new TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataFeedStoppedCompleteEvent();
        await vm.ApplyMarketDataFeedStatusAsync(stopped);
        await vm.ApplyMarketDataFeedStatusAsync(stopped);
        vm.IsLiveFeedEnabled.Should().BeFalse();
        vm.CanEnableLiveFeed.Should().BeFalse();
        vm.CanSelectPositionHistory.Should().BeTrue();
        vm.LiveFeedStatusMessage.Should().Contain("closed by the API");
        changed.Should().Contain(nameof(vm.IsLiveFeedEnabled));
        await vm.ApplyMarketDataFeedStatusAsync(new TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataFeedStartedCompleteEvent());
        vm.CanEnableLiveFeed.Should().BeTrue();
        vm.IsLiveFeedEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadsExactNewTradeModel_AndRejectsWrongIdentity(bool mismatch)
    {
        var actual = new EstablishedTradeDefinition {
            Id = new(101, 701, 1701, 1101), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = new DateTime(2026, 10, 6, 23, 19, 0, DateTimeKind.Utc),
            OpeningValue = -13.05m, OpeningCommission = 2.6m,
            Legs = [new TradeLegDefinition { Expiry = new DateOnly(2026,11,20) }]
        };
        actual = StrategyPositionHistoryProjection.EnrichTrade(actual);
        var queries = Substitute.For<IEstablishedTradeQueryApi>();
        queries.GetAsync(actual.Id, TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<EstablishedTradeDefinition>(mismatch ? actual with { Id = new(101,701,1601,1001) } : actual));
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services); services.EstablishedTrades.Returns(queries);
        ConfigureTradeLimits(services);
        var positionQueries = Substitute.For<IStrategyPositionQueryApi>();
        positionQueries.GetHistoryAsync(Arg.Any<StrategyPositionId>(), TradeStrategyKind.IronCondor,
            Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<StrategyPositionSnapshot[]>([]));
        services.StrategyPositions.Returns(new TomasAI.IFM.UI.Net.Services.Trade.StrategyPositionService(
            Substitute.For<IStrategyPositionCommandApi>(), positionQueries));
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 701 });
        var viewModel = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"), order,
            new PortfolioFundOrderTradeEditorModel { TradeId = 101 }, null, [], establishedTrade: actual);
        viewModel.ApplyMarketSessionSnapshot(ServerSession());
        if (mismatch)
            await FluentActions.Awaiting(() => viewModel.LoadEstablishedTradeAsync()).Should().ThrowAsync<InvalidOperationException>();
        else
            {
            (await viewModel.LoadEstablishedTradeAsync()).Should().BeSameAs(actual);
            var opening = viewModel.TradeHistory.Should().ContainSingle().Subject;
            opening.OrderId.Should().Be(1701); opening.TradeId.Should().Be(1101);
            opening.TradeStatus.Should().Be(TradeStatus.Open);
            opening.ValueDate.Should().Be(new DateOnly(2026,10,7));
            opening.DaysToExpiry.Should().Be(44);
            opening.NetSpread.Should().Be(-13.05m); opening.Commission.Should().Be(2.6m);
        }
        await queries.Received(1).GetAsync(actual.Id, TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>());
        if (!mismatch)
        {
            viewModel.TradeLimitSnapshot.Should().NotBeNull();
            viewModel.TradeLimitSnapshot!.TradeLimit.TradeId.Should().Be(actual.Id.TradeId);
            viewModel.TradeLimitSnapshot.OrderId.Should().Be(actual.Id.OrderId);
            viewModel.TradeLimitSnapshot.FundBalance.Should().Be(125000m);
            viewModel.TradeLimitSnapshot.TradeLimit.MaxProfit.Should().Be(652.50m);
        }
    }

    [Fact]
    public void Current_position_filters_trade_identity_leg_identity_and_old_generations()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1701, 1101),
            StrategyKind = TradeStrategyKind.IronCondor,
            Legs = ids.Select((id, i) => new TradeLegDefinition { TradeLegId = id, ContractId = $"option-{i}" }).ToArray() };
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services);
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
        var viewModel = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade: StrategyPositionHistoryProjection.EnrichTrade(trade));
        viewModel.ApplyMarketSessionSnapshot(ServerSession());
        var position = new StrategyPositionSnapshot { Id = new(trade.Id, Guid.NewGuid()),
            StrategyKind = TradeStrategyKind.IronCondor, PositionSequence = 10, RouteGeneration = 2,
            AsOfUtc = DateTime.UtcNow, Legs = trade.Legs.Select(leg => new StrategyPositionLeg {
                TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = 1, CurrentPrice = 4m }).ToArray() };
        viewModel.ApplyIronCondorPosition(position).Should().BeTrue();
        viewModel.ApplyIronCondorPosition(position).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { RouteGeneration = 1, PositionSequence = 100 }).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { Id = new(new(101, 701, 1701, 9999), position.Id.PositionId) }).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { PositionSequence = 11, Legs = position.Legs[..3] }).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { RouteGeneration = 3, PositionSequence = 1 }).Should().BeTrue();
        viewModel.GetOptionLegContractIds().Should().HaveCount(4);
    }
    [Fact]
    public async Task Selected_position_date_loads_all_history_pages_without_a_200_row_cap()
    {
        var date = new DateOnly(2026, 10, 6);
        var trade = new EstablishedTradeDefinition {
            Id = new(101, 701, 1701, 1101), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = date.ToDateTime(new TimeOnly(12, 0)),
            Legs = [new TradeLegDefinition { Expiry = date.AddDays(30) }]
        };
        var positionId = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor);
        var latest = new TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot {
            Position = new StrategyPositionSnapshot { Id = positionId, StrategyKind = TradeStrategyKind.IronCondor },
            ValueDate = date, PlanRevision = 201
        };
        var api = Substitute.For<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IStrategyTradePlanQueryApi>();
        api.GetCurrentIronCondorAsync(positionId, date, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot>(latest));
        api.GetIronCondorHistoryAsync(positionId, date, 200, null, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanHistoryPage>(new(
                Enumerable.Range(2,200).Select(i => latest with { PlanRevision = i }).ToArray(), [1])));
        api.GetIronCondorHistoryAsync(positionId, date, 200, Arg.Is<byte[]>(state => state.Length == 1), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanHistoryPage>(new([latest with { PlanRevision = 1 }], null)));
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services);
        services.StrategyTradePlanQueries.Returns(new TomasAI.IFM.UI.Net.Services.Trade.StrategyTradePlanQueryService(api));
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
        var vm = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade: StrategyPositionHistoryProjection.EnrichTrade(trade));
        vm.ApplyMarketSessionSnapshot(ServerSession());
        typeof(IronCondorViewModel).GetProperty(nameof(vm.TradeHistory))!.SetValue(vm,
            new[] { IronCondorViewModel.CreateOpeningPosition(StrategyPositionHistoryProjection.EnrichTrade(trade), TradeType.ShortIronCondor) });
        var displayedCounts = new List<int>();
        vm.PropertyChanged += (_,e) => {
            if (e.PropertyName == nameof(vm.IronCondorPlanHistory)) displayedCounts.Add(vm.IronCondorPlanHistory.Count);
        };
        await vm.LoadTradePlans(0);
        displayedCounts.Should().Equal(0,200,201);
        vm.IronCondorPlanHistory.Should().HaveCount(201);
        vm.IronCondorPlanHistory.Select(plan => plan.ValueDate).Should().OnlyContain(value => value == date);
        vm.IronCondorPlanHistory.First().PlanRevision.Should().Be(201);
        vm.IronCondorPlanHistory.Last().PlanRevision.Should().Be(1);
        services.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "get_TradePlanQueries").Should().BeEmpty();
    }

    [Fact]
    public async Task Loading_trade_reads_all_persisted_daily_positions_in_ascending_order()
    {
        var date = new DateOnly(2026,10,6);
        var trade = new EstablishedTradeDefinition {
            Id = new(101,701,1701,1101), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = date.ToDateTime(new TimeOnly(14,0),DateTimeKind.Utc),
            Legs = Enumerable.Range(0,4).Select(i => new TradeLegDefinition {
                TradeLegId = Guid.NewGuid(),ContractId = $"option-{i}", Expiry = date.AddDays(30), SignedQuantity = i%2==0 ? 1 : -1
            }).ToArray()
        };
        var id = StrategyPositionId.Create(trade.Id,TradeStrategyKind.IronCondor);
        StrategyPositionSnapshot Position(int day,long sequence,StrategyPositionPhase phase) => StrategyPositionHistoryProjection.EnrichPosition(new() {
            Id = id, StrategyKind = TradeStrategyKind.IronCondor, ValueDate = date.AddDays(day),
            PositionSequence = sequence, Phase = phase, RouteGeneration = 1,
            AsOfUtc = date.AddDays(day).ToDateTime(new TimeOnly(20,0),DateTimeKind.Utc), DailyPnl = sequence
        }, trade);
        var queries = Substitute.For<IEstablishedTradeQueryApi>();
        queries.GetAsync(trade.Id,TradeStrategyKind.IronCondor,Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<EstablishedTradeDefinition>(StrategyPositionHistoryProjection.EnrichTrade(trade)));
        var positions = Substitute.For<IStrategyPositionQueryApi>();
        positions.GetHistoryAsync(id,TradeStrategyKind.IronCondor,Arg.Any<DateTime>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<StrategyPositionSnapshot[]>([
                Position(2,30,StrategyPositionPhase.Close), Position(1,20,StrategyPositionPhase.EndOfDay),
                Position(0,10,StrategyPositionPhase.MarkToMarket), Position(1,19,StrategyPositionPhase.MarkToMarket),
                Position(0,1,StrategyPositionPhase.Open)
            ]));
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services); services.EstablishedTrades.Returns(queries);
        ConfigureTradeLimits(services);
        services.StrategyPositions.Returns(new TomasAI.IFM.UI.Net.Services.Trade.StrategyPositionService(
            Substitute.For<IStrategyPositionCommandApi>(),positions));
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var feed = Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi>();
        feed.GetLastFuturesOptionTickDataAsync(Arg.Any<string>(),date.AddDays(1)).Returns(call =>
            new ServiceOk<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel>(new() {
                ContractId = call.ArgAt<string>(0),ValueDate = date.AddDays(1),TickTime = new TimeOnly(15,30),BidPrice = 2.5,Delta = 0.25
            }));
        services.FeedQueries.Returns(new TomasAI.IFM.UI.Net.Services.MarketDataFeed.MarketDataFeedQueryService(feed));
        var planApi = Substitute.For<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IStrategyTradePlanQueryApi>();
        planApi.GetCurrentIronCondorAsync(id,date.AddDays(1),Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot>(new() {
                Position = Position(1,19,StrategyPositionPhase.MarkToMarket),
                IronCondorTradePlanSnapshot = new() { ContractMultiplier = 50m, DailyPnl = 997.40m }
            }));
        services.StrategyTradePlanQueries.Returns(new TomasAI.IFM.UI.Net.Services.Trade.StrategyTradePlanQueryService(planApi));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
        var vm = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null,[],establishedTrade:StrategyPositionHistoryProjection.EnrichTrade(trade));
        vm.ApplyMarketSessionSnapshot(ServerSession());
        await vm.LoadEstablishedTradeAsync();
        vm.TradeHistory.Select(row => row.ValueDate).Should().Equal(date,date,date.AddDays(1),date.AddDays(2));
        vm.TradeHistory.Select(row => row.TradeStatus).Should().Equal(TradeStatus.Open,TradeStatus.IntraDay,TradeStatus.EndOfDay,TradeStatus.Close);
        vm.TradeHistory[2].TradePnl.Should().Be(20);
        await vm.LoadSavedPositionDataAsync(2);
        vm.TradeHistory[2].TradePnl.Should().Be(997.40m);
        vm.SelectedSavedPosition!.Phase.Should().Be(StrategyPositionPhase.EndOfDay);
        vm.SavedLegObservations.Should().HaveCount(4);
        vm.SavedLegObservations.Values.Should().OnlyContain(quote => quote.ValueDate == date.AddDays(1) && quote.BidPrice == 2.5);
        foreach (var leg in trade.Legs)
            await feed.Received(1).GetLastFuturesOptionTickDataAsync(leg.ContractId,date.AddDays(1));
        await vm.LoadEstablishedTradeAsync();
        vm.TradeHistory.Should().HaveCount(4);
        vm.TradeLimitSnapshot.Should().NotBeNull();
        vm.TradeLimitSnapshot!.TradeLimit.TradeId.Should().Be(trade.Id.TradeId);
        vm.TradeLimitSnapshot.TradeLimit.RiskMargin.Should().Be(1847.50m);
        await positions.Received(2).GetHistoryAsync(id,TradeStrategyKind.IronCondor,
            Arg.Any<DateTime>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>());
        var historicalRows = vm.TradeHistory;
        typeof(IronCondorViewModel).GetProperty(nameof(vm.IsLiveFeedEnabled))!.SetValue(vm, true);
        var liveDate = vm.ValueDate ?? ServerSession().ActiveValueDate!.Value;
        var current = Position(liveDate.DayNumber - date.DayNumber, 31, StrategyPositionPhase.MarkToMarket) with {
            Legs = trade.Legs.Select(leg => new StrategyPositionLeg { TradeLegId = leg.TradeLegId,
                ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity }).ToArray() };
        vm.ApplyIronCondorPosition(current).Should().BeTrue();
        vm.TradeHistory.Should().HaveCount(5);
        for (var i = 0; i < historicalRows.Length; i++) vm.TradeHistory[i].Should().BeSameAs(historicalRows[i]);
        var liveRows = vm.TradeHistory;
        vm.ApplyIronCondorPosition(current with { PositionSequence = 32 }).Should().BeTrue();
        vm.TradeHistory.Should().BeSameAs(liveRows, "unchanged row values do not invalidate history");
        vm.ApplyIronCondorTradePlan(new() { Position = current with { PositionSequence = 32 }, ValueDate = current.ValueDate,
            PlanRevision = 1, IronCondorTradePlanSnapshot = new() { ContractMultiplier = 50, DailyPnl = -250 } }).Should().BeTrue();
        vm.TradeHistory.Last().TradePnl.Should().Be(-250);
        for (var i = 0; i < historicalRows.Length; i++) vm.TradeHistory[i].Should().BeSameAs(historicalRows[i]);
        vm.ApplyIronCondorPosition(current with { ValueDate = liveDate.AddDays(-1), PositionSequence = 100 }).Should().BeFalse();
        vm.ApplyIronCondorPosition(current with { PositionSequence = 33, Phase = StrategyPositionPhase.EndOfDay }).Should().BeTrue();
        vm.TradeHistory.Last().TradeStatus.Should().Be(TradeStatus.EndOfDay);
        var sealedRows = vm.TradeHistory;
        vm.ApplyIronCondorPosition(current with { PositionSequence = 34 }).Should().BeTrue();
        vm.TradeHistory.Should().BeSameAs(sealedRows);
        for (var i = 0; i < historicalRows.Length; i++) vm.TradeHistory[i].Should().BeSameAs(historicalRows[i]);
        await positions.Received(2).GetHistoryAsync(id, TradeStrategyKind.IronCondor,
            Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EstablishedTradeStatus.Open, true)]
    [InlineData(EstablishedTradeStatus.Closed, false)]
    [InlineData(EstablishedTradeStatus.Closing, false)]
    public async Task Closed_trades_cannot_start_monitoring_and_active_monitoring_locks_history(EstablishedTradeStatus status, bool allowed)
    {
        var services = Substitute.For<IUiServiceCatalog>();
        ConfigureSession(services);
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services); root.AppEnvironment.Returns("Development");
        var trade = new EstablishedTradeDefinition { Id = new(101,701,1701,1101), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = new DateTime(2026,10,22,12,0,0,DateTimeKind.Utc), Status = status, Legs = Enumerable.Range(0,4).Select(i => new TradeLegDefinition {
                TradeLegId = Guid.NewGuid(), ContractId = $"option-{i}", Expiry = new DateOnly(2026,12,18), SignedQuantity = 1 }).ToArray() };
        var vm = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade:StrategyPositionHistoryProjection.EnrichTrade(trade), brokerEnvironment:BrokerEnvironment.Emulator);
        vm.ApplyMarketSessionSnapshot(ServerSession());
        vm.CanEnableLiveFeed.Should().Be(allowed);
        vm.CanSelectPositionHistory.Should().BeTrue();
        if (!allowed)
        {
            await FluentActions.Awaiting(() => vm.EnableLiveFeedAsync()).Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Open trade*");
            vm.IsLiveFeedEnabled.Should().BeFalse();
            return;
        }
        typeof(IronCondorViewModel).GetProperty(nameof(vm.IsLiveFeedEnabled))!.SetValue(vm,true);
        vm.CanSelectPositionHistory.Should().BeFalse();
        await vm.LoadSavedPositionDataAsync(0);
        vm.SelectedSavedPosition.Should().BeNull();
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor),
            StrategyKind = TradeStrategyKind.IronCondor, Phase = StrategyPositionPhase.MarkToMarket,
            ValueDate = ServerSession().ActiveValueDate!.Value, AsOfUtc = DateTime.UtcNow, PositionSequence = 1, DailyPnl = -25m,
            Legs = trade.Legs.Select(leg => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId }).ToArray() };
        position = StrategyPositionHistoryProjection.EnrichPosition(position, trade);
        vm.ApplyIronCondorPosition(position).Should().BeTrue();
        vm.ApplyIronCondorPosition(position with { PositionSequence = 2, DailyPnl = -35m }).Should().BeTrue();
        vm.IronCondorPosition!.DailyPnl.Should().Be(-35m);
        vm.TradeHistory.Last().TradePnl.Should().Be(-35m);
        // A raw position/plan update must not remove today's already valued history row.
        for (var sequence = 3; sequence <= 7; sequence += 2)
        {
            var raw = position with { PositionSequence = sequence, HistoryValuation = null, DailyPnl = -40m };
            vm.ApplyIronCondorPosition(raw).Should().BeTrue();
            vm.ApplyIronCondorTradePlan(new() { Position = raw, ValueDate = position.ValueDate,
                PlanRevision = sequence }).Should().BeTrue();
            vm.TradeHistory.Should().HaveCount(2);
            vm.TradeHistory.Last().TradePnl.Should().Be(-35m);
            vm.ApplyIronCondorPosition(raw with { PositionSequence = sequence + 1,
                HistoryValuation = position.HistoryValuation, DailyPnl = -35m }).Should().BeTrue();
            vm.TradeHistory.Should().HaveCount(2);
        }
        vm.CanSelectPositionHistory.Should().BeFalse();
        typeof(IronCondorViewModel).GetProperty(nameof(vm.IsLiveFeedEnabled))!.SetValue(vm,false);
        vm.CanSelectPositionHistory.Should().BeTrue();
    }

    [Fact]
    public void Trade_maturity_and_opening_history_use_the_latest_leg_expiry()
    {
        var trade = new EstablishedTradeDefinition { Id = new(101,701,1701,1101),
            EstablishedAtUtc = new DateTime(2026,10,9,14,0,0,DateTimeKind.Utc),
            Legs = [new TradeLegDefinition { Expiry = new DateOnly(2026,10,16) },
                new TradeLegDefinition { Expiry = new DateOnly(2026,11,20) }] };
        trade.MaturityDate.Should().Be(new DateOnly(2026,11,20));
        trade.TradeDate.Should().Be(new DateOnly(2026,10,9));
        (trade with { EstablishedAtUtc = default }).TradeDate.Should().BeNull();
        IronCondorViewModel.CreateOpeningPosition(StrategyPositionHistoryProjection.EnrichTrade(trade),TradeType.ShortIronCondor).DaysToExpiry.Should().Be(42);
        (trade with { Legs = [new TradeLegDefinition()] }).MaturityDate.Should().BeNull();
    }

    static void ConfigureTradeLimits(IUiServiceCatalog services)
    {
        var financial = Substitute.For<IPortfolioFinancialApi>();
        financial.GetAccountBalancesAsync(Arg.Is<FinancialReadScope>(scope => scope.PortfolioId == 101 && scope.FundId == 701),
            Arg.Any<GetAccountBalancesRequest>(), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<FinancialRead<FinancialBalanceSnapshot>>(new(FinancialReadStatus.Found,
                new(101,"Active",[],0,125000m,true),1,DateTime.UtcNow)));
        services.PortfolioFinancial.Returns(financial);
        var queries = Substitute.For<ITradeQueryApi>();
        queries.GetTradeLimitAsync(1101).Returns(new ServiceOk<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel>(new() {
            TradeId = 1101, TradeType = TradeType.ShortIronCondor, MaxProfit = 652.50m, RiskMargin = 1847.50m
        }));
        services.TradeQueries.Returns(new TomasAI.IFM.UI.Net.Services.Trade.TradeQueryService(queries));
    }

    static TomasAI.IFM.Domain.MarketData.Shared.ViewModels.MarketSessionReadModel ServerSession() => new() {
        OperationalValueDate = new(2026,10,22), ActiveValueDate = new(2026,10,22),
        State = TomasAI.IFM.Domain.MarketData.Shared.FuturesMarketState.LiveTrading, AllowDevelopmentEmulator = true,
        SessionStartUtc = new(2026,10,21,22,0,0,DateTimeKind.Utc), SessionEndUtc = new(2026,10,22,21,0,0,DateTimeKind.Utc),
        NextTransitionUtc = new(2026,10,22,21,0,0,DateTimeKind.Utc), AsOfUtc = new(2026,10,22,12,0,0,DateTimeKind.Utc), Revision = 1 };
    static void ConfigureSession(IUiServiceCatalog services) {
        var market = Substitute.For<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi>();
        market.GetMarketSessionAsync().Returns(new ServiceOk<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.MarketSessionReadModel>(ServerSession()));
        services.MarketDataQueries.Returns(new TomasAI.IFM.UI.Net.Services.MarketData.MarketDataQueryService(market,
            Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi>()));
    }

}
