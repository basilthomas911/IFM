using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData;

public sealed class FuturesOptionTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    const string ContractId = "ES20260918C5500";
    static readonly DateOnly ValueDate = new(2026, 8, 17);

    public sealed class TestableDurableActor(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        IStatusConsoleWriter status,
        ILogger<FuturesOptionTickDataEventActor> logger)
        : FuturesOptionTickDataEventActor(new FuturesOptionTickDataEventContext(
            supervisor, logger, marketDataApi, status))
    {
        public ValueTask Start(IEventActorContext<FuturesOptionTickDataEventActor> context) => OnStartup(context);
        public ValueTask Stop(IEventActorContext<FuturesOptionTickDataEventActor> context) => OnShutdown(context);
    }

    public sealed class TestableRealtimeActor(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        IStatusConsoleWriter status,
        ILogger<FuturesOptionTickDataRealtimeActor> logger)
        : FuturesOptionTickDataRealtimeActor(new FuturesOptionTickDataRealtimeContext(
            supervisor, logger, marketDataApi, status))
    {
        public IEvent Parse(IEventActorContext<FuturesOptionTickDataRealtimeActor> context, IActorMessage message) =>
            ParseMessage(context, message);
        public ValueTask Start(IEventActorContext<FuturesOptionTickDataRealtimeActor> context) => OnStartup(context);
        public ValueTask Stop(IEventActorContext<FuturesOptionTickDataRealtimeActor> context) => OnShutdown(context);
    }

    [Fact]
    public async Task Durable_stream_lifecycle_actor_has_no_live_tick_route()
    {
        var actor = new TestableDurableActor(
            CreateSupervisor(),
            Substitute.For<IMarketDataApi>(),
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesOptionTickDataEventActor>>());
        var context = Substitute.For<IEventActorContext<FuturesOptionTickDataEventActor>>();

        await actor.Start(context);
        await actor.Stop(context);

        actor.Id.ActorType.Should().Be(ActorType.Event);
        context.DidNotReceiveWithAnyArgs().AddEventRouter(default!, default!);
        context.DidNotReceiveWithAnyArgs().AddRealtimeRouter(default!, default!);
    }

    [Fact]
    public async Task Realtime_actor_registers_and_removes_only_the_realtime_tick_route()
    {
        var actor = CreateRealtimeActor();
        var context = Substitute.For<IEventActorContext<FuturesOptionTickDataRealtimeActor>>();
        var route = new ActorTypeId(
            ActorType.Realtime,
            FuturesTickTradeDataInsertedEvent.Actor,
            FuturesTickTradeDataInsertedEvent.Verb);

        await actor.Start(context);
        await actor.Stop(context);

        actor.Id.Should().Be(new ActorMailboxId(
            ActorType.Realtime,
            FuturesOptionTickDataRealtimeActor.ActorName));
        context.Received(1).AddRealtimeRouter(route, actor.Id);
        context.Received(1).RemoveRealtimeRouter(route, actor.Id);
        context.DidNotReceiveWithAnyArgs().AddEventRouter(default!, default!);
    }

    [Fact]
    public void Realtime_actor_parses_routed_trade_without_changing_payload_identity()
    {
        var source = CreateTrade();
        var actor = CreateRealtimeActor();
        NatsMsg<byte[]> message = new()
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesOptionTickDataRealtimeActor.ActorName,
                FuturesTickTradeDataInsertedEvent.Verb,
                source.EntityId.Format()).ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(source)
        };

        var parsed = actor.Parse(
                Substitute.For<IEventActorContext<FuturesOptionTickDataRealtimeActor>>(),
                new NatsActorMessage(message))
            .Should().BeOfType<FuturesTickTradeDataInsertedEvent>().Which;

        parsed.CommandId.Should().Be(source.CommandId);
        parsed.TickDataId.Should().Be(source.TickDataId);
        parsed.TradeData.Price.Should().Be(13.125m);
    }

    [Fact]
    public async Task Active_option_trade_combines_exact_trade_with_hot_quote_and_notifies_ui()
    {
        var source = CreateTrade();
        var eventApi = Substitute.For<IEventActorContext>();
        var marketDataApi = Substitute.For<IMarketDataApi>();
        marketDataApi.IsTickDataStreamActive(ContractId).Returns(true);
        marketDataApi.GetFuturesOptionContractAsync(ContractId)
            .Returns(SampleData.FuturesOptionContracts[0]);
        var price = CreateOptionPrice();
        marketDataApi.TryGetLastOptionTickPrice(
                ContractId,
                out Arg.Any<OptionTickerPriceSnapshot>())
            .Returns(call =>
            {
                call[1] = price;
                return true;
            });

        var result = await source.ExecuteAsync(
            eventApi,
            marketDataApi,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesOptionTickDataRealtimeActor>>());

        result.Should().BeTrue();
        await eventApi.Received(1).SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(
            Arg.Is<OptionTradeTickPriceDataUpdatedEvent>(sent =>
                sent.OptionTickData.ContractId == ContractId
                && sent.OptionTickData.ValueDate == ValueDate
                && sent.OptionTickData.OptionPrice == 13.125d
                && sent.OptionTickData.BidPrice == 12.25d
                && sent.OptionTickData.AskPrice == 12.75d
                && sent.OptionTickData.BidSize == 100
                && sent.OptionTickData.AskSize == 150));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Inactive_or_non_option_trade_is_ignored(
        bool active,
        bool optionAsset)
    {
        var source = CreateTrade(
            optionAsset ? AssetTypeId.FuturesOption : AssetTypeId.Futures);
        var eventApi = Substitute.For<IEventActorContext>();
        var marketDataApi = Substitute.For<IMarketDataApi>();
        marketDataApi.IsTickDataStreamActive(ContractId).Returns(active);

        var result = await source.ExecuteAsync(
            eventApi,
            marketDataApi,
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesOptionTickDataRealtimeActor>>());

        result.Should().BeTrue();
        await eventApi.DidNotReceiveWithAnyArgs()
            .SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(default!);
    }

    static TestableRealtimeActor CreateRealtimeActor() => new(
        CreateSupervisor(),
        Substitute.For<IMarketDataApi>(),
        Substitute.For<IStatusConsoleWriter>(),
        Substitute.For<ILogger<FuturesOptionTickDataRealtimeActor>>());

    static IActorSupervisor CreateSupervisor()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>())
            .Returns(Substitute.For<IActorMailbox>());
        return supervisor;
    }

    static OptionTickerPriceSnapshot CreateOptionPrice() => new(
        new TickerPriceSnapshot(
            ContractId,
            99,
            7,
            AssetTypeId.FuturesOption,
            ValueDate,
            new TickerQuoteSnapshot(
                12.25m,
                100,
                12.75m,
                150,
                1,
                1,
                100,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            new TickerTradeSnapshot(
                12.5m,
                25,
                101,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow)),
        null);

    static FuturesTickTradeDataInsertedEvent CreateTrade(
        AssetTypeId assetType = AssetTypeId.FuturesOption)
    {
        var entityId = new TickDataEntityId(ContractId, ValueDate, assetType);
        var timestamp = new DateTimeOffset(2026, 8, 17, 14, 30, 0, TimeSpan.Zero);
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
            TickDataId = new TickDataId(ContractId, ValueDate, 11, timestamp.UtcDateTime),
            AssetTypeId = assetType,
            Dataset = "GLBX.MDP3",
            DefinitionDate = ValueDate,
            PublisherId = 7,
            InstrumentId = 99,
            TradeData = new FuturesTickTradeData(
                100,
                timestamp.ToUnixTimeMilliseconds() * 1_000_000,
                timestamp.ToUnixTimeMilliseconds() * 1_000_000,
                0,
                13_125_000_000,
                13.125m,
                25,
                1,
                2,
                0)
        };
    }
}
