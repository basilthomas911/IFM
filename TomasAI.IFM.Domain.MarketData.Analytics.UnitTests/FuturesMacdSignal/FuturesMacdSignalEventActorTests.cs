using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesMacdSignal;

public class FuturesMacdSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesMacdSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesMacdSignalEventActor : FuturesMacdSignalEventActor
    {
        public TestableFuturesMacdSignalEventActor(IActorSupervisor supervisor, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesMacdSignalEventActor> logger)
            : base(supervisor, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);

        public async ValueTask InvokeOnStartAsync(IEventActorContext context)
            => await OnStartup(context);

        public async ValueTask InvokeOnStopAsync(IEventActorContext context)
            => await OnShutdown(context);
    }

    public static IEnumerable<object[]> TimePeriods()
    {
        yield return new object[] { TradeTimePeriodType.Daily };
        yield return new object[] { TradeTimePeriodType.Weekly };
        yield return new object[] { TradeTimePeriodType.Monthly };
    }

    private static FuturesMacdSignalGeneratedCompleteEvent CreateMacdCompleteEvent(TradeTimePeriodType timePeriod, Guid? commandId = null)
    {
        var entityId = SampleData.MacdEntityId with { TimePeriod = timePeriod };
        return new FuturesMacdSignalGeneratedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalGeneratedCompleteEvent.Actor, FuturesMacdSignalGeneratedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesMacdSignal = new FuturesMacdSignalReadModel(
                contractId: SampleData.ContractId,
                valueDate: SampleData.ValueDate,
                timePeriod: timePeriod,
                periodLength: SampleData.PeriodLength,
                futuresPrice: (decimal)SampleData.FuturesPrice,
                timestamp: new TimeOnly(18, 50, 10),
                macdLine: 1.5,
                signalLine: 1.2,
                histogram: 0.3,
                macd: FuturesTrendDirectionType.UpTrending,
                macdStrength: FuturesTrendDirectionStrengthType.Medium),
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    #region ParseMessage Happy Path Tests

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public void ParseMessage_WithValidMacdSignalGeneratedCompleteEvent_ShouldReturnEvent(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(timePeriod);
        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
        var serializedData = _fixture.DataSerializer.Serialize(@event);
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(mockContext, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesMacdSignalGeneratedCompleteEvent>();
        var parsedEvent = (FuturesMacdSignalGeneratedCompleteEvent)result;
        parsedEvent.CommandId.Should().Be(@event.CommandId);
        parsedEvent.Id.Should().Be(@event.Id);
        parsedEvent.EntityId.ContractId.Should().Be(SampleData.ContractId);
        parsedEvent.EntityId.TimePeriod.Should().Be(timePeriod);
    }

    [Fact]
    public void ParseMessage_WithValidSubjectAndEventData_ShouldDeserializeCorrectly()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
        var serializedData = _fixture.DataSerializer.Serialize(@event);
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(mockContext, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesMacdSignalGeneratedCompleteEvent>();
        var parsedEvent = (FuturesMacdSignalGeneratedCompleteEvent)result;
        parsedEvent.CreatedBy.Should().Be("UnitTest");
        parsedEvent.FuturesMacdSignal.ContractId.Should().Be(SampleData.ContractId);
    }

    [Fact]
    public void ParseMessage_MultipleCallsWithSameData_ShouldProduceSameResult()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
        var serializedData = _fixture.DataSerializer.Serialize(@event);
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = serializedData
        };

        // Act
        var result1 = actor.InvokeParseMessage(mockContext, message);
        var result2 = actor.InvokeParseMessage(mockContext, message);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().BeOfType<FuturesMacdSignalGeneratedCompleteEvent>();
        result2.Should().BeOfType<FuturesMacdSignalGeneratedCompleteEvent>();

        var event1 = (FuturesMacdSignalGeneratedCompleteEvent)result1;
        var event2 = (FuturesMacdSignalGeneratedCompleteEvent)result2;

        event1.Id.Should().Be(event2.Id);
        event1.CommandId.Should().Be(event2.CommandId);
    }

    [Fact]
    public void ParseMessage_WithDifferentLoggerInstances_ShouldWorkConsistently()
    {
        // Arrange
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger1 = Substitute.For<ILogger<FuturesMacdSignalEventActor>>();
        var mockLogger2 = Substitute.For<ILogger<FuturesMacdSignalEventActor>>();

        var actor1 = _fixture.CreateMacdEventActor(mockSupervisor, logger: mockLogger1);
        var actor2 = _fixture.CreateMacdEventActor(mockSupervisor, logger: mockLogger2);

        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
        var serializedData = _fixture.DataSerializer.Serialize(@event);
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = serializedData
        };

        // Act
        var result1 = actor1.InvokeParseMessage(mockContext, message);
        var result2 = actor2.InvokeParseMessage(mockContext, message);

        // Assert
        result1.Should().BeOfType<FuturesMacdSignalGeneratedCompleteEvent>();
        result2.Should().BeOfType<FuturesMacdSignalGeneratedCompleteEvent>();
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var message = new NatsMsg<byte[]>
        {
            Subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}",
            Data = _fixture.DataSerializer.Serialize(@event)
        };

        // Act
        Action act = () => actor.InvokeParseMessage(null!, message);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_WithInvalidActorType_ShouldReturnNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var invalidSubject = $"Command.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.123";
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject,
            Data = _fixture.DataSerializer.Serialize(@event)
        };

        // Act
        var result = actor.InvokeParseMessage(mockContext, message);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_WithWrongActorName_ShouldReturnNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var invalidSubject = $"Event.WrongActor.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.123";
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject,
            Data = _fixture.DataSerializer.Serialize(@event)
        };

        // Act
        var result = actor.InvokeParseMessage(mockContext, message);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_WithUnknownVerb_ShouldReturnNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var invalidSubject = $"Event.{FuturesMacdSignalEventActor.Actor}.UnknownVerb.123";
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject,
            Data = _fixture.DataSerializer.Serialize(@event)
        };

        // Act
        var result = actor.InvokeParseMessage(mockContext, message);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_WithEmptyCommandId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily, commandId: Guid.Empty);

        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
        var serializedData = _fixture.DataSerializer.Serialize(@event);
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = serializedData
        };

        // Act
        Action act = () => actor.InvokeParseMessage(mockContext, message);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public void ParseMessage_WithMalformedSubject_ShouldThrowArgumentException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var malformedSubject = "InvalidSubjectFormat";
        var message = new NatsMsg<byte[]>
        {
            Subject = malformedSubject,
            Data = _fixture.DataSerializer.Serialize(CreateMacdCompleteEvent(TradeTimePeriodType.Daily))
        };

        // Act
        Action act = () => actor.InvokeParseMessage(mockContext, message);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid subject format*");
    }

    [Fact]
    public void ParseMessage_WithNullMessageData_ShouldThrowException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.123";
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = null!
        };

        // Act
        Action act = () => actor.InvokeParseMessage(mockContext, message);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_WithEmptyMessageData_ShouldThrowException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.123";
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = Array.Empty<byte>()
        };

        // Act
        Action act = () => actor.InvokeParseMessage(mockContext, message);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_WithCorruptedMessageData_ShouldThrowException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesMacdSignalEventActor.Actor}.{FuturesMacdSignalGeneratedCompleteEvent.Verb}.123";
        var corruptedData = new byte[] { 0x00, 0x01, 0x02, 0xFF };
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = corruptedData
        };

        // Act
        Action act = () => actor.InvokeParseMessage(mockContext, message);

        // Assert
        act.Should().Throw<Exception>();
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task ReceiveAsync_WithMacdSignalGeneratedCompleteEvent_ShouldNotThrow(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var mockState = Substitute.For<IActorState>();
        var @event = CreateMacdCompleteEvent(timePeriod);

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, @event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_WithMultipleEvents_ShouldProcessSequentially()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var mockState = Substitute.For<IActorState>();
        var event1 = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var event2 = CreateMacdCompleteEvent(TradeTimePeriodType.Weekly);

        // Act
        Func<Task> act = async () =>
        {
            await actor.InvokeReceiveAsync(mockContext, event1);
            await actor.InvokeReceiveAsync(mockContext, event2);
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, @event);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_WithNullState_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, @event);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_WithUnknownEventType_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var mockState = Substitute.For<IActorState>();

        var unknownEvent = Substitute.For<IEvent>();
        unknownEvent.Subject.Returns(new ActorSubject(ActorType.Event, FuturesMacdSignalEventActor.Actor, "UnknownVerb", "123"));

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, unknownEvent);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesMacdSignalEventActor.Actor} event from message:*");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_WithValidException_SendsEventExceptionEvent()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesMacdSignalEventActor.Actor, "1");
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("Test exception message");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await mockContext.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage == exception.Message &&
                e.ErrorType == ErrorType.EventService));
    }

    [Fact]
    public async Task OnExceptionAsync_WithArgumentNullException_SendsEventExceptionEvent()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesMacdSignalEventActor.Actor, "1");
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var exception = new ArgumentNullException("paramName", "Parameter cannot be null");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await mockContext.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage.Contains("paramName") &&
                e.ErrorType == ErrorType.EventService));
    }

    [Fact]
    public async Task OnExceptionAsync_WithTimeoutException_SendsEventExceptionEvent()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesMacdSignalEventActor.Actor, "1");
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var exception = new TimeoutException("Database operation timed out");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await mockContext.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage == exception.Message &&
                e.ErrorType == ErrorType.EventService));
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task OnExceptionAsync_ForEachTimePeriod_SendsEventExceptionEvent(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesMacdSignalEventActor.Actor, "1");
        var @event = CreateMacdCompleteEvent(timePeriod);
        var exception = new InvalidOperationException($"Failure for {timePeriod}");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await mockContext.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e => e.ErrorMessage == exception.Message));
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_WithNullContext_ShouldNotThrow()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var threadId = new ActorThreadId(ActorType.Event, FuturesMacdSignalEventActor.Actor, "1");
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("boom");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, @event, exception);

        // Assert - a null context is handled gracefully by SendErrorEventAsync/OnExceptionAsync's
        // internal try/catch, so no exception should escape to the caller.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullEvent_ShouldStillSendEventExceptionEvent()
    {
        // Arrange
        var actor = _fixture.CreateMacdEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesMacdSignalEventActor.Actor, "1");
        var exception = new InvalidOperationException("boom");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        // Passing a null @event causes the initial IsArgumentNull.Check(@event) to throw, which is caught
        // by the inner catch block and reported via the fallback SendErrorEventAsync call instead.
        await actor.InvokeOnExceptionAsync(mockContext, threadId, null!, exception);

        // Assert
        await mockContext.Received().SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e => e.ErrorMessage.Contains("@event")));
    }

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncThrows_ShouldAttemptFallbackSendAndLog()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesMacdSignalEventActor>>();
        var actor = _fixture.CreateMacdEventActor(logger: logger);
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesMacdSignalEventActor.Actor, "1");
        var @event = CreateMacdCompleteEvent(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("boom");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(_ => throw new InvalidOperationException("send failed"), _ => ValueTask.CompletedTask);

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await act.Should().NotThrowAsync();
        await mockContext.Received(2).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    #endregion
}
