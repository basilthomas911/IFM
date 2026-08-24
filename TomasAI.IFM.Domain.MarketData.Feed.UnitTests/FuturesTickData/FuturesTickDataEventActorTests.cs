using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData;

public sealed class FuturesTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    const string ContractId = "VX20260916";
    static readonly DateOnly ValueDate = new(2026, 8, 14);

    public sealed class TestableDurableActor(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboard,
        IStatusConsoleWriter status,
        ILogger<FuturesTickDataEventActor> logger)
        : FuturesTickDataEventActor(new FuturesTickDataEventContext(
            supervisor, logger, marketDataApi, blackboard, status))
    {
        public ValueTask Start(IEventActorContext<FuturesTickDataEventActor> context) => OnStartup(context);
        public ValueTask Stop(IEventActorContext<FuturesTickDataEventActor> context) => OnShutdown(context);
    }

    public sealed class TestableRealtimeActor : FuturesEodDataRealtimeActor
    {
        public IFuturesEodDataRealtimeContext Context { get; }

        public TestableRealtimeActor(
            IActorSupervisor supervisor,
            IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
            IMarketDataApi marketDataApi,
            IBlackboardService blackboard,
            IStatusConsoleWriter status,
            ILogger<FuturesEodDataRealtimeActor> logger)
            : this(TypedActorContextFactory.Realtime(
                supervisor, projector, marketDataApi, blackboard, status, logger)) { }

        TestableRealtimeActor(IFuturesEodDataRealtimeContext context)
            : base(context) => Context = context;

        public IEvent Parse(IEventActorContext<FuturesEodDataRealtimeActor> context, IActorMessage message) =>
            ParseMessage(context, message);
        public ValueTask Receive(IEventActorContext<FuturesEodDataRealtimeActor> context, IEvent domainEvent) =>
            ReceiveAsync(context, domainEvent);
        public ValueTask Start(IEventActorContext<FuturesEodDataRealtimeActor> context) => OnStartup(context);
        public ValueTask Stop(IEventActorContext<FuturesEodDataRealtimeActor> context) => OnShutdown(context);
    }

    [Fact]
    public async Task Durable_stream_lifecycle_actor_has_no_live_tick_route()
    {
        var actor = new TestableDurableActor(
            CreateSupervisor(),
            Substitute.For<IMarketDataApi>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesTickDataEventActor>>());
        var context = Substitute.For<IEventActorContext<FuturesTickDataEventActor>>();

        await actor.Start(context);
        await actor.Stop(context);

        actor.Id.ActorType.Should().Be(ActorType.Event);
        context.DidNotReceiveWithAnyArgs().AddEventRouter(default!, default!);
        context.DidNotReceiveWithAnyArgs().AddRealtimeRouter(default!, default!);
    }

    [Fact]
    public async Task Realtime_actor_registers_all_live_eod_routes_and_projector_lifecycle()
    {
        var projector = CreateProjector();
        var actor = CreateRealtimeActor(projector, out _);
        var context = Substitute.For<IEventActorContext<FuturesEodDataRealtimeActor>>();
        var route = new ActorTypeId(
            ActorType.Realtime,
            FuturesTickTradeDataInsertedEvent.Actor,
            FuturesTickTradeDataInsertedEvent.Verb);
        var marketPriceRoute = new ActorTypeId(
            ActorType.Realtime,
            FuturesMarketPriceUpdatedRealtimeEvent.Actor,
            FuturesMarketPriceUpdatedRealtimeEvent.Verb);
        var statisticsRoute = new ActorTypeId(
            ActorType.Realtime,
            FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
            FuturesSessionStatisticsUpdatedRealtimeEvent.Verb);

        await actor.Start(context);
        await actor.Stop(context);

        actor.Id.Should().Be(new ActorMailboxId(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName));
        context.Received(1).AddRealtimeRouter(route, actor.Id);
        context.Received(1).AddRealtimeRouter(marketPriceRoute, actor.Id);
        context.Received(1).AddRealtimeRouter(statisticsRoute, actor.Id);
        context.Received(1).RemoveRealtimeRouter(route, actor.Id);
        context.Received(1).RemoveRealtimeRouter(marketPriceRoute, actor.Id);
        context.Received(1).RemoveRealtimeRouter(statisticsRoute, actor.Id);
        await projector.Received(1).StartAsync(context, Arg.Any<CancellationToken>());
        await projector.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Realtime_actor_parses_routed_trade_without_changing_payload_identity()
    {
        var source = CreateTrade();
        var actor = CreateRealtimeActor(CreateProjector(), out _);
        NatsMsg<byte[]> message = new()
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesEodDataRealtimeActor.ActorName,
                FuturesTickTradeDataInsertedEvent.Verb,
                source.EntityId.Format()).ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(source)
        };

        var parsed = actor.Parse(Substitute.For<IEventActorContext<FuturesEodDataRealtimeActor>>(), new NatsActorMessage(message))
            .Should().BeOfType<FuturesTickTradeDataInsertedEvent>().Which;

        parsed.CommandId.Should().Be(source.CommandId);
        parsed.TickDataId.Should().Be(source.TickDataId);
        parsed.TradeData.Price.Should().Be(20.15m);
    }

    [Fact]
    public async Task Active_vix_trade_is_projected_without_a_command_actor()
    {
        var projector = CreateProjector();
        var actor = CreateRealtimeActor(projector, out var marketDataApi);
        marketDataApi.IsTickDataStreamActive(ContractId).Returns(true);
        marketDataApi.GetFuturesContractAsync(ContractId).Returns(new FuturesContractV2ReadModel(
            ContractId,
            "VIX Futures",
            "VX",
            "VXU6",
            "FUT",
            "USD",
            "CFE",
            "1000",
            new DateOnly(2026, 9, 16),
            true));

        await actor.Receive(Substitute.For<IEventActorContext<FuturesEodDataRealtimeActor>>(), CreateTrade());

        await projector.Received(1).ProcessRealtimeEventAsync(
            Arg.Is<VixFuturesEodDataInsertedEvent>(inserted =>
                inserted.Subject.ActorType == ActorType.Realtime
                && inserted.VixFuturesTickData.Price == 20.15m
                && inserted.VixFuturesTickData.Size == 17),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Realtime_actor_parses_session_statistics_and_projects_recalculated_eod()
    {
        const string contractId = "ES20260918";
        var entityId = new FuturesEodDataId(contractId, ValueDate);
        var source = new FuturesSessionStatisticsUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = DateTime.UtcNow,
            Statistics = new FuturesSessionStatisticsSnapshot(
                contractId,
                ValueDate,
                5400m,
                5500m,
                5350m,
                10,
                20,
                12_345,
                FuturesSessionVolumeQuality.ObservedComplete)
        };
        NatsMsg<byte[]> message = new()
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesEodDataRealtimeActor.ActorName,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Verb,
                entityId.Format()).ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(source)
        };
        var projector = CreateProjector();
        var actor = CreateRealtimeActor(projector, out _);
        IEventActorContext<FuturesEodDataRealtimeActor> context = actor.Context;
        var current = new FuturesEodDataV2ReadModel(
            contractId, ValueDate, "ES", 5390m, 5460m, 5370m, 5425m, 1000,
            0.1, 0.01, 54.25, 5500, 5425, 5350,
            MarketDirectionType.NeutralUp, MarketVolatilityType.Normal,
            PriceDirectionType.Falling, PriceVolatilityType.Falling);
        context.RequestAsync<FuturesEodDataV2ReadModel, GetFuturesEodDataQuery>(
                Arg.Any<GetFuturesEodDataQuery>())
            .Returns(new ServiceOk<FuturesEodDataV2ReadModel>(current));

        var parsed = actor.Parse(context, new NatsActorMessage(message))
            .Should().BeOfType<FuturesSessionStatisticsUpdatedRealtimeEvent>().Which;
        await actor.Receive(context, parsed);

        parsed.Statistics.Should().Be(source.Statistics);
        await projector.Received(1).ProcessRealtimeEventAsync(
            Arg.Is<FuturesEodSessionStatisticsUpdatedEvent>(projected =>
                projected.CommandId == source.CommandId
                && projected.FuturesEodData.OpenPrice == 5400m
                && projected.FuturesEodData.HighPrice == 5500m
                && projected.FuturesEodData.LowPrice == 5350m
                && projected.FuturesEodData.Volume == 12_345
                && projected.FuturesEodData.DailyPercentChange == 0.0046d
                && projected.FuturesEodData.PriceDirection == PriceDirectionType.Rising),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Realtime_actor_routes_volume_only_session_snapshot_to_vx_storage_event()
    {
        var entityId = new FuturesEodDataId(ContractId, ValueDate);
        var source = new FuturesSessionStatisticsUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
                FuturesSessionStatisticsUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = DateTime.UtcNow,
            Statistics = new FuturesSessionStatisticsSnapshot(
                ContractId,
                ValueDate,
                0,
                0,
                0,
                99,
                100,
                50_000,
                FuturesSessionVolumeQuality.OfficialFinal)
        };
        var projector = CreateProjector();
        var actor = CreateRealtimeActor(projector, out _);
        IEventActorContext<FuturesEodDataRealtimeActor> context = actor.Context;
        context.RequestAsync<FuturesEodDataV2ReadModel, GetFuturesEodDataQuery>(
                Arg.Any<GetFuturesEodDataQuery>())
            .Returns(new ServiceOk<FuturesEodDataV2ReadModel>(null!));
        context.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(
                Arg.Any<GetVixFuturesEodDataQuery>())
            .Returns(new ServiceOk<VixFuturesEodDataReadModel[]>([
                new VixFuturesEodDataReadModel(
                    ContractId, ValueDate, 20m, 21m, 19m, 20.25m, 100)
            ]));

        await actor.Receive(context, source);

        await projector.Received(1).ProcessRealtimeEventAsync(
            Arg.Is<VixFuturesEodDataInsertedEvent>(projected =>
                projected.VixFuturesTickData.ContractId == ContractId
                && projected.VixFuturesTickData.Price == 20.25m
                && projected.VixFuturesTickData.Size == 0
                && projected.SessionStatistics == source.Statistics),
            Arg.Any<CancellationToken>());
    }

    static TestableRealtimeActor CreateRealtimeActor(
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        out IMarketDataApi marketDataApi)
    {
        marketDataApi = Substitute.For<IMarketDataApi>();
        return new TestableRealtimeActor(
            CreateSupervisor(),
            projector,
            marketDataApi,
            Substitute.For<IBlackboardService>(),
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesEodDataRealtimeActor>>());
    }

    static IRealtimeProjector<FuturesEodDataRealtimeActor> CreateProjector()
    {
        var projector = Substitute.For<IRealtimeProjector<FuturesEodDataRealtimeActor>>();
        projector.ProcessRealtimeEventAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        return projector;
    }

    static IActorSupervisor CreateSupervisor()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>())
            .Returns(Substitute.For<IActorMailbox>());
        return supervisor;
    }

    static FuturesTickTradeDataInsertedEvent CreateTrade()
    {
        var entityId = new TickDataEntityId(ContractId, ValueDate, AssetTypeId.Futures);
        var timestamp = new DateTimeOffset(2026, 8, 14, 14, 30, 0, TimeSpan.Zero);
        return new FuturesTickTradeDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesTickTradeDataInsertedEvent.Actor,
                FuturesTickTradeDataInsertedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = timestamp.UtcDateTime,
            TickDataId = new TickDataId(ContractId, ValueDate, 2, timestamp.UtcDateTime),
            AssetTypeId = AssetTypeId.Futures,
            Dataset = "GLBX.MDP3",
            DefinitionDate = ValueDate,
            PublisherId = 7,
            InstrumentId = 42,
            TradeData = new FuturesTickTradeData(
                2,
                timestamp.ToUnixTimeMilliseconds() * 1_000_000,
                timestamp.ToUnixTimeMilliseconds() * 1_000_000,
                0,
                20_150_000_000,
                20.15m,
                17,
                1,
                2,
                0)
        };
    }
}
