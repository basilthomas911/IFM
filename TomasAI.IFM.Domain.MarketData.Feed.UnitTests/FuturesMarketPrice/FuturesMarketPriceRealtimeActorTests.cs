using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesMarketPrice;

public sealed class FuturesMarketPriceRealtimeActorTests
{
    public sealed class TestableFuturesMarketPriceRealtimeActor(
        IActorSupervisor supervisor,
        ILogger<FuturesMarketPriceRealtimeActor> logger)
        : FuturesMarketPriceRealtimeActor(new FuturesMarketPriceRealtimeContext(supervisor, logger))
    {
        public IEvent Parse(IEventActorContext<FuturesMarketPriceRealtimeActor> context, IActorMessage message) =>
            ParseMessage(context, message);

        public ValueTask Receive(IEventActorContext<FuturesMarketPriceRealtimeActor> context, IEvent @event) =>
            ReceiveAsync(context, @event);
    }

    [Fact]
    public void Actor_UsesRequiredRealtimePrimaryMailboxIdentity()
    {
        var actor = CreateActor();

        actor.Id.Should().Be(new ActorMailboxId(
            ActorType.Realtime,
            FuturesMarketPriceUpdatedRealtimeEvent.Actor));
        FuturesMarketPriceRealtimeActor.ActorName
            .Should().Be(FuturesMarketPriceUpdatedRealtimeEvent.Actor);
    }

    [Fact]
    public void Contract_RoundTripsProviderNeutralDecimalSnapshot()
    {
        var @event = CreateEvent();

        var roundTrip = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(
            MessagePackSerializer.Serialize(@event));

        roundTrip.Should().BeEquivalentTo(@event);
        roundTrip.Price.Trade!.Value.LastPrice.Should().Be(5450.25m);
        roundTrip.Price.Quote!.Value.BidPrice.Should().Be(5450.00m);
        roundTrip.Price.Quote!.Value.AskPrice.Should().Be(5450.50m);
    }

