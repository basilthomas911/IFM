using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionQuoteData;

public class FuturesOptionQuoteDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionQuoteDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesOptionQuoteDataEventActor : FuturesOptionQuoteDataEventActor
    {
        public TestableFuturesOptionQuoteDataEventActor(IActorSupervisor supervisor,
             IMarketDataSnapshotApi marketDataSnapshotApi,
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesOptionQuoteDataEventActor> logger)
            : base(supervisor, marketDataSnapshotApi, blackboardService, statusConsoleWriter, logger)
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
    public void ParseMessage_InsertedCompleteEvent_PreservesSampleQuoteData()
    {
        var actor = CreateActor();
        var @event = CreateInsertedCompleteEvent();

        var parsed = actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        var inserted = parsed.Should().BeOfType<FuturesOptionQuoteDataInsertedCompleteEvent>().Which;
        inserted.QuoteId.Should().Be(SampleData.OptionQuoteStreamId);
        inserted.OptionQuoteData.ContractId.Should().Be(SampleData.FuturesOptionQuotes[0].ContractId);
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesOptionQuoteDataEventActor.Actor, FuturesOptionQuoteDataInsertedCompleteEvent.Verb)]
    [InlineData(ActorType.Event, "WrongActor", FuturesOptionQuoteDataInsertedCompleteEvent.Verb)]
    [InlineData(ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ReturnsNull(
        ActorType actorType, string actorName, string verb)
    {
        var actor = CreateActor();
        var @event = CreateInsertedCompleteEvent();
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

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateInsertedCompleteEvent()));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidPayload_Throws(bool empty)
    {
        var actor = CreateActor();
        var @event = CreateInsertedCompleteEvent();
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
            Substitute.For<IEventActorContext>(), CreateMessage(CreateInsertedCompleteEvent(Guid.Empty)));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public async Task ReceiveAsync_InsertedCompleteEvent_PublishesUpdatedEvent()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedCompleteEvent();
        context.SendAsync<FuturesOptionQuoteDataUpdatedEvent, QuoteId>(
                Arg.Any<FuturesOptionQuoteDataUpdatedEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeReceiveAsync(context, @event);

        await context.Received(1).SendAsync<FuturesOptionQuoteDataUpdatedEvent, QuoteId>(
            Arg.Is<FuturesOptionQuoteDataUpdatedEvent>(value =>
                value.CommandId == @event.CommandId && value.OptionQuoteData == @event.OptionQuoteData));
    }

    [Fact]
    public async Task ReceiveAsync_InsertedUpdatePublishFails_HandlesFailureWithoutLeakingIt()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        context.SendAsync<FuturesOptionQuoteDataUpdatedEvent, QuoteId>(
                Arg.Any<FuturesOptionQuoteDataUpdatedEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("publish failed"));

        Func<Task> act = () => actor.InvokeReceiveAsync(
            context, CreateInsertedCompleteEvent()).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStartedComplete_StartsSnapshotApiAndCachesQuoteSet()
    {
        var snapshotApi = Substitute.For<IMarketDataSnapshotApi>();
        var (blackboard, redis, serializer) = CreateBlackboard();
        serializer.Serialize(Arg.Any<FuturesOptionQuoteReadModel[]>()).Returns("quotes");
        var actor = CreateActor(snapshotApi: snapshotApi, blackboard: blackboard);
        var @event = CreateStreamingStartedCompleteEvent() with
        {
            FuturesOptionQuotes = [],
            FuturesOptionContracts = []
        };

        await actor.InvokeReceiveAsync(Substitute.For<IEventActorContext>(), @event);

        snapshotApi.Received(1).Start(Arg.Any<Action<int, string>>());
        redis.Received(1).Set(
            Arg.Is<string>(key => key.EndsWith($":{SampleData.OptionQuoteStreamId}")), "quotes");
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStartFailure_IsHandledInsideTheEventHandler()
    {
        var snapshotApi = Substitute.For<IMarketDataSnapshotApi>();
        snapshotApi.When(value => value.Start(Arg.Any<Action<int, string>>()))
            .Do(_ => throw new InvalidOperationException("snapshot start failed"));
        var actor = CreateActor(snapshotApi: snapshotApi);
        var @event = CreateStreamingStartedCompleteEvent() with
        {
            FuturesOptionQuotes = [],
            FuturesOptionContracts = []
        };

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IEventActorContext>(), @event).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStoppedComplete_StopsSnapshotApiAndClearsQuoteSet()
    {
        var snapshotApi = Substitute.For<IMarketDataSnapshotApi>();
        var (blackboard, redis, _) = CreateBlackboard();
        var actor = CreateActor(snapshotApi: snapshotApi, blackboard: blackboard);
        var @event = CreateStreamingStoppedCompleteEvent() with { FuturesOptionQuotes = [] };

        await actor.InvokeReceiveAsync(Substitute.For<IEventActorContext>(), @event);

        snapshotApi.Received(1).Stop();
        redis.Received(1).Set(
            Arg.Is<string>(key => key.EndsWith($":{SampleData.OptionQuoteStreamId}")), string.Empty);
    }

    [Fact]
    public async Task ReceiveAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedCompleteEvent();

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, @event).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedEvent_ThrowsInvalidOperationException()
    {
        var actor = CreateActor();
        var @event = Substitute.For<IEvent>();
        @event.Subject.Returns(new ActorSubject(
            ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IEventActorContext>(), @event).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionQuoteDataEventActor.Actor} event from message: *");
    }

    [Fact]
    public async Task OnExceptionAsync_ValidInputs_SendsEventExceptionEvent()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedCompleteEvent();
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new InvalidOperationException("event failed"));

        await context.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(value =>
                value.ErrorMessage == "event failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_FirstPublishFails_RetriesWithPublishingException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedCompleteEvent();
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

        await context.Received(2).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    [Fact]
    public async Task OnExceptionAsync_DefaultThreadOrNullEvent_AreConvertedToErrorEvents()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedCompleteEvent();

        await actor.InvokeOnExceptionAsync(context, default, @event, new Exception("failure"));
        await actor.InvokeOnExceptionAsync(context, @event.Subject.ThreadId, null!, new Exception("failure"));

        await context.Received(2).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    TestableFuturesOptionQuoteDataEventActor CreateActor(
        IActorSupervisor? supervisor = null,
        IMarketDataSnapshotApi? snapshotApi = null,
        IBlackboardService? blackboard = null,
        IStatusConsoleWriter? statusConsole = null,
        ILogger<FuturesOptionQuoteDataEventActor>? logger = null)
        => _fixture.CreateActor(
            supervisor ?? Substitute.For<IActorSupervisor>(),
            snapshotApi ?? Substitute.For<IMarketDataSnapshotApi>(),
            blackboard ?? CreateBlackboard().Blackboard,
            statusConsole ?? Substitute.For<IStatusConsoleWriter>(),
            logger ?? Substitute.For<ILogger<FuturesOptionQuoteDataEventActor>>());

    static (IBlackboardService Blackboard, IRedisCache Redis, IJsonSerializer Serializer) CreateBlackboard()
    {
        var blackboard = Substitute.For<IBlackboardService>();
        var redis = Substitute.For<IRedisCache>();
        var serializer = Substitute.For<IJsonSerializer>();
        blackboard.FuturesOptionQuote.Returns(new FuturesOptionQuoteModel(redis, serializer));
        blackboard.FuturesOptionQuoteData.Returns(new FuturesOptionQuoteDataModel(redis, serializer));
        return (blackboard, redis, serializer);
    }

    public static IEnumerable<object[]> SupportedEvents()
    {
        yield return [CreateInsertedCompleteEvent()];
        yield return [CreateStreamingStartedCompleteEvent()];
        yield return [CreateStreamingStoppedCompleteEvent()];
    }

    static NatsMsg<byte[]> CreateMessage(IEvent @event)
        => new() { Subject = @event.Subject.ToString(), Data = Serialize(@event) };

    static byte[] Serialize(IEvent @event) => @event switch
    {
        FuturesOptionQuoteDataInsertedCompleteEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesOptionQuoteDataStreamingStartedCompleteEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesOptionQuoteDataStreamingStoppedCompleteEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(@event))
    };

    static FuturesOptionQuoteDataInsertedCompleteEvent CreateInsertedCompleteEvent(Guid? commandId = null)
    {
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        return new FuturesOptionQuoteDataInsertedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, FuturesOptionQuoteDataInsertedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            QuoteId = SampleData.OptionQuoteStreamId,
            OptionQuoteData = new FuturesOptionQuoteDataReadModel(SampleData.OptionQuoteStreamId, "ES20240601C5500", 1001, 12.50m, 10, 13.00m, 5),
            UpdatedOn = DateTime.UtcNow,
            UpdatedBy = "UnitTest"
        };
    }

    static FuturesOptionQuoteDataStreamingStartedCompleteEvent CreateStreamingStartedCompleteEvent(Guid? commandId = null)
    {
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        return new FuturesOptionQuoteDataStreamingStartedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, FuturesOptionQuoteDataStreamingStartedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            QuoteId = SampleData.OptionQuoteStreamId,
            FuturesOptionQuotes = SampleData.FuturesOptionQuotes,
            FuturesOptionContracts = SampleData.FuturesOptionContracts,
            FuturesOptionQuoteData = [.. SampleData.FuturesOptionQuotes.Select(o => new FuturesOptionQuoteDataReadModel(o.QuoteId, o.ContractId, o.RequestId, -1, -1, -1, -1))],
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesOptionQuoteDataStreamingStoppedCompleteEvent CreateStreamingStoppedCompleteEvent(Guid? commandId = null)
    {
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        return new FuturesOptionQuoteDataStreamingStoppedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, FuturesOptionQuoteDataStreamingStoppedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            QuoteId = SampleData.OptionQuoteStreamId,
            FuturesOptionQuotes = SampleData.FuturesOptionQuotes,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

}
