using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData;

public sealed class FuturesOptionTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    private readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionTickDataEventActorTests(MarketDataFeedTestFixture fixture) =>
        _fixture = fixture;

    public sealed class TestableFuturesOptionTickDataEventActor : FuturesOptionTickDataEventActor
    {
        public TestableFuturesOptionTickDataEventActor(
            IActorSupervisor supervisor,
            ApplicationMarketDataApi marketDataApi,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesOptionTickDataEventActor> logger)
            : base(
                supervisor,
                new global::TomasAI.IFM.Domain.MarketData.Feed.Event.Api.ActorMarketDataFeedEventApiFactory(),
                marketDataApi,
                statusConsoleWriter,
                logger)
        {
        }

        public IEvent Parse(IEventActorContext context, NatsMsg<byte[]> message) =>
            ParseMessage(context, message);
        public ValueTask Receive(IEventActorContext context, IEvent @event) =>
            ReceiveAsync(context, @event);
        public ValueTask Startup(IEventActorContext context) => OnStartup(context);
        public ValueTask Shutdown(IEventActorContext context) => OnShutdown(context);
        public ValueTask Exception(
            IEventActorContext context,
            ActorThreadId threadId,
            IEvent @event,
            Exception exception) =>
            OnExceptionAsync(context, threadId, @event, exception);
    }

    [Theory]
    [MemberData(nameof(SupportedEvents))]
    public void ParseMessage_SupportedEvent_ReturnsConcreteEvent(IEvent @event)
    {
        var parsed = CreateActor().Parse(
            Substitute.For<IEventActorContext>(),
            CreateRoutedMessage(@event));

        parsed.Should().BeOfType(@event.GetType());
        parsed.CommandId.Should().Be(@event.CommandId);
    }

    [Fact]
    public void ParseMessage_TradeEventPreservesDurableIdentityAndDecimalPrice()
    {
        var @event = CreateTradeEvent();
        var parsed = CreateActor().Parse(
            Substitute.For<IEventActorContext>(),
            CreateRoutedMessage(@event));

        var trade = parsed.Should().BeOfType<FuturesTickTradeDataInsertedEvent>().Which;
        trade.AssetTypeId.Should().Be(AssetTypeId.FuturesOption);
        trade.InstrumentId.Should().Be(99);
        trade.TradeData.Price.Should().Be(12.5m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidTradePayloadThrows(bool empty)
    {
        var @event = CreateTradeEvent();
        var message = CreateRoutedMessage(@event) with
        {
            Data = empty ? [] : [0x00, 0x01, 0xFF]
        };
        var action = () => CreateActor().Parse(
            Substitute.For<IEventActorContext>(),
            message);

        action.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_EmptyCommandIdThrows()
    {
        var @event = CreateTradeEvent() with { CommandId = Guid.Empty };
        var action = () => CreateActor().Parse(
            Substitute.For<IEventActorContext>(),
            CreateRoutedMessage(@event));

        action.Should().Throw<Exception>();
    }

    [Fact]
    public async Task StartupAndShutdown_RegisterAndRemoveOnlyDurableTradeRoute()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var route = new ActorTypeId(
            ActorType.Event,
            FuturesTickTradeDataInsertedEvent.Actor,
            FuturesTickTradeDataInsertedEvent.Verb);

        await actor.Startup(context);
        await actor.Shutdown(context);

        context.Received(1).AddEventRouter(route, actor.Id);
        context.Received(1).RemoveEventRouter(route, actor.Id);
    }

    [Fact]
    public async Task StreamingStart_ValidatesOptionAndRegistersDeterministicOwner()
    {
        var option = SampleData.FuturesOptionContracts[0];
        var api = Substitute.For<ApplicationMarketDataApi>();
        api.GetFuturesOptionContractAsync(option.ContractId).Returns(option);
        api.StartStreamingFuturesOptionTickDataAsync(
                option.ContractId,
                Arg.Any<TickerStreamOwner?>())
            .Returns(true);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent(option);

        await actor.Receive(context, @event);

        await api.Received(1).StartStreamingFuturesOptionTickDataAsync(
            option.ContractId,
            Arg.Is<TickerStreamOwner?>(owner =>
                owner.HasValue
                && owner.Value.WorkflowType == nameof(FuturesOptionTickDataEventActor)
                && owner.Value.WorkflowId == @event.EntityId.Format()
                && owner.Value.LegId == option.ContractId));
        await context.Received(1).SendAsync<
            FuturesOptionTickDataStreamingStartedCompleteEvent,
            FuturesOptionTickEntityId>(Arg.Any<FuturesOptionTickDataStreamingStartedCompleteEvent>());
    }

    [Fact]
    public async Task StreamingStart_UnknownOptionPublishesFailureWithoutRegistration()
    {
        var option = SampleData.FuturesOptionContracts[0];
        var api = Substitute.For<ApplicationMarketDataApi>();
        api.GetFuturesOptionContractAsync(option.ContractId)
            .Returns((FuturesOptionContractReadModel?)null);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();

        var action = () => actor.Receive(
            context,
            CreateStreamingStartedEvent(option)).AsTask();

        await action.Should().NotThrowAsync();
        await api.DidNotReceive().StartStreamingFuturesOptionTickDataAsync(
            Arg.Any<string>(),
            Arg.Any<TickerStreamOwner?>());
        await context.Received(1).SendAsync<
            FuturesOptionTickDataStreamingStartedFailEvent,
            FuturesOptionTickEntityId>(Arg.Any<FuturesOptionTickDataStreamingStartedFailEvent>());
    }

    [Fact]
    public async Task StreamingStop_ReleasesOwnedRegistrationAndPublishesCompletion()
    {
        var option = SampleData.FuturesOptionContracts[0];
        var api = Substitute.For<ApplicationMarketDataApi>();
        api.GetFuturesOptionContractAsync(option.ContractId).Returns(option);
        api.StartStreamingFuturesOptionTickDataAsync(
                option.ContractId,
                Arg.Any<TickerStreamOwner?>())
            .Returns(true);
        api.StopStreamingFuturesOptionTickDataAsync(
                option.ContractId,
                Arg.Any<TickerStreamOwner?>())
            .Returns(true);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        await actor.Receive(context, CreateStreamingStartedEvent(option));

        await actor.Receive(context, CreateStreamingStoppedEvent(option.ContractId));

        await api.Received(1).StopStreamingFuturesOptionTickDataAsync(
            option.ContractId,
            Arg.Any<TickerStreamOwner?>());
        await context.Received(1).SendAsync<
            FuturesOptionTickDataStreamingStoppedCompleteEvent,
            FuturesOptionTickEntityId>(Arg.Any<FuturesOptionTickDataStreamingStoppedCompleteEvent>());
    }

    [Fact]
    public async Task DurableOptionTrade_CombinesExactTradeWithLatestQuoteAndPublishesDomainUpdate()
    {
        var option = SampleData.FuturesOptionContracts[0];
        var api = Substitute.For<ApplicationMarketDataApi>();
        api.GetFuturesOptionContractAsync(option.ContractId).Returns(option);
        var price = CreateOptionPrice(option.ContractId);
        ConfigureActiveOption(api, option.ContractId, price);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        await actor.Receive(context, CreateStreamingStartedEvent(option));
        var trade = CreateTradeEvent(option.ContractId, 13.125m, 25);

        await actor.Receive(context, trade);

        await context.Received(1).SendAsync<
            OptionTradeTickPriceDataUpdatedEvent,
            FuturesOptionTickEntityId>(Arg.Is<OptionTradeTickPriceDataUpdatedEvent>(updated =>
                updated.CommandId == trade.CommandId
                && updated.OptionTickData.ContractId == option.ContractId
                && updated.OptionTickData.OptionPrice == 13.125d
                && updated.OptionTickData.BidPrice == 12.25d
                && updated.OptionTickData.AskPrice == 12.75d
                && updated.OptionTickData.BidSize == 100
                && updated.OptionTickData.AskSize == 150
                && updated.OptionTickData.ImpliedVolatility == 0d));
    }

    [Fact]
    public async Task DurableTradeWithoutActiveStream_IsAcknowledgedWithoutUpdate()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();

        await actor.Receive(context, CreateTradeEvent());

        await context.DidNotReceive().SendAsync<
            OptionTradeTickPriceDataUpdatedEvent,
            FuturesOptionTickEntityId>(Arg.Any<OptionTradeTickPriceDataUpdatedEvent>());
    }

    [Fact]
    public async Task DurableTradeAfterStreamStops_IsAcknowledgedWithoutUpdate()
    {
        var option = SampleData.FuturesOptionContracts[0];
        var api = Substitute.For<ApplicationMarketDataApi>();
        api.GetFuturesOptionContractAsync(option.ContractId).Returns(option);
        api.StartStreamingFuturesOptionTickDataAsync(
                option.ContractId,
                Arg.Any<TickerStreamOwner?>())
            .Returns(true);
        api.IsTickDataStreamActive(option.ContractId).Returns(false);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        await actor.Receive(context, CreateStreamingStartedEvent(option));

        var action = () => actor.Receive(context, CreateTradeEvent(option.ContractId)).AsTask();

        await action.Should().NotThrowAsync();
        await context.DidNotReceive().SendAsync<
            OptionTradeTickPriceDataUpdatedEvent,
            FuturesOptionTickEntityId>(Arg.Any<OptionTradeTickPriceDataUpdatedEvent>());
    }

    [Fact]
    public async Task FuturesTrade_IsIgnoredByOptionActor()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();

        await actor.Receive(context, CreateTradeEvent(assetType: AssetTypeId.Futures));

        context.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task DomainUpdatePublishFailure_PropagatesForDurableErrorHandling()
    {
        var option = SampleData.FuturesOptionContracts[0];
        var api = Substitute.For<ApplicationMarketDataApi>();
        api.GetFuturesOptionContractAsync(option.ContractId).Returns(option);
        ConfigureActiveOption(api, option.ContractId, CreateOptionPrice(option.ContractId));
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        context.SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(
                Arg.Any<OptionTradeTickPriceDataUpdatedEvent>())
            .Returns(_ => ValueTask.FromException(new InvalidOperationException("publish failed")));
        await actor.Receive(context, CreateStreamingStartedEvent(option));

        var action = () => actor.Receive(context, CreateTradeEvent(option.ContractId)).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("publish failed");
    }

    [Fact]
    public async Task Receive_NullAndUnsupportedEventsThrow()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        await ((Func<Task>)(() => actor.Receive(null!, CreateTradeEvent()).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.Receive(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();

        var unsupported = Substitute.For<IEvent>();
        unsupported.Subject.Returns(new ActorSubject(
            ActorType.Event,
            FuturesOptionTickDataEventActor.Actor,
            "Unknown",
            "entity"));
        await ((Func<Task>)(() => actor.Receive(context, unsupported).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnException_PublishesFrameworkErrorEvent()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateTradeEvent();

        await actor.Exception(
            context,
            @event.Subject.ThreadId,
            @event,
            new InvalidOperationException("event failed"));

        await context.Received(1).SendAsync<
            global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(Arg.Is<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent>(
                failed => failed.ErrorMessage == "event failed"));
    }

    private TestableFuturesOptionTickDataEventActor CreateActor(ApplicationMarketDataApi? marketDataApi = null) =>
        _fixture.CreateActor(
            Substitute.For<IActorSupervisor>(),
            marketDataApi ?? Substitute.For<ApplicationMarketDataApi>(),
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesOptionTickDataEventActor>>());

    private static void ConfigureActiveOption(
        ApplicationMarketDataApi api,
        string contractId,
        OptionTickerPriceSnapshot optionPrice)
    {
        api.StartStreamingFuturesOptionTickDataAsync(
                contractId,
                Arg.Any<TickerStreamOwner?>())
            .Returns(true);
        api.IsTickDataStreamActive(contractId).Returns(true);
        api.TryGetLastOptionTickPrice(contractId, out Arg.Any<OptionTickerPriceSnapshot>())
            .Returns(call =>
            {
                call[1] = optionPrice;
                return true;
            });
    }

    private static OptionTickerPriceSnapshot CreateOptionPrice(string contractId) => new(
        new TickerPriceSnapshot(
            contractId,
            99,
            7,
            AssetTypeId.FuturesOption,
            SampleData.ValueDate,
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

    public static IEnumerable<object[]> SupportedEvents()
    {
        yield return [CreateTradeEvent()];
        yield return [CreateStreamingStartedEvent(SampleData.FuturesOptionContracts[0])];
        yield return [CreateStreamingStoppedEvent(SampleData.FuturesOptionContracts[0].ContractId)];
    }

    private static NatsMsg<byte[]> CreateRoutedMessage(IEvent @event) => new()
    {
        Subject = new ActorSubject(
            ActorType.Event,
            FuturesOptionTickDataEventActor.Actor,
            @event.Subject.Verb,
            @event.Subject.EntityId).ToString(),
        Data = @event switch
        {
            FuturesTickTradeDataInsertedEvent value =>
                ActorExtensions.DataSerializer!.Serialize(value),
            FuturesOptionTickDataStreamingStartedEvent value =>
                ActorExtensions.DataSerializer!.Serialize(value),
            FuturesOptionTickDataStreamingStoppedEvent value =>
                ActorExtensions.DataSerializer!.Serialize(value),
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        }
    };

    private static FuturesTickTradeDataInsertedEvent CreateTradeEvent(
        string? contractId = null,
        decimal price = 12.5m,
        uint size = 25,
        AssetTypeId assetType = AssetTypeId.FuturesOption)
    {
        contractId ??= SampleData.FuturesOptionContracts[0].ContractId;
        var entityId = new TickDataEntityId(contractId, SampleData.ValueDate, assetType);
        return new FuturesTickTradeDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesTickTradeDataInsertedEvent.Actor,
                FuturesTickTradeDataInsertedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            AggregateId = entityId.Format(),
            EventSource = "test",
            ReceivedOn = DateTime.UtcNow,
            TickDataId = new TickDataId(contractId, SampleData.ValueDate, 11, DateTime.UtcNow),
            AssetTypeId = assetType,
            Dataset = "GLBX.MDP3",
            DefinitionDate = SampleData.ValueDate,
            PublisherId = 7,
            InstrumentId = 99,
            TradeData = new FuturesTickTradeData(
                100,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
                0,
                decimal.ToInt64(price * 1_000_000_000m),
                price,
                size,
                1,
                2,
                0)
        };
    }

    private static FuturesOptionTickDataStreamingStartedEvent CreateStreamingStartedEvent(
        FuturesOptionContractReadModel option)
    {
        var entityId = new FuturesOptionTickEntityId(option.ContractId, SampleData.ValueDate);
        return new FuturesOptionTickDataStreamingStartedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesOptionTickDataEventActor.Actor,
                FuturesOptionTickDataStreamingStartedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            AggregateId = entityId.Format(),
            EventSource = "test",
            ReceivedOn = DateTime.UtcNow,
            Contract = option,
            BaseContract = SampleData.EsContract,
            ValueDate = SampleData.ValueDate,
            MaturityDate = option.ContractMonth,
            RiskFreeRate = 0.04,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    private static FuturesOptionTickDataStreamingStoppedEvent CreateStreamingStoppedEvent(
        string contractId)
    {
        var entityId = new FuturesOptionTickEntityId(contractId, SampleData.ValueDate);
        return new FuturesOptionTickDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesOptionTickDataEventActor.Actor,
                FuturesOptionTickDataStreamingStoppedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            AggregateId = entityId.Format(),
            EventSource = "test",
            ReceivedOn = DateTime.UtcNow,
            ContractId = contractId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }
}
