using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData;

public class FuturesEodDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesEodDataEventActorTests(MarketDataFeedTestFixture fixture) => _fixture = fixture;

    public class TestableFuturesEodDataEventActor(
        IActorSupervisor supervisor,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesEodDataEventActor> logger)
        : FuturesEodDataEventActor(supervisor, blackboardService, statusConsoleWriter, logger)
    {
        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => ReceiveAsync(context, @event);

        public ValueTask InvokeOnExceptionAsync(
            IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception exception)
            => OnExceptionAsync(context, threadId, @event, exception);
    }

    public static IEnumerable<object[]> SupportedEvents()
    {
        yield return [CreateFuturesEodDataInsertedEvent()];
        yield return [CreateVixFuturesEodDataInsertedCompleteEvent()];
    }

    [Theory]
    [MemberData(nameof(SupportedEvents))]
    public void ParseMessage_ValidSupportedEvent_ReturnsConcreteEvent(IEvent @event)
    {
        var harness = CreateHarness();

        var parsed = harness.Actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        parsed.GetType().Should().Be(@event.GetType());
        parsed.CommandId.Should().Be(@event.CommandId);
        parsed.Subject.Should().Be(@event.Subject);
    }

    [Fact]
    public void ParseMessage_InsertedEvent_PreservesSampleEodData()
    {
        var harness = CreateHarness();
        var @event = CreateFuturesEodDataInsertedEvent();

        var parsed = harness.Actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        parsed.Should().BeOfType<FuturesEodDataInsertedEvent>()
            .Which.FuturesEodData.Should().BeEquivalentTo(SampleData.EodDataToday);
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesEodDataEventActor.Actor, FuturesEodDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, "WrongActor", FuturesEodDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, FuturesEodDataEventActor.Actor, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ReturnsNull(
        ActorType actorType, string actorName, string verb)
    {
        var harness = CreateHarness();
        var @event = CreateFuturesEodDataInsertedEvent();
        var subject = new ActorSubject(actorType, actorName, verb, @event.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(@event) };

        var parsed = harness.Actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), message);

        parsed.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidPayload_Throws(bool emptyPayload)
    {
        var harness = CreateHarness();
        var @event = CreateFuturesEodDataInsertedEvent();
        var message = new NatsMsg<byte[]>
        {
            Subject = @event.Subject.ToString(),
            Data = emptyPayload ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => harness.Actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_EmptyCommandId_Throws()
    {
        var harness = CreateHarness();

        Action act = () => harness.Actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(),
            CreateMessage(CreateFuturesEodDataInsertedEvent(Guid.Empty)));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var harness = CreateHarness();

        Action act = () => harness.Actor.InvokeParseMessage(
            null!, CreateMessage(CreateFuturesEodDataInsertedEvent()));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_InsertedEvent_CachesDataAndSendsUpdatedEvent()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateFuturesEodDataInsertedEvent();
        context.SendAsync<FuturesEodDataUpdatedEvent, FuturesEodDataId>(
                Arg.Any<FuturesEodDataUpdatedEvent>())
            .Returns(ValueTask.CompletedTask);

        await harness.Actor.InvokeReceiveAsync(context, @event);

        harness.JsonSerializer.Received(1).Serialize(@event.FuturesEodData);
        harness.RedisCache.Received(1).Set(
            Arg.Is<string>(key => key.Contains(@event.FuturesEodData.ContractId)), "serialized");
        await context.Received(1).SendAsync<FuturesEodDataUpdatedEvent, FuturesEodDataId>(
            Arg.Is<FuturesEodDataUpdatedEvent>(value =>
                value.CommandId == @event.CommandId &&
                value.EntityId == @event.EntityId &&
                value.FuturesEodData == @event.FuturesEodData));
    }

    [Fact]
    public async Task ReceiveAsync_InsertedEventCacheFailure_WritesStatusAndDoesNotSendUpdate()
    {
        var harness = CreateHarness();
        harness.JsonSerializer.Serialize(Arg.Any<object>())
            .Returns(_ => throw new InvalidOperationException("cache failed"));
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateFuturesEodDataInsertedEvent();

        Func<Task> act = () => harness.Actor.InvokeReceiveAsync(context, @event).AsTask();

        await act.Should().NotThrowAsync();
        await harness.StatusConsole.Received(1).WriteConsoleAsync(
            LogSourceType.FuturesEodDataEvent,
            FuturesEodDataInsertedEvent.ErrorCode,
            Arg.Is<string>(message => message.Contains("cache failed")),
            Arg.Any<string>(), Arg.Any<string>());
        await context.DidNotReceive().SendAsync<FuturesEodDataUpdatedEvent, FuturesEodDataId>(
            Arg.Any<FuturesEodDataUpdatedEvent>());
    }

    [Fact]
    public async Task ReceiveAsync_UpdatedEventPublishFailure_WritesStatus()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateFuturesEodDataInsertedEvent();
        context.SendAsync<FuturesEodDataUpdatedEvent, FuturesEodDataId>(
                Arg.Any<FuturesEodDataUpdatedEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("update publish failed"));

        await harness.Actor.InvokeReceiveAsync(context, @event);

        await harness.StatusConsole.Received(1).WriteConsoleAsync(
            LogSourceType.FuturesEodDataEvent,
            FuturesEodDataInsertedEvent.ErrorCode,
            Arg.Is<string>(message => message.Contains("update publish failed")),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReceiveAsync_VixCompleteEvent_QueriesCachesAndReportsValue()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateVixFuturesEodDataInsertedCompleteEvent();
        context.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(
                Arg.Any<GetVixFuturesEodDataQuery>())
            .Returns(new ServiceResult<VixFuturesEodDataReadModel[]>(SampleData.VixEodData));

        await harness.Actor.InvokeReceiveAsync(context, @event);

        await context.Received(1).RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(
            Arg.Is<GetVixFuturesEodDataQuery>(query =>
                query.ContractId == @event.VixFuturesTickData.ContractId &&
                query.ValueDate == @event.VixFuturesTickData.ValueDate));
        harness.JsonSerializer.Received(1).Serialize(
            Arg.Is<object>(value => ReferenceEquals(value, SampleData.VixEodData)));
        harness.RedisCache.Received(1).Set(
            Arg.Is<string>(key => key.Contains(@event.VixFuturesTickData.ContractId)), "serialized");
        await harness.StatusConsole.Received(1).WriteConsoleAsync(
            LogSourceType.FuturesEodDataEvent,
            Arg.Is<string>(message => message.Contains("cached")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReceiveAsync_VixQueryWithoutData_DoesNotCacheOrWriteSuccess(bool failedResult)
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateVixFuturesEodDataInsertedCompleteEvent();
        var result = failedResult
            ? new ServiceResult<VixFuturesEodDataReadModel[]>(5005, "query failed")
            : new ServiceResult<VixFuturesEodDataReadModel[]>(Array.Empty<VixFuturesEodDataReadModel>());
        context.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(
                Arg.Any<GetVixFuturesEodDataQuery>())
            .Returns(result);

        await harness.Actor.InvokeReceiveAsync(context, @event);

        harness.RedisCache.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
        await harness.StatusConsole.DidNotReceive().WriteConsoleAsync(
            LogSourceType.FuturesEodDataEvent, Arg.Any<string>());
    }

    [Fact]
    public async Task ReceiveAsync_VixQueryFailure_WritesFailureStatus()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateVixFuturesEodDataInsertedCompleteEvent();
        context.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(
                Arg.Any<GetVixFuturesEodDataQuery>())
            .Returns<ValueTask<ServiceResult<VixFuturesEodDataReadModel[]>>>(
                _ => throw new TimeoutException("VIX query timed out"));

        await harness.Actor.InvokeReceiveAsync(context, @event);

        await harness.StatusConsole.Received(1).WriteConsoleAsync(
            LogSourceType.MarketDataFeedEvent,
            6009,
            Arg.Is<string>(message => message.Contains("VIX query timed out")),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReceiveAsync_NullInputs_ThrowArgumentNullException()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateFuturesEodDataInsertedEvent();

        await ((Func<Task>)(() => harness.Actor.InvokeReceiveAsync(null!, @event).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => harness.Actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedEvent_ThrowsInvalidOperationException()
    {
        var harness = CreateHarness();
        var @event = Substitute.For<IEvent>();
        @event.Subject.Returns(new ActorSubject(
            ActorType.Event, FuturesEodDataEventActor.Actor, "Unknown", "entity"));

        Func<Task> act = () => harness.Actor.InvokeReceiveAsync(
            Substitute.For<IEventActorContext>(), @event).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataEventActor.Actor} event from message: *");
    }

    [Fact]
    public async Task OnExceptionAsync_ValidInputs_SendsEventExceptionEvent()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateFuturesEodDataInsertedEvent();

        await harness.Actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new InvalidOperationException("event failed"));

        await context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(value =>
                    value.ErrorMessage == "event failed" && value.ErrorType == ErrorType.EventService));
    }

    [Fact]
    public async Task OnExceptionAsync_FirstPublishFails_RetriesWithSecondaryFailure()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateVixFuturesEodDataInsertedCompleteEvent();
        var callCount = 0;
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(_ =>
            {
                if (++callCount == 1)
                    throw new InvalidOperationException("first publish failed");
                return ValueTask.CompletedTask;
            });

        await harness.Actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new Exception("original failure"));

        await context.Received(2)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    [Fact]
    public async Task OnExceptionAsync_BothPublishesFail_DoesNotLeakPublishingFailure()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateFuturesEodDataInsertedEvent();
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("publish failed"));

        Func<Task> act = () => harness.Actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new Exception("original failure")).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_InvalidThreadOrEvent_IsConvertedToEventException()
    {
        var harness = CreateHarness();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateFuturesEodDataInsertedEvent();

        await harness.Actor.InvokeOnExceptionAsync(
            context, default, @event, new Exception("failure"));
        await harness.Actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, null!, new Exception("failure"));

        await context.Received(2)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    ActorHarness CreateHarness()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var blackboard = Substitute.For<IBlackboardService>();
        var statusConsole = Substitute.For<IStatusConsoleWriter>();
        var logger = Substitute.For<ILogger<FuturesEodDataEventActor>>();
        var redisCache = Substitute.For<IRedisCache>();
        var jsonSerializer = Substitute.For<IJsonSerializer>();
        jsonSerializer.Serialize(Arg.Any<object>()).Returns("serialized");
        blackboard.FuturesEodData.Returns(new FuturesEodDataModel(redisCache, jsonSerializer));
        blackboard.VixFuturesEodData.Returns(new VixFuturesEodDataModel(redisCache, jsonSerializer));
        var actor = _fixture.CreateActor(supervisor, blackboard, statusConsole, logger);
        return new ActorHarness(actor, redisCache, jsonSerializer, statusConsole);
    }

    sealed record ActorHarness(
        TestableFuturesEodDataEventActor Actor,
        IRedisCache RedisCache,
        IJsonSerializer JsonSerializer,
        IStatusConsoleWriter StatusConsole);

    static FuturesEodDataInsertedEvent CreateFuturesEodDataInsertedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FuturesEodDataEntityId;
        return new FuturesEodDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event, FuturesEodDataEventActor.Actor,
                FuturesEodDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesEodData = SampleData.EodDataToday,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static VixFuturesEodDataInsertedCompleteEvent CreateVixFuturesEodDataInsertedCompleteEvent(
        Guid? commandId = null)
    {
        var entityId = SampleData.VixFuturesEodDataEntityId;
        return new VixFuturesEodDataInsertedCompleteEvent
        {
            Subject = new ActorSubject(
                ActorType.Event, FuturesEodDataEventActor.Actor,
                VixFuturesEodDataInsertedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            VixFuturesTickData = SampleData.VixTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static NatsMsg<byte[]> CreateMessage(IEvent @event)
        => new() { Subject = @event.Subject.ToString(), Data = Serialize(@event) };

    static byte[] Serialize(IEvent @event) => @event switch
    {
        FuturesEodDataInsertedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        VixFuturesEodDataInsertedCompleteEvent value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(@event))
    };
}