    [Fact]
    public void ParseMessage_SupportedRealtimeEvent_ReturnsConcreteEvent()
    {
        var @event = CreateEvent();
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(@event.Subject);
        message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>().Returns(@event);

        var parsed = CreateActor().Parse(
            Substitute.For<IEventActorContext<FuturesMarketPriceRealtimeActor>>(),
            message);

        parsed.Should().BeEquivalentTo(@event);
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesMarketPriceUpdatedRealtimeEvent.Actor, FuturesMarketPriceUpdatedRealtimeEvent.Verb)]
    [InlineData(ActorType.Realtime, "WrongRealtimeActor", FuturesMarketPriceUpdatedRealtimeEvent.Verb)]
    [InlineData(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor, "Completed")]
    [InlineData(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor, "Failed")]
    public void ParseMessage_UnsupportedSubject_ReturnsNull(
        ActorType actorType,
        string actorName,
        string verb)
    {
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(new ActorSubject(actorType, actorName, verb, "entity"));

        var parsed = CreateActor().Parse(
            Substitute.For<IEventActorContext<FuturesMarketPriceRealtimeActor>>(),
            message);

        parsed.Should().BeNull();
    }

    [Fact]
    public async Task Receive_AcceptsLegacyPriceUpdateWithoutSourceTrade()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext<FuturesMarketPriceRealtimeActor>>();

        var action = () => actor.Receive(context, CreateEvent()).AsTask();

        await action.Should().NotThrowAsync();
        context.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Handler_ValidatesArgumentsAndReturnsSuccess()
    {
        var @event = CreateEvent();
        var context = Substitute.For<IEventActorContext<FuturesMarketPriceRealtimeActor>>();
        var logger = Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>();

        var result = await @event.ExecuteAsync(context, logger);

        result.Should().BeTrue();
        await ((Func<Task>)(() => FuturesMarketPriceUpdated.ExecuteAsync(
                null!, context, logger).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => @event.ExecuteAsync(null!, logger).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ActorAssembly_ExposesRealtimeActorForReflectionRegistration()
    {
        var actorType = typeof(FuturesMarketPriceRealtimeActor);

        MarketDataFeedActorAssembly.Current.GetTypes().Should().Contain(actorType);
        actorType.GetInterfaces().Should().Contain(contract =>
            contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IActor<>));
    }

    [Theory]
    [InlineData(FuturesMarketPriceUpdateSource.Trade, "FuturesMarketPrice", true, false)]
    [InlineData(FuturesMarketPriceUpdateSource.Quote, "FuturesMarketPrice", false, false)]
    [InlineData(FuturesMarketPriceUpdateSource.Trade, "FuturesVwapSignal", false, true)]
    [InlineData(FuturesMarketPriceUpdateSource.Trade, "FuturesMarketPrice", false, true)]
    public async Task Handler_forwards_only_qualifying_trade_after_serialization(
        FuturesMarketPriceUpdateSource source, string actorName, bool shouldForward, bool checkpoint)
    {
        var price = CreateEvent();
        var trade = new FuturesTickTradeDataChangedEvent
        {
            Subject = new ActorSubject(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor,
                FuturesTickTradeDataChangedEvent.Verb, price.EntityId.Format()),
            Id = price.Id,
            CommandId = price.CommandId,
            EntityId = price.EntityId,
            AggregateId = price.AggregateId,
            TickDataId = new TickDataId(price.EntityId.ContractId, price.EntityId.ValueDate, 1, price.ReceivedOn),
            Dataset = "GLBX.MDP3",
            DefinitionDate = price.EntityId.ValueDate,
            PublisherId = price.Price.PublisherId,
            InstrumentId = price.Price.InstrumentId,
            AssetTypeId = AssetTypeId.Futures,
            TradeData = new FuturesTickTradeData(42, 100, 101, 0, 5_450_250_000_000, 5450.25m, 3,
                (byte)'T', (byte)'B', 0)
        };
        price = price with { SourceTrade = trade, UpdateSource = source, VwapCheckpoint = checkpoint ? new FuturesVwapSourceCheckpoint() : null,
            Subject = price.Subject with { Name = actorName } };
        var restored = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(
            MessagePackSerializer.Serialize(price));
        restored.SourceTrade.Should().BeEquivalentTo(trade);
        var generation = Guid.NewGuid();
        restored = restored with { SourceDataset = "GLBX.MDP3", SourceGenerationId = generation };
        var context = Substitute.For<IEventActorContext>();
        await restored.ExecuteAsync(context, Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>());
        if (shouldForward)
        {
            await context.Received(1).SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(
                Arg.Is<FuturesTickTradeDataChangedEvent>(e => e.Id == trade.Id
                    && e.TickDataId == trade.TickDataId && e.TradeData == trade.TradeData
                    && e.SourceDataset == "GLBX.MDP3" && e.SourceGenerationId == generation));
            var forwarded = context.ReceivedCalls().Single().GetArguments()
                .OfType<FuturesTickTradeDataChangedEvent>().Single();
            var tickContext = Substitute.For<ITickAggregationRealtimeContext>();
            await TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.FuturesTickTradeDataChanged
                .ExecuteAsync(forwarded, tickContext);
            await tickContext.Projector.Received(1).ProcessRealtimeEventAsync(
                Arg.Is<IEvent>(e => e is FuturesTickTradeDataInsertedEvent
                    && ((FuturesTickTradeDataInsertedEvent)e).TradeData == trade.TradeData
                    && e.Id == trade.Id), Arg.Any<CancellationToken>());
        }
        else
            context.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void Legacy_price_payload_without_source_trade_still_deserializes()
    {
        var bytes = MessagePackSerializer.Serialize(CreateEvent());
        // Remove the appended, null key 12 from the previous twelve-field array contract.
        bytes[0] = 0x9c;
        var restored = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(bytes[..^1]);
        restored.SourceTrade.Should().BeNull();
        restored.Price.Trade!.Value.LastPrice.Should().Be(5450.25m);
    }
    static TestableFuturesMarketPriceRealtimeActor CreateActor()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.CreateMailbox(Arg.Any<ActorMailboxId>())
            .Returns(Substitute.For<IActorMailbox>());
        return new TestableFuturesMarketPriceRealtimeActor(
            supervisor,
            Substitute.For<ILogger<FuturesMarketPriceRealtimeActor>>());
    }

    static FuturesMarketPriceUpdatedRealtimeEvent CreateEvent()
    {
        var valueDate = new DateOnly(2026, 8, 14);
        var entityId = new TickDataEntityId("ESZ26", valueDate, AssetTypeId.Futures);
        var eventTimestamp = new DateTimeOffset(2026, 8, 14, 14, 30, 0, TimeSpan.Zero);
        var receiveTimestamp = eventTimestamp.AddMilliseconds(2);
        return new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 17,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = receiveTimestamp.UtcDateTime,
            Price = new FuturesMarketPriceSnapshot(
                entityId.ContractId,
                42,
                7,
                entityId.AssetTypeId,
                entityId.ValueDate,
                new FuturesMarketQuoteSnapshot(
                    5450.00m,
                    11,
                    5450.50m,
                    13,
                    2,
                    3,
                    100,
                    eventTimestamp,
                    receiveTimestamp),
                new FuturesMarketTradeSnapshot(
                    5450.25m,
                    5,
                    101,
                    eventTimestamp,
                    receiveTimestamp))
        };
    }
}
