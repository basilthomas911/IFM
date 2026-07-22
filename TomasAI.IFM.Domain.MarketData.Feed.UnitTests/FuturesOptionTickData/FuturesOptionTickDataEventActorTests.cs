using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData;

public class FuturesOptionTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionTickDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesOptionTickDataEventActor : FuturesOptionTickDataEventActor
    {
        public TestableFuturesOptionTickDataEventActor(
            IActorSupervisor supervisor,
             IMarketDataApi marketDataApi,
            IMarketDataSnapshotApi marketDataSnapshotApi,
            IBlackboardService blackboardService,
            IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesOptionTickDataEventActor> logger)
            : base(supervisor, marketDataApi, marketDataSnapshotApi, blackboardService, optionTradeLiveFeedMap, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    [Theory]
    [MemberData(nameof(SupportedEvents))]
    public void ParseMessage_SupportedEvent_ReturnsConcreteEventWithIdentity(IEvent @event)
    {
        var actor = CreateActor();

        var parsed = actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        parsed.GetType().Should().Be(@event.GetType());
        parsed.CommandId.Should().Be(@event.CommandId);
        parsed.Subject.Should().Be(@event.Subject);
    }

    [Fact]
    public void ParseMessage_InsertedEvent_PreservesSampleData()
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent();

        var parsed = actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        var inserted = parsed.Should().BeOfType<FuturesOptionTickDataInsertedEvent>().Which;
        inserted.Contract.Should().BeEquivalentTo(SampleData.EsContract);
        inserted.TickData.Should().BeEquivalentTo(SampleData.EsOptionTickData);
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, "WrongActor", FuturesOptionTickDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, FuturesOptionTickDataEventActor.Actor, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ReturnsNull(
        ActorType actorType, string actorName, string verb)
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent();
        var message = new NatsMsg<byte[]>
        {
            Subject = new ActorSubject(actorType, actorName, verb, @event.EntityId.Format()).ToString(),
            Data = Serialize(@event)
        };

        var parsed = actor.InvokeParseMessage(Substitute.For<IEventActorContext>(), message);

        parsed.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var actor = CreateActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateInsertedEvent()));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidPayload_Throws(bool empty)
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent();
        var message = new NatsMsg<byte[]>
        {
            Subject = @event.Subject.ToString(),
            Data = empty ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IEventActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_EmptyCommandId_Throws()
    {
        var actor = CreateActor();

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(CreateInsertedEvent(Guid.Empty)));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public async Task ReceiveAsync_InsertedEvent_PublishesOptionTradeTickUpdate()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();
        context.SendAsync<TomasAI.IFM.Shared.Trade.Events.OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(
                Arg.Any<TomasAI.IFM.Shared.Trade.Events.OptionTradeTickPriceDataUpdatedEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeReceiveAsync(context, @event);

        await context.Received(1)
            .SendAsync<TomasAI.IFM.Shared.Trade.Events.OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(
                Arg.Is<TomasAI.IFM.Shared.Trade.Events.OptionTradeTickPriceDataUpdatedEvent>(value =>
                    value.CommandId == @event.CommandId && value.OptionTickData.ContractId == SampleData.EsOptionTickData.ContractId));
    }

    [Fact]
    public async Task ReceiveAsync_InsertedEventPublishFails_HandlesFailureWithoutLeakingIt()
    {
        var status = Substitute.For<IStatusConsoleWriter>();
        var actor = CreateActor(statusConsoleWriter: status);
        var context = Substitute.For<IEventActorContext>();
        context.SendAsync<TomasAI.IFM.Shared.Trade.Events.OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(
                Arg.Any<TomasAI.IFM.Shared.Trade.Events.OptionTradeTickPriceDataUpdatedEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("publish failed"));

        Func<Task> act = () => actor.InvokeReceiveAsync(context, CreateInsertedEvent()).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStartFails_SendsFailureEvent()
    {
        var marketDataApi = Substitute.For<IMarketDataApi>();
        marketDataApi.Start(Arg.Any<Action<int, string>>()).Returns(false);
        var actor = CreateActor(marketDataApi: marketDataApi);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent();

        await actor.InvokeReceiveAsync(context, @event);

        marketDataApi.Received(1).Start(Arg.Any<Action<int, string>>());
        await context.Received(1)
            .SendAsync<FuturesOptionTickDataStreamingStartedFailEvent, FuturesOptionTickEntityId>(
                Arg.Is<FuturesOptionTickDataStreamingStartedFailEvent>(value =>
                    value.CommandId == @event.CommandId && value.ErrorMessage.Contains("failed to start")));
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStopWithoutRequest_SendsFailureEvent()
    {
        var marketDataApi = Substitute.For<IMarketDataApi>();
        var actor = CreateActor(marketDataApi: marketDataApi);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStoppedEvent();

        await actor.InvokeReceiveAsync(context, @event);

        marketDataApi.DidNotReceive().StopStreamingFuturesOptionTickData(Arg.Any<int>());
        await context.Received(1)
            .SendAsync<FuturesOptionTickDataStreamingStoppedFailEvent, FuturesOptionTickEntityId>(
                Arg.Is<FuturesOptionTickDataStreamingStoppedFailEvent>(value =>
                    value.CommandId == @event.CommandId && value.ErrorMessage.Contains("not found")));
    }

    [Fact]
    public async Task ReceiveAsync_BidAskWithoutStreamingRequest_CompletesWithoutOutgoingCommands()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();

        Func<Task> act = () => actor.InvokeReceiveAsync(context, CreateTickPriceDataEvent()).AsTask();

        await act.Should().NotThrowAsync();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<TomasAI.IFM.Shared.MarketDataFeed.Commands.InsertFuturesOptionTickDataCommand, FuturesOptionTickEntityId>(default!);
    }

    [Fact]
    public async Task ReceiveAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, @event).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ParsedPriceDataEvent_IsRejectedBecauseNoReceiveHandlerExists()
    {
        var actor = CreateActor();
        var @event = CreatePriceInsertedEvent();
        actor.InvokeParseMessage(Substitute.For<IEventActorContext>(), CreateMessage(@event))
            .Should().BeOfType<FuturesOptionTickPriceDataInsertedEvent>();

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IEventActorContext>(), @event).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataEventActor.Actor} event from message: *");
    }

    [Fact]
    public async Task ReceiveAsync_UnknownEvent_ThrowsInvalidOperationException()
    {
        var actor = CreateActor();
        var @event = Substitute.For<IEvent>();
        @event.Subject.Returns(new ActorSubject(
            ActorType.Event, FuturesOptionTickDataEventActor.Actor, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IEventActorContext>(), @event).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ValidInputs_SendsEventExceptionEvent()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new InvalidOperationException("event failed"));

        await context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(value =>
                    value.ErrorMessage == "event failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_FirstPublishFails_RetriesWithPublishingException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();
        var count = 0;
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(_ =>
            {
                count++;
                if (count == 1)
                    throw new InvalidOperationException("first publish failed");
                return ValueTask.CompletedTask;
            });

        await actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new Exception("original failure"));

        await context.Received(2)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    [Fact]
    public async Task OnExceptionAsync_DefaultThreadOrNullEvent_AreConvertedToErrorEvents()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();

        await actor.InvokeOnExceptionAsync(context, default, @event, new Exception("failure"));
        await actor.InvokeOnExceptionAsync(context, @event.Subject.ThreadId, null!, new Exception("failure"));

        await context.Received(2)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    TestableFuturesOptionTickDataEventActor CreateActor(
        IActorSupervisor? supervisor = null,
        IMarketDataApi? marketDataApi = null,
        IMarketDataSnapshotApi? marketDataSnapshotApi = null,
        IBlackboardService? blackboardService = null,
        IOptionTradeLiveFeedMap? optionTradeLiveFeedMap = null,
        IStatusConsoleWriter? statusConsoleWriter = null,
        ILogger<FuturesOptionTickDataEventActor>? logger = null)
        => _fixture.CreateActor(
            supervisor ?? Substitute.For<IActorSupervisor>(),
            marketDataApi ?? Substitute.For<IMarketDataApi>(),
            marketDataSnapshotApi ?? Substitute.For<IMarketDataSnapshotApi>(),
            blackboardService ?? CreateBlackboard(),
            optionTradeLiveFeedMap ?? Substitute.For<IOptionTradeLiveFeedMap>(),
            statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>(),
            logger ?? Substitute.For<ILogger<FuturesOptionTickDataEventActor>>());

    static IBlackboardService CreateBlackboard()
    {
        var blackboard = Substitute.For<IBlackboardService>();
        blackboard.StreamingRequestId.Returns(new StreamingRequestIdModel(
            Substitute.For<IRedisCache>(), Substitute.For<IJsonSerializer>()));
        return blackboard;
    }

    public static IEnumerable<object[]> SupportedEvents()
    {
        yield return [CreateInsertedEvent()];
        yield return [CreatePriceInsertedEvent()];
        yield return [CreateStreamingStartedEvent()];
        yield return [CreateStreamingStoppedEvent()];
        yield return [CreateTickPriceDataEvent()];
    }

    static NatsMsg<byte[]> CreateMessage(IEvent @event)
        => new() { Subject = @event.Subject.ToString(), Data = Serialize(@event) };

    static byte[] Serialize(IEvent @event) => @event switch
    {
        FuturesOptionTickDataInsertedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesOptionTickPriceDataInsertedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesOptionTickDataStreamingStartedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesOptionTickDataStreamingStoppedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesOptionTickBidAskEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(@event))
    };

    static FuturesOptionTickDataInsertedEvent CreateInsertedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesOptionTickEntityId(SampleData.EsOptionTickData.ContractId, SampleData.ValueDate);
        return new FuturesOptionTickDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.EsContract,
            TickData = SampleData.EsOptionTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static FuturesOptionTickDataStreamingStartedEvent CreateStreamingStartedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.OptionTickStreamingFeedId;
        return new FuturesOptionTickDataStreamingStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickDataStreamingStartedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.FuturesOptionContracts[0],
            BaseContract = SampleData.EsContract,
            ValueDate = SampleData.ValueDate,
            MaturityDate = SampleData.OptionMaturityDate,
            RiskFreeRate = SampleData.RiskFreeRate,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesOptionTickPriceDataInsertedEvent CreatePriceInsertedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.EsOptionTickData.EntityId;
        return new FuturesOptionTickPriceDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event, FuturesOptionTickDataEventActor.Actor,
                FuturesOptionTickPriceDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 5,
            AggregateId = entityId.Format(),
            ReceivedOn = DateTime.UtcNow,
            EventSource = "UnitTest",
            Contract = SampleData.EsContract,
            TickData = SampleData.EsOptionTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static FuturesOptionTickDataStreamingStoppedEvent CreateStreamingStoppedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.OptionTickStreamingFeedId;
        return new FuturesOptionTickDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickDataStreamingStoppedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            ContractId = SampleData.FuturesOptionContracts[0].ContractId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

    static FuturesOptionTickBidAskEvent CreateTickPriceDataEvent(Guid? commandId = null)
    {
        var entityId = SampleData.OptionTickStreamingFeedId;
        var tickPriceData = new FuturesOptionTickBidAskReadModel(
            DateTime.UtcNow, DateTime.UtcNow.Ticks, 12.25, 12.75, 100, 150);
        return new FuturesOptionTickBidAskEvent(1001, tickPriceData)
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickBidAskEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 4,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test"
        };
    }

}
