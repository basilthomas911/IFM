using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData;

public class FuturesBarDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesBarDataEventActorTests(MarketDataFeedTestFixture fixture) => _fixture = fixture;

    public class TestableFuturesBarDataEventActor(
        IActorSupervisor supervisor,
        IFuturesBarDataTimer futuresBarTimer,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesBarDataEventActor> logger)
        : FuturesBarDataEventActor(supervisor, futuresBarTimer, statusConsoleWriter, logger)
    {
        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => ReceiveAsync(context, @event);

        public ValueTask InvokeOnExceptionAsync(
            IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception exception)
            => OnExceptionAsync(context, threadId, @event, exception);
    }

    [Theory]
    [MemberData(nameof(ValidEvents))]
    public void ParseMessage_ValidSupportedEvent_ReturnsConcreteEvent(IEvent @event)
    {
        var actor = CreateActor();

        var result = actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        result.GetType().Should().Be(@event.GetType());
        result.CommandId.Should().Be(@event.CommandId);
        result.Subject.Should().Be(@event.Subject);
    }

    [Fact]
    public void ParseMessage_InsertedEvent_PreservesSampleBarData()
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent();

        var result = actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        var parsed = result.Should().BeOfType<FuturesBarDataInsertedEvent>().Which;
        parsed.FuturesBarData.Should().BeEquivalentTo(SampleData.FuturesBarData1);
        parsed.FuturesBarData.BarRateType.Should().Be(BarRateType.Minute);
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesBarDataEventActor.Actor, FuturesBarDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, "WrongActor", FuturesBarDataInsertedEvent.Verb)]
    [InlineData(ActorType.Event, FuturesBarDataEventActor.Actor, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ReturnsNull(
        ActorType actorType, string actorName, string verb)
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent();
        var subject = new ActorSubject(actorType, actorName, verb, @event.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(@event) };

        var result = actor.InvokeParseMessage(Substitute.For<IEventActorContext>(), message);

        result.Should().BeNull();
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
    public void ParseMessage_InvalidPayload_Throws(bool useEmptyPayload)
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent();
        var message = new NatsMsg<byte[]>
        {
            Subject = @event.Subject.ToString(),
            Data = useEmptyPayload ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IEventActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_EmptyCommandId_Throws()
    {
        var actor = CreateActor();
        var @event = CreateInsertedEvent(Guid.Empty);

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<IEventActorContext>(), CreateMessage(@event));

        act.Should().Throw<Exception>();
    }

    [Theory]
    [MemberData(nameof(NoOpEvents))]
    public async Task ReceiveAsync_InsertedOrDeletedEvent_CompletesWithoutSideEffects(IEvent @event)
    {
        var timer = Substitute.For<IFuturesBarDataTimer>();
        var actor = CreateActor(timer: timer);

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IEventActorContext>(), @event).AsTask();

        await act.Should().NotThrowAsync();
        timer.DidNotReceive().Start(Arg.Any<Action>());
        timer.DidNotReceive().Stop();
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStarted_StartsTimerAndSendsCompleteEvent()
    {
        var timer = Substitute.For<IFuturesBarDataTimer>();
        var actor = CreateActor(timer: timer);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent();
        context.SendAsync<FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingId>(
                Arg.Any<FuturesBarDataStreamingStartedCompleteEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeReceiveAsync(context, @event);

        timer.Received(1).Start(Arg.Any<Action>());
        await context.Received(1)
            .SendAsync<FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingId>(
                Arg.Is<FuturesBarDataStreamingStartedCompleteEvent>(value =>
                    value.CommandId == @event.CommandId && value.EntityId == @event.EntityId));
    }

    [Fact]
    public async Task ReceiveAsync_StreamingStopped_StopsTimerAndSendsCompleteEvent()
    {
        var timer = Substitute.For<IFuturesBarDataTimer>();
        var actor = CreateActor(timer: timer);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStoppedEvent();
        context.SendAsync<FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingId>(
                Arg.Any<FuturesBarDataStreamingStoppedCompleteEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeReceiveAsync(context, @event);

        timer.Received(1).Stop();
        await context.Received(1)
            .SendAsync<FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingId>(
                Arg.Is<FuturesBarDataStreamingStoppedCompleteEvent>(value =>
                    value.CommandId == @event.CommandId && value.EntityId == @event.EntityId));
    }

    [Fact]
    public async Task ReceiveAsync_TimerStartFails_SendsStreamingStartedFailEvent()
    {
        var timer = Substitute.For<IFuturesBarDataTimer>();
        timer.When(value => value.Start(Arg.Any<Action>()))
            .Do(_ => throw new InvalidOperationException("timer start failed"));
        var actor = CreateActor(timer: timer);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStartedEvent();
        context.SendAsync<FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(
                Arg.Any<FuturesBarDataStreamingStartedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeReceiveAsync(context, @event);

        await context.Received(1)
            .SendAsync<FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(
                Arg.Is<FuturesBarDataStreamingStartedFailEvent>(value =>
                    value.ErrorMessage == "timer start failed"));
    }

    [Fact]
    public async Task ReceiveAsync_TimerStopFails_SendsStreamingStoppedFailEvent()
    {
        var timer = Substitute.For<IFuturesBarDataTimer>();
        timer.When(value => value.Stop())
            .Do(_ => throw new InvalidOperationException("timer stop failed"));
        var actor = CreateActor(timer: timer);
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateStreamingStoppedEvent();
        context.SendAsync<FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(
                Arg.Any<FuturesBarDataStreamingStoppedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeReceiveAsync(context, @event);

        await context.Received(1)
            .SendAsync<FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(
                Arg.Is<FuturesBarDataStreamingStoppedFailEvent>(value =>
                    value.ErrorMessage == "timer stop failed"));
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
            ActorType.Event, FuturesBarDataEventActor.Actor, "Unknown", "entity"));

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
    public async Task OnExceptionAsync_FirstPublishFails_RetriesWithSecondaryException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateDeletedEvent();
        var callCount = 0;
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
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
    public async Task OnExceptionAsync_BothPublishesFail_DoesNotLeakPublishingFailure()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateDeletedEvent();
        context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("publish failed"));

        Func<Task> act = () => actor.InvokeOnExceptionAsync(
            context, @event.Subject.ThreadId, @event, new Exception("original failure")).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateInsertedEvent();
        var exception = new Exception("failure");

        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                null!, @event.Subject.ThreadId, @event, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, default, @event, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, @event.Subject.ThreadId, null!, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    TestableFuturesBarDataEventActor CreateActor(
        IActorSupervisor? supervisor = null,
        IFuturesBarDataTimer? timer = null,
        IStatusConsoleWriter? statusConsoleWriter = null,
        ILogger<FuturesBarDataEventActor>? logger = null)
        => _fixture.CreateActor(
            supervisor ?? Substitute.For<IActorSupervisor>(),
            timer ?? Substitute.For<IFuturesBarDataTimer>(),
            statusConsoleWriter ?? Substitute.For<IStatusConsoleWriter>(),
            logger ?? Substitute.For<ILogger<FuturesBarDataEventActor>>());

    public static IEnumerable<object[]> ValidEvents()
    {
        yield return [CreateStreamingStartedEvent()];
        yield return [CreateStreamingStoppedEvent()];
        yield return [CreateInsertedEvent()];
        yield return [CreateDeletedEvent()];
    }

    public static IEnumerable<object[]> NoOpEvents()
    {
        yield return [CreateInsertedEvent()];
        yield return [CreateDeletedEvent()];
    }

    static FuturesBarDataStreamingStartedEvent CreateStreamingStartedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        return new FuturesBarDataStreamingStartedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event, FuturesBarDataEventActor.Actor,
                FuturesBarDataStreamingStartedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            AggregateId = entityId.Format(),
            ReceivedOn = DateTime.UtcNow,
            EventSource = "UnitTest",
            Contracts = SampleData.FuturesContracts,
            ValueDate = SampleData.ValueDate,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesBarDataStreamingStoppedEvent CreateStreamingStoppedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        return new FuturesBarDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event, FuturesBarDataEventActor.Actor,
                FuturesBarDataStreamingStoppedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            AggregateId = entityId.Format(),
            ReceivedOn = DateTime.UtcNow,
            EventSource = "UnitTest",
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

    static FuturesBarDataInsertedEvent CreateInsertedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FuturesBarDataId1;
        return new FuturesBarDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event, FuturesBarDataEventActor.Actor,
                FuturesBarDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            AggregateId = entityId.Format(),
            ReceivedOn = DateTime.UtcNow,
            EventSource = "UnitTest",
            FuturesBarData = SampleData.FuturesBarData1,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static FuturesBarDataDeletedEvent CreateDeletedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FuturesBarDataId1;
        return new FuturesBarDataDeletedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event, FuturesBarDataEventActor.Actor,
                FuturesBarDataDeletedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 4,
            AggregateId = entityId.Format(),
            ReceivedOn = DateTime.UtcNow,
            EventSource = "UnitTest",
            BarDataId = entityId,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static NatsMsg<byte[]> CreateMessage(IEvent @event)
        => new() { Subject = @event.Subject.ToString(), Data = Serialize(@event) };

    static byte[] Serialize(IEvent @event)
        => @event switch
        {
            FuturesBarDataStreamingStartedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
            FuturesBarDataStreamingStoppedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
            FuturesBarDataInsertedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
            FuturesBarDataDeletedEvent value => ActorExtensions.DataSerializer!.Serialize(value),
            _ => throw new ArgumentOutOfRangeException(nameof(@event))
        };
}
