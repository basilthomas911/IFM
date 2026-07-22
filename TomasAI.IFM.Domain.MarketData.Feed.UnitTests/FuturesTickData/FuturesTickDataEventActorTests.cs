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
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData;

public class FuturesTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesTickDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTickDataEventActor : FuturesTickDataEventActor
    {
        public TestableFuturesTickDataEventActor(IActorSupervisor supervisor,
            IMarketDataApi marketDataApi,
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesTickDataEventActor> logger)
            : base(supervisor, marketDataApi, blackboardService, statusConsoleWriter, logger)
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
    public void ParseMessage_InsertedEvent_PreservesSampleTickData()
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent();

        var parsed = actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        var inserted = parsed.Should().BeOfType<FuturesTickDataInsertedEvent>().Which;
        inserted.Contract.Should().Be(SampleData.EsContract);
        inserted.TickData.Should().Be(SampleData.EsTickData);
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesTickDataEventActor.Actor, FuturesTickDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, "WrongActor", FuturesTickDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, FuturesTickDataEventActor.Actor, "UnknownVerb")]
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
    public async Task ReceiveAsync_VixInsertedEvent_RequestsEodInsertAndCachesContract()
    {
        var (blackboard, redis) = CreateBlackboard();
        var actor = CreateActor(blackboard: blackboard);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateVixInsertedEvent();
        context.RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(
                Arg.Any<InsertVixFuturesEodDataCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));

        await actor.InvokeReceiveAsync(context, @event);

        await context.Received(1).RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(
            Arg.Is<InsertVixFuturesEodDataCommand>(command =>
                command.VixFuturesTickData == SampleData.VixTickData));
        redis.Received(1).Set(
            Arg.Is<string>(key => key.Contains($":{SampleData.ValueDate:yyyyMMdd}")),
            SampleData.VixTickData.ContractId);
    }

    [Fact]
    public async Task ReceiveAsync_VixInsertFailure_IsHandledInsideEventHandler()
    {
        var (blackboard, _) = CreateBlackboard();
        var status = Substitute.For<IStatusConsoleWriter>();
        var actor = CreateActor(blackboard: blackboard, statusConsole: status);
        var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(
                Arg.Any<InsertVixFuturesEodDataCommand>())
            .Returns(new ServiceFailed<GuidResult>(5005, "insert failed"));

        Func<Task> act = () => actor.InvokeReceiveAsync(
            context, CreateVixInsertedEvent()).AsTask();

        await act.Should().NotThrowAsync();
        await status.Received(1).WriteConsoleAsync(
            Arg.Any<Shared.StatusConsole.LogSourceType>(), 6009, Arg.Any<string>());
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStartedEvent_StartsFeedCachesParametersAndPublishesCompletion()
    {
        var api = Substitute.For<IMarketDataApi>();
        var streamIds = Substitute.For<IStreamIdCollection>();
        api.StreamIds.Returns(streamIds);
        api.Start(Arg.Any<Action<int, string>>()).Returns(true);
        streamIds.Add(SampleData.EsContract.ContractId).Returns(42);
        var (blackboard, redis) = CreateBlackboard();
        var actor = CreateActor(marketDataApi: api, blackboard: blackboard);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent();

        await actor.InvokeReceiveAsync(context, @event);

        api.Received(1).Start(Arg.Any<Action<int, string>>());
        api.Received(1).StartStreamingFuturesTickData(42, SampleData.ValueDate, SampleData.EsContract);
        redis.Received(1).Set(
            Arg.Is<string>(key => key.EndsWith(":42")), Arg.Any<string>());
        await context.Received(1).SendAsync<FuturesTickDataStreamingStartedCompleteEvent, FuturesTickDataStreamingId>(
            Arg.Is<FuturesTickDataStreamingStartedCompleteEvent>(value =>
                value.CommandId == @event.CommandId));
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStartFailure_PublishesFailureWithoutLeakingException()
    {
        var api = Substitute.For<IMarketDataApi>();
        api.Start(Arg.Any<Action<int, string>>()).Returns(false);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent();

        Func<Task> act = () => actor.InvokeReceiveAsync(context, @event).AsTask();

        await act.Should().NotThrowAsync();
        await context.Received(1).SendAsync<FuturesTickDataStreamingStartedFailEvent, FuturesTickDataStreamingId>(
            Arg.Is<FuturesTickDataStreamingStartedFailEvent>(value =>
                value.CommandId == @event.CommandId));
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStoppedEvent_StopsFeedRemovesStreamAndPublishesCompletion()
    {
        var api = Substitute.For<IMarketDataApi>();
        var streamIds = Substitute.For<IStreamIdCollection>();
        api.StreamIds.Returns(streamIds);
        streamIds[SampleData.EsContract.ContractId].Returns(42);
        api.StopStreamingFuturesTickData(42).Returns(true);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStoppedEvent();

        await actor.InvokeReceiveAsync(context, @event);

        api.Received(1).StopStreamingFuturesTickData(42);
        streamIds.Received(1).Remove(42);
        await context.Received(1).SendAsync<FuturesTickDataStreamingStoppedCompleteEvent, FuturesTickDataStreamingId>(
            Arg.Is<FuturesTickDataStreamingStoppedCompleteEvent>(value =>
                value.CommandId == @event.CommandId));
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStopReturnsFalse_DoesNotPublishCompletion()
    {
        var api = Substitute.For<IMarketDataApi>();
        var streamIds = Substitute.For<IStreamIdCollection>();
        api.StreamIds.Returns(streamIds);
        streamIds[SampleData.EsContract.ContractId].Returns(42);
        api.StopStreamingFuturesTickData(42).Returns(false);
        var actor = CreateActor(marketDataApi: api);
        var context = Substitute.For<IEventActorContext>();

        await actor.InvokeReceiveAsync(context, CreateStreamingStoppedEvent());

        await context.DidNotReceive().SendAsync<FuturesTickDataStreamingStoppedCompleteEvent, FuturesTickDataStreamingId>(
            Arg.Any<FuturesTickDataStreamingStoppedCompleteEvent>());
        streamIds.DidNotReceive().Remove(Arg.Any<int>());
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
    public async Task ReceiveAsync_UnsupportedEvent_ThrowsInvalidOperationException()
    {
        var actor = CreateActor();
        var @event = Substitute.For<IEvent>();
        @event.Subject.Returns(new ActorSubject(
            ActorType.Event, FuturesTickDataEventActor.Actor, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IEventActorContext>(), @event).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTickDataEventActor.Actor} event from message: *");
    }

    [Fact]
    public async Task OnExceptionAsync_ValidInputs_SendsEventExceptionEvent()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();

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
        var @event = CreateInsertedEvent();
        var attempts = 0;
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(_ =>
            {
                attempts++;
                if (attempts == 1)
                    throw new InvalidOperationException("first publish failed");
                return ValueTask.CompletedTask;
            });

        await actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new Exception("original failure"));

        attempts.Should().Be(2);
    }

    [Fact]
    public async Task OnExceptionAsync_DefaultThreadOrNullEvent_AreConvertedToErrorEvents()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();

        await actor.InvokeOnExceptionAsync(context, default, @event, new Exception("failure"));
        await actor.InvokeOnExceptionAsync(context, @event.Subject.ThreadId, null!, new Exception("failure"));

        await context.Received(2).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    TestableFuturesTickDataEventActor CreateActor(
        IActorSupervisor? supervisor = null,
        IMarketDataApi? marketDataApi = null,
        IBlackboardService? blackboard = null,
        IStatusConsoleWriter? statusConsole = null,
        ILogger<FuturesTickDataEventActor>? logger = null)
        => _fixture.CreateActor(
            supervisor ?? Substitute.For<IActorSupervisor>(),
            marketDataApi ?? CreateMarketDataApi(),
            blackboard ?? CreateBlackboard().Blackboard,
            statusConsole ?? Substitute.For<IStatusConsoleWriter>(),
            logger ?? Substitute.For<ILogger<FuturesTickDataEventActor>>());

    static IMarketDataApi CreateMarketDataApi()
    {
        var api = Substitute.For<IMarketDataApi>();
        api.StreamIds.Returns(Substitute.For<IStreamIdCollection>());
        return api;
    }

    static (IBlackboardService Blackboard, IRedisCache Redis) CreateBlackboard()
    {
        var blackboard = Substitute.For<IBlackboardService>();
        var redis = Substitute.For<IRedisCache>();
        var serializer = Substitute.For<IJsonSerializer>();
        blackboard.FuturesTickDataStreamingParameter.Returns(
            new FuturesTickDataStreamingParameterModel(redis));
        blackboard.VixFuturesContractId.Returns(
            new VixFuturesContractIdModel(redis, serializer));
        return (blackboard, redis);
    }

    public static IEnumerable<object[]> SupportedEvents()
    {
        yield return [CreateInsertedEvent()];
        yield return [CreateStreamingStartedEvent()];
        yield return [CreateStreamingStoppedEvent()];
    }

    static NatsMsg<byte[]> CreateMessage(IEvent @event)
        => new() { Subject = @event.Subject.ToString(), Data = Serialize(@event) };

    static byte[] Serialize(IEvent @event) => @event switch
    {
        FuturesTickDataInsertedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesTickDataStreamingStartedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        FuturesTickDataStreamingStoppedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(@event))
    };

    static FuturesTickDataInsertedEvent CreateInsertedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesTickDataId(SampleData.EsTickData.ContractId, SampleData.ValueDate, SampleData.EsTickData.TickId);
        return new FuturesTickDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickDataEventActor.Actor, FuturesTickDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.EsContract,
            TickData = SampleData.EsTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static FuturesTickDataInsertedEvent CreateVixInsertedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesTickDataId(
            SampleData.VixTickData.ContractId,
            SampleData.ValueDate,
            SampleData.VixTickData.TickId);
        return new FuturesTickDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesTickDataEventActor.Actor,
                FuturesTickDataInsertedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 4,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.EsContract with
            {
                ContractId = SampleData.VixTickData.ContractId,
                Symbol = "VX",
                LocalSymbol = "VXM4"
            },
            TickData = SampleData.VixTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static FuturesTickDataStreamingStartedEvent CreateStreamingStartedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesTickDataStreamingId(SampleData.ValueDate);
        return new FuturesTickDataStreamingStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickDataEventActor.Actor, FuturesTickDataStreamingStartedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.EsContract,
            ValueDate = SampleData.ValueDate,
            ResetStream = false,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesTickDataStreamingStoppedEvent CreateStreamingStoppedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesTickDataStreamingId(SampleData.ValueDate);
        return new FuturesTickDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickDataEventActor.Actor, FuturesTickDataStreamingStoppedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            ContractId = SampleData.EsContract.ContractId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

}
