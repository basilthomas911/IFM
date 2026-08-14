using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData;

public sealed class FuturesTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    private readonly MarketDataFeedTestFixture _fixture;

    public FuturesTickDataEventActorTests(MarketDataFeedTestFixture fixture) =>
        _fixture = fixture;

    public sealed class TestableFuturesTickDataEventActor : FuturesTickDataEventActor
    {
        public TestableFuturesTickDataEventActor(
            IActorSupervisor supervisor,
            ApplicationMarketDataApi marketDataApi,
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesTickDataEventActor> logger)
            : base(
                supervisor,
                new global::TomasAI.IFM.Domain.MarketData.Feed.Command.Api.ActorMarketDataFeedCommandApiFactory(),
                new global::TomasAI.IFM.Domain.MarketData.Feed.Event.Api.ActorMarketDataFeedEventApiFactory(),
                marketDataApi,
                blackboardService,
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
        var actor = CreateActor();
        var parsed = actor.Parse(
            Substitute.For<IEventActorContext>(),
            CreateRoutedMessage(@event));

        parsed.Should().BeOfType(@event.GetType());
        parsed.CommandId.Should().Be(@event.CommandId);
    }

    [Fact]
    public void ParseMessage_TradeEvent_PreservesDecimalTradeAndInstrumentIdentity()
    {
        var actor = CreateActor();
        var @event = CreateTradeEvent();

        var parsed = actor.Parse(
            Substitute.For<IEventActorContext>(),
            CreateRoutedMessage(@event));

        var trade = parsed.Should().BeOfType<FuturesTickTradeDataInsertedEvent>().Which;
        trade.InstrumentId.Should().Be(42);
        trade.EntityId.ContractId.Should().Be(SampleData.EsContract.ContractId);
        trade.TradeData.Price.Should().Be(5450.25m);
        trade.TradeData.Size.Should().Be(10);
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesTickDataEventActor.Actor, FuturesTickTradeDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, "WrongActor", FuturesTickTradeDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, FuturesTickDataEventActor.Actor, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ReturnsNull(
        ActorType actorType,
        string actorName,
        string verb)
    {
        var @event = CreateTradeEvent();
        var message = CreateRoutedMessage(@event) with
        {
            Subject = new ActorSubject(
                actorType,
                actorName,
                verb,
                @event.EntityId.Format()).ToString()
        };

        CreateActor().Parse(Substitute.For<IEventActorContext>(), message)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidTradePayload_Throws(bool empty)
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
    public async Task StartupAndShutdown_RegisterAndRemoveOnlyTheDurableTradeRoute()
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
    public async Task StreamingStart_AcquiresDeterministicLeaseAndPublishesCompletion()
    {
        var api = Substitute.For<ApplicationMarketDataApi>();
        var reader = CreateReader(CreateDetails(SampleData.EsContract.ContractId, "ES"));
        api.CreateTickerDataReaderAsync(
                Arg.Any<TickerReaderOwner>(),
                SampleData.EsContract.ContractId,
                Arg.Any<CancellationToken>())
            .Returns(reader);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent();

        await actor.Receive(context, @event);

        await api.Received(1).CreateTickerDataReaderAsync(
            Arg.Is<TickerReaderOwner>(owner =>
                owner.WorkflowType == nameof(FuturesTickDataEventActor)
                && owner.WorkflowId == @event.EntityId.Format()
                && owner.LegId == SampleData.EsContract.ContractId),
            SampleData.EsContract.ContractId,
            Arg.Any<CancellationToken>());
        await context.Received(1).SendAsync<
            FuturesTickDataStreamingStartedCompleteEvent,
            FuturesTickDataStreamingId>(Arg.Is<FuturesTickDataStreamingStartedCompleteEvent>(
                complete => complete.CommandId == @event.CommandId));
    }

    [Fact]
    public async Task StreamingStart_LeaseFailurePublishesFailureWithoutLeakingException()
    {
        var api = Substitute.For<ApplicationMarketDataApi>();
        api.CreateTickerDataReaderAsync(
                Arg.Any<TickerReaderOwner>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<ITickerDataReader>(
                new InvalidOperationException("lease failed")));
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent();

        var action = () => actor.Receive(context, @event).AsTask();

        await action.Should().NotThrowAsync();
        await context.Received(1).SendAsync<
            FuturesTickDataStreamingStartedFailEvent,
            FuturesTickDataStreamingId>(Arg.Is<FuturesTickDataStreamingStartedFailEvent>(
                failed => failed.CommandId == @event.CommandId
                    && failed.ErrorMessage == "lease failed"));
    }

    [Fact]
    public async Task StreamingStop_DisposesOwnedReaderAndPublishesCompletion()
    {
        var api = Substitute.For<ApplicationMarketDataApi>();
        var reader = CreateReader(CreateDetails(SampleData.EsContract.ContractId, "ES"));
        api.CreateTickerDataReaderAsync(
                Arg.Any<TickerReaderOwner>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(reader);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        await actor.Receive(context, CreateStreamingStartedEvent());

        await actor.Receive(context, CreateStreamingStoppedEvent());

        await reader.Received(1).DisposeAsync();
        await context.Received(1).SendAsync<
            FuturesTickDataStreamingStoppedCompleteEvent,
            FuturesTickDataStreamingId>(Arg.Any<FuturesTickDataStreamingStoppedCompleteEvent>());
    }

    [Fact]
    public async Task DurableTradeWithoutActiveLease_IsAcknowledgedWithoutDomainCommand()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();

        await actor.Receive(context, CreateTradeEvent());

        await context.DidNotReceive().RequestAsync<
            InsertVixFuturesEodDataCommand,
            FuturesEodDataId>(Arg.Any<InsertVixFuturesEodDataCommand>());
    }

    [Fact]
    public async Task VixTradeWithActiveLease_UsesExactDurableTradeForEodCommand()
    {
        var contractId = SampleData.VixTickData.ContractId;
        var api = Substitute.For<ApplicationMarketDataApi>();
        var reader = CreateReader(CreateDetails(contractId, "VX"));
        api.CreateTickerDataReaderAsync(
                Arg.Any<TickerReaderOwner>(),
                contractId,
                Arg.Any<CancellationToken>())
            .Returns(reader);
        var (blackboard, redis) = CreateBlackboard();
        var actor = CreateActor(marketDataApi: api, blackboard: blackboard);
        var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(
                Arg.Any<InsertVixFuturesEodDataCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        await actor.Receive(context, CreateStreamingStartedEvent(
            SampleData.EsContract with
            {
                ContractId = contractId,
                Symbol = "VX",
                LocalSymbol = "VXM4"
            }));

        await actor.Receive(context, CreateTradeEvent(contractId, AssetTypeId.Futures, 21.75m, 17));

        await context.Received(1).RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(
            Arg.Is<InsertVixFuturesEodDataCommand>(command =>
                command.VixFuturesTickData.ContractId == contractId
                && command.VixFuturesTickData.Price == 21.75m
                && command.VixFuturesTickData.Size == 17));
        redis.Received(1).Set(
            Arg.Is<string>(key => key.Contains($":{SampleData.ValueDate:yyyyMMdd}")),
            contractId);
    }

    [Fact]
    public async Task DurableTradeAfterReaderLeaseExpires_IsAcknowledgedWithoutCommand()
    {
        var api = Substitute.For<ApplicationMarketDataApi>();
        var reader = CreateReader(CreateDetails(SampleData.VixTickData.ContractId, "VX"));
        reader.GetContractDetails().Returns(_ => throw new TickerLeaseNotActiveException(
            reader.Lease,
            TickerLeaseFailureReason.LeaseReleased));
        api.CreateTickerDataReaderAsync(
                Arg.Any<TickerReaderOwner>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(reader);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        await actor.Receive(context, CreateStreamingStartedEvent(
            SampleData.EsContract with { ContractId = SampleData.VixTickData.ContractId, Symbol = "VX" }));

        var action = () => actor.Receive(
            context,
            CreateTradeEvent(SampleData.VixTickData.ContractId)).AsTask();

        await action.Should().NotThrowAsync();
        await context.DidNotReceive().RequestAsync<
            InsertVixFuturesEodDataCommand,
            FuturesEodDataId>(Arg.Any<InsertVixFuturesEodDataCommand>());
    }

    [Fact]
    public async Task OptionTrade_IsIgnoredByFuturesActorEvenWhenContractIdMatches()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();

        await actor.Receive(context, CreateTradeEvent(assetType: AssetTypeId.FuturesOption));

        context.ReceivedCalls().Should().BeEmpty();
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
            FuturesTickDataEventActor.Actor,
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

    private TestableFuturesTickDataEventActor CreateActor(
        ApplicationMarketDataApi? marketDataApi = null,
        IBlackboardService? blackboard = null,
        IStatusConsoleWriter? statusConsole = null) =>
        _fixture.CreateActor(
            Substitute.For<IActorSupervisor>(),
            marketDataApi ?? Substitute.For<ApplicationMarketDataApi>(),
            blackboard ?? CreateBlackboard().Blackboard,
            statusConsole ?? Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesTickDataEventActor>>());

    private static ITickerDataReader CreateReader(TickerContractDetails details)
    {
        var reader = Substitute.For<ITickerDataReader>();
        var owner = new TickerReaderOwner("test", "workflow", details.ContractId);
        reader.ContractId.Returns(details.ContractId);
        reader.Owner.Returns(owner);
        reader.Lease.Returns(new TickerStreamLease(Guid.NewGuid(), details.ContractId, owner, 1));
        reader.GetContractDetails().Returns(details);
        reader.DisposeAsync().Returns(ValueTask.CompletedTask);
        return reader;
    }

    private static TickerContractDetails CreateDetails(string contractId, string ticker) => new()
    {
        ContractId = contractId,
        InstrumentId = 42,
        PublisherId = 7,
        AssetTypeId = AssetTypeId.Futures,
        Dataset = "GLBX.MDP3",
        DefinitionDate = SampleData.ValueDate,
        ProviderContractId = contractId,
        Ticker = ticker,
        LocalSymbol = contractId,
        SecurityType = "FUT",
        Currency = "USD",
        Exchange = "CME",
        ContractMultiplier = 50m,
        MaturityDate = SampleData.EsContract.LastTradeDate,
        IsCurrentlyTraded = true
    };

    private static (IBlackboardService Blackboard, IRedisCache Redis) CreateBlackboard()
    {
        var blackboard = Substitute.For<IBlackboardService>();
        var redis = Substitute.For<IRedisCache>();
        var serializer = Substitute.For<IJsonSerializer>();
        blackboard.MarketDataFeed.VixFuturesContractId.Returns(
            new VixFuturesContractIdCacheModel(redis, serializer));
        return (blackboard, redis);
    }

    public static IEnumerable<object[]> SupportedEvents()
    {
        yield return [CreateTradeEvent()];
        yield return [CreateStreamingStartedEvent()];
        yield return [CreateStreamingStoppedEvent()];
    }

    private static NatsMsg<byte[]> CreateRoutedMessage(IEvent @event) => new()
    {
        Subject = new ActorSubject(
            ActorType.Event,
            FuturesTickDataEventActor.Actor,
            @event.Subject.Verb,
            @event.Subject.EntityId).ToString(),
        Data = @event switch
        {
            FuturesTickTradeDataInsertedEvent value =>
                ActorExtensions.DataSerializer!.Serialize(value),
            FuturesTickDataStreamingStartedEvent value =>
                ActorExtensions.DataSerializer!.Serialize(value),
            FuturesTickDataStreamingStoppedEvent value =>
                ActorExtensions.DataSerializer!.Serialize(value),
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        }
    };

    private static FuturesTickTradeDataInsertedEvent CreateTradeEvent(
        string? contractId = null,
        AssetTypeId assetType = AssetTypeId.Futures,
        decimal price = 5450.25m,
        uint size = 10)
    {
        contractId ??= SampleData.EsContract.ContractId;
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
            InstrumentId = 42,
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

    private static FuturesTickDataStreamingStartedEvent CreateStreamingStartedEvent(
        global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV2ReadModel? contract = null)
    {
        var entityId = new FuturesTickDataStreamingId(SampleData.ValueDate);
        return new FuturesTickDataStreamingStartedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesTickDataEventActor.Actor,
                FuturesTickDataStreamingStartedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            AggregateId = entityId.Format(),
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = contract ?? SampleData.EsContract,
            ValueDate = SampleData.ValueDate,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    private static FuturesTickDataStreamingStoppedEvent CreateStreamingStoppedEvent()
    {
        var entityId = new FuturesTickDataStreamingId(SampleData.ValueDate);
        return new FuturesTickDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesTickDataEventActor.Actor,
                FuturesTickDataStreamingStoppedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            AggregateId = entityId.Format(),
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            ContractId = SampleData.EsContract.ContractId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }
}
