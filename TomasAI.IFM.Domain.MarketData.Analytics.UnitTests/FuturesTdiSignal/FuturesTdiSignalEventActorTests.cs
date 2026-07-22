using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public class FuturesTdiSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTdiSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public sealed class TestableFuturesTdiSignalEventActor(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesTdiSignalEventActor> logger)
        : FuturesTdiSignalEventActor(supervisor, statusConsoleWriter, logger)
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
        TestableFuturesTdiSignalEventActor Actor,
        IActorSupervisor Supervisor,
        IStatusConsoleWriter StatusConsoleWriter,
        ILogger<FuturesTdiSignalEventActor> Logger,
        IEventActorContext Context);

    Scenario CreateScenario()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var statusConsoleWriter = Substitute.For<IStatusConsoleWriter>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalEventActor>>();
        var actor = _fixture.CreateActor(supervisor, statusConsoleWriter, logger);
        var context = Substitute.For<IEventActorContext>();
        return new Scenario(actor, supervisor, statusConsoleWriter, logger, context);
    }

    NatsMsg<byte[]> CreateMessage(
        FuturesTdiSignalGeneratedCompleteEvent @event,
        byte[]? payload = null,
        string? subject = null)
        => new()
        {
            Subject = subject ?? @event.Subject.ToString(),
            Data = payload ?? _fixture.DataSerializer.Serialize(@event)
        };

    // ParseMessage

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_GivenValidCompletionEvent_WhenParsed_ThenPreservesPeriodAndIdentity(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.CreateTdiSignalGeneratedCompleteEventFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<FuturesTdiSignalGeneratedCompleteEvent>().Subject;
        parsed.Id.Should().Be(expected.Id);
        parsed.CommandId.Should().Be(expected.CommandId);
        parsed.EntityId.Should().Be(expected.EntityId);
        parsed.FuturesTdiSignal.TimePeriod.Should().Be(timePeriod);
        parsed.FuturesTdiSignal.Should().BeEquivalentTo(expected.FuturesTdiSignal);
    }

    [Fact]
    public void ParseMessage_GivenNullContext_WhenParsed_ThenThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEvent();

        var act = () => scenario.Actor.InvokeParseMessage(null!, CreateMessage(@event));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Command", "FuturesTdiSignalEvent", "FuturesTdiSignalGeneratedComplete")]
    [InlineData("Event", "WrongTdiEventActor", "FuturesTdiSignalGeneratedComplete")]
    [InlineData("Event", "FuturesTdiSignalEvent", "UnknownVerb")]
    public void ParseMessage_GivenUnroutableSubject_WhenParsed_ThenReturnsNull(
        string actorType,
        string actorName,
        string verb)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(TradeTimePeriodType.Weekly);
        var subject = $"{actorType}.{actorName}.{verb}.{@event.EntityId.Format()}";

        var result = scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, subject: subject));

        result.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_GivenEmptyCommandId_WhenParsed_ThenRejectsEvent(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(
            timePeriod,
            commandId: Guid.Empty);

        var act = () => scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(@event));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_GivenCorruptedPayload_WhenParsed_ThenThrowsDeserializationException(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(timePeriod);
        byte[] corruptedPayload = [0x00, 0x01, 0x02, 0xff, 0xfe];

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, corruptedPayload));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_GivenEmptyPayload_WhenParsed_ThenThrowsDeserializationException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(TradeTimePeriodType.Monthly);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, []));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_GivenMalformedSubject_WhenParsed_ThenThrowsArgumentException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(@event, subject: "invalid-subject"));

        act.Should().Throw<ArgumentException>();
    }

    // ReceiveAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_GivenCompletionEvent_WhenReceived_ThenProcessesPeriodSuccessfully(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(timePeriod);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, @event);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_GivenNullContext_WhenReceived_ThenThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEvent();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(null!, @event);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_GivenNullEvent_WhenReceived_ThenThrowsArgumentNullException()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_GivenUnsupportedGeneratedEvent_WhenReceived_ThenRejectsEvent(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedEventFor(timePeriod);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, @event);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalEventActor.Actor} event from message:*");
    }

    // OnExceptionAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_GivenProcessingFailure_WhenHandled_ThenPublishesTypedError(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} event processing failed");
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

    [Fact]
    public async Task OnExceptionAsync_GivenFirstErrorPublicationFails_WhenHandled_ThenPublishesFallback()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(TradeTimePeriodType.Monthly);
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
    public async Task OnExceptionAsync_GivenNullEvent_WhenHandled_ThenPublishesValidationFailure()
    {
        var scenario = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Event, FuturesTdiSignalEventActor.Actor, "null-event");
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
    public async Task OnExceptionAsync_GivenBothErrorPublicationsFail_WhenHandled_ThenContainsFailures()
    {
        var scenario = CreateScenario();
        var @event = SampleData.CreateTdiSignalGeneratedCompleteEventFor(TradeTimePeriodType.Weekly);
        scenario.Context.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(_ => throw new InvalidOperationException("publication unavailable"));

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            @event.Subject.ThreadId,
            @event,
            new Exception("original event failure"));

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(2)
            .SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
        scenario.Logger.ReceivedCalls().Should().BeEmpty();
    }
}
