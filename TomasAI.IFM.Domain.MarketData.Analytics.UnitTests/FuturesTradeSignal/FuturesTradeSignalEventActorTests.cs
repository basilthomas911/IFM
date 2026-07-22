using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal;

public class FuturesTradeSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTradeSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public sealed class TestableFuturesTradeSignalEventActor(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesTradeSignalEventActor> logger)
        : FuturesTradeSignalEventActor(supervisor, statusConsoleWriter, logger)
    {
        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => ReceiveAsync(context, @event);

        public ValueTask InvokeOnExceptionAsync(
            IEventActorContext context,
            ActorThreadId threadId,
            IEvent @event,
            Exception exception)
            => OnExceptionAsync(context, threadId, @event, exception);
    }

    sealed record Scenario(
        TestableFuturesTradeSignalEventActor Actor,
        IActorSupervisor Supervisor,
        IStatusConsoleWriter StatusConsoleWriter,
        ILogger<FuturesTradeSignalEventActor> Logger,
        IEventActorContext Context);

    Scenario CreateScenario()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var statusConsoleWriter = Substitute.For<IStatusConsoleWriter>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalEventActor>>();
        var actor = _fixture.CreateActor(supervisor, statusConsoleWriter, logger);
        var context = Substitute.For<IEventActorContext>();
        return new Scenario(actor, supervisor, statusConsoleWriter, logger, context);
    }

    NatsMsg<byte[]> CreateMessage<TEvent>(
        TEvent @event,
        byte[]? payload = null,
        string? subject = null)
        where TEvent : class, IEvent
        => new()
        {
            Subject = subject ?? @event.Subject.ToString(),
            Data = payload ?? _fixture.DataSerializer.Serialize(@event)
        };

    // ParseMessage

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_CompletionEvent_PreservesPeriodAndIdentity(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.CreateTradeSignalUpdatedCompleteEventFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<FuturesTradeSignalUpdatedCompleteEvent>().Subject;
        parsed.Id.Should().Be(expected.Id);
        parsed.CommandId.Should().Be(expected.CommandId);
        parsed.EntityId.Should().Be(expected.EntityId);
        parsed.FuturesTradeSignal.Should().NotBeNull();
        parsed.FuturesTradeSignal!.TimePeriod.Should().Be(timePeriod);
        parsed.FuturesTradeSignal.Should().BeEquivalentTo(expected.FuturesTradeSignal);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_HoldTradeChangedEvent_PreservesPeriodAndPayload(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.CreateHoldTradeChangedEventFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<FuturesItiSignalHoldTradeChangedEvent>().Subject;
        parsed.Id.Should().Be(expected.Id);
        parsed.CommandId.Should().Be(expected.CommandId);
        parsed.EntityId.Should().Be(expected.EntityId);
        parsed.FuturesItiSignalId.Should().Be(expected.FuturesItiSignalId);
        parsed.FuturesItiSignalId.TimePeriod.Should().Be(timePeriod);
        parsed.HoldTrade.Should().BeTrue();
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(null!, CreateMessage(@event));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Command", FuturesTradeSignalEventActor.Actor, FuturesTradeSignalUpdatedCompleteEvent.Verb)]
    [InlineData("Event", "WrongTradeSignalEventActor", FuturesTradeSignalUpdatedCompleteEvent.Verb)]
    [InlineData("Event", FuturesTradeSignalEventActor.Actor, "UnknownVerb")]
    public void ParseMessage_UnroutableSubject_ReturnsNull(
        string actorType,
        string actorName,
        string verb)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(TradeTimePeriodType.Weekly);
        var subject = $"{actorType}.{actorName}.{verb}.{@event.EntityId.Format()}";

        var result = scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, subject: subject));

        result.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_EmptyCommandId_RejectsEvent(TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(
            timePeriod,
            commandId: Guid.Empty);

        var act = () => scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(@event));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public void ParseMessage_HoldTradeChangedEventWithEmptyCommandId_RejectsEvent()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateHoldTradeChangedEventFor(
            TradeTimePeriodType.Monthly,
            commandId: Guid.Empty);

        var act = () => scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(@event));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_CorruptedPayload_ThrowsDeserializationException(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(timePeriod);
        byte[] corruptedPayload = [0x00, 0x01, 0x02, 0xff, 0xfe];

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, corruptedPayload));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_EmptyPayload_ThrowsDeserializationException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateHoldTradeChangedEventFor(TradeTimePeriodType.Monthly);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, []));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_MalformedSubject_ThrowsArgumentException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, subject: "invalid-subject"));

        act.Should().Throw<ArgumentException>();
    }

    // ReceiveAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_CompletionEvent_ProcessesEveryPeriod(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(timePeriod);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, @event);

        await act.Should().NotThrowAsync();
        @event.FuturesTradeSignal!.TimePeriod.Should().Be(timePeriod);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_HoldTradeChangedEvent_ProcessesEveryPeriod(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateHoldTradeChangedEventFor(timePeriod, holdTrade: false);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, @event);

        await act.Should().NotThrowAsync();
        @event.FuturesItiSignalId.TimePeriod.Should().Be(timePeriod);
        @event.HoldTrade.Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEvent();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(null!, @event);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_NullEvent_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedEvent_ThrowsInvalidOperationException()
    {
        var scenario = CreateScenario();
        var @event = Substitute.For<IEvent>();
        @event.Subject.Returns(new ActorSubject(
            ActorType.Event,
            FuturesTradeSignalEventActor.Actor,
            "Unsupported",
            SampleData.TradeSignalEntityIdFor(TradeTimePeriodType.Weekly).Format()));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, @event);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalEventActor.Actor} event from message:*");
    }

    // OnExceptionAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_CompletionEvent_PublishesTypedError(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} completion failed");
        scenario.Context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            @event.Subject.ThreadId,
            @event,
            exception);

        await scenario.Context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(error =>
                    error.ErrorType == ErrorType.EventService
                    && error.ErrorMessage == exception.Message
                    && error.ErrorData.Contains(nameof(InvalidOperationException))));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_HoldTradeChangedEvent_PublishesTypedError(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateHoldTradeChangedEventFor(timePeriod);
        var exception = new TimeoutException($"{timePeriod} hold update timed out");
        scenario.Context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            @event.Subject.ThreadId,
            @event,
            exception);

        await scenario.Context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(error =>
                    error.ErrorType == ErrorType.EventService
                    && error.ErrorMessage == exception.Message
                    && error.ErrorData.Contains(nameof(TimeoutException))));
    }

    [Fact]
    public async Task OnExceptionAsync_FirstErrorPublicationFails_PublishesFallback()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(TradeTimePeriodType.Monthly);
        scenario.Context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(
                _ => throw new InvalidOperationException("primary publication failed"),
                _ => ValueTask.CompletedTask);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            @event.Subject.ThreadId,
            @event,
            new TimeoutException("original event failure"));

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(2)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
        scenario.Logger.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task OnExceptionAsync_NullEvent_PublishesValidationFailureAndLogsError()
    {
        var scenario = CreateScenario();
        var threadId = new ActorThreadId(
            ActorType.Event,
            FuturesTradeSignalEventActor.Actor,
            "null-event");
        scenario.Context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            threadId,
            null!,
            new Exception("original event failure"));

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(error =>
                    error.ErrorType == ErrorType.EventService
                    && error.ErrorData.Contains(nameof(ArgumentNullException))));
        scenario.Logger.ReceivedCalls()
            .Should().ContainSingle(call =>
                call.GetMethodInfo().Name == nameof(ILogger.Log)
                && (LogLevel)call.GetArguments()[0]! == LogLevel.Error);
    }

    [Fact]
    public async Task OnExceptionAsync_EmptyThreadId_PublishesValidationFailureAndLogsError()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateHoldTradeChangedEventFor(TradeTimePeriodType.Weekly);
        scenario.Context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            default,
            @event,
            new Exception("original event failure"));

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(error =>
                    error.ErrorData.Contains(nameof(ArgumentNullException))));
    }

    [Fact]
    public async Task OnExceptionAsync_NullException_PublishesFallbackError()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(TradeTimePeriodType.Daily);
        scenario.Context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            @event.Subject.ThreadId,
            @event,
            null!);

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    [Fact]
    public async Task OnExceptionAsync_NullContext_ContainsValidationFailure()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTradeSignalUpdatedCompleteEventFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            null!,
            @event.Subject.ThreadId,
            @event,
            new Exception("original event failure"));

        await act.Should().NotThrowAsync();
    }
}
