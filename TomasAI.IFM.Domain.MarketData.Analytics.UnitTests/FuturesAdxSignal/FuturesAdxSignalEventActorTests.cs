using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAdxSignal;

public class FuturesAdxSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesAdxSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesAdxSignalEventActor : FuturesAdxSignalEventActor
    {
        public TestableFuturesAdxSignalEventActor(IActorSupervisor supervisor, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesAdxSignalEventActor> logger)
            : base(supervisor, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IActorState state, IEvent @event)
            => await ReceiveAsync(context, state, @event);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event)
            => await OnLoadStateAsync(context, threadId, @event);

        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);

        public async ValueTask InvokeOnStartAsync(IEventActorContext context)
            => await OnStartup(context);

        public async ValueTask InvokeOnStopAsync(IEventActorContext context)
            => await OnShutdown(context);
    }

    #region ParseMessage Tests - Happy Paths

    [Fact]
    public void ParseMessage_WithValidAdxSignalGeneratedCompleteEvent_ShouldReturnEvent()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result.Should().BeOfType<FuturesAdxSignalGeneratedCompleteEvent>();
        var parsedEvent = (FuturesAdxSignalGeneratedCompleteEvent)result;
        parsedEvent.CommandId.Should().Be(@event.CommandId);
        parsedEvent.Id.Should().Be(@event.Id);
        parsedEvent.EntityId.ContractId.Should().Be(SampleData.ContractId);
    }

    [Fact]
    public void ParseMessage_WithValidSubjectAndEventData_ShouldDeserializeCorrectly()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result.Should().BeOfType<FuturesAdxSignalGeneratedCompleteEvent>();
        var parsedEvent = (FuturesAdxSignalGeneratedCompleteEvent)result;
        parsedEvent.CreatedBy.Should().Be("UnitTest");
        parsedEvent.FuturesAdxSignal.ContractId.Should().Be(SampleData.ContractId);
    }

    [Fact]
    public void ParseMessage_MultipleCallsWithSameData_ShouldProduceSameResult()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result1.Should().BeOfType<FuturesAdxSignalGeneratedCompleteEvent>();
        result2.Should().BeOfType<FuturesAdxSignalGeneratedCompleteEvent>();

        var event1 = (FuturesAdxSignalGeneratedCompleteEvent)result1;
        var event2 = (FuturesAdxSignalGeneratedCompleteEvent)result2;

        event1.Id.Should().Be(event2.Id);
        event1.CommandId.Should().Be(event2.CommandId);
    }

    [Fact]
    public void ParseMessage_WithDifferentLoggerInstances_ShouldWorkConsistently()
    {
        // Arrange
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger1 = Substitute.For<ILogger<FuturesAdxSignalEventActor>>();
        var mockLogger2 = Substitute.For<ILogger<FuturesAdxSignalEventActor>>();

        var actor1 = _fixture.CreateAdxEventActor(mockSupervisor, mockLogger1);
        var actor2 = _fixture.CreateAdxEventActor(mockSupervisor, mockLogger2);

        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result1.Should().BeOfType<FuturesAdxSignalGeneratedCompleteEvent>();
        result2.Should().BeOfType<FuturesAdxSignalGeneratedCompleteEvent>();

        var event1 = (FuturesAdxSignalGeneratedCompleteEvent)result1;
        var event2 = (FuturesAdxSignalGeneratedCompleteEvent)result2;

        event1.Id.Should().Be(event2.Id);
        event1.CommandId.Should().Be(event2.CommandId);
    }

    #endregion

    #region ParseMessage Tests - Edge Cases

    [Fact]
    public void ParseMessage_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var message = new NatsMsg<byte[]>
        {
            Subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}",
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var invalidSubject = $"Command.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var invalidSubject = $"Event.WrongActor.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var invalidSubject = $"Event.{FuturesAdxSignalEventActor.Actor}.UnknownVerb.123";
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent(commandId: Guid.Empty);

        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var malformedSubject = "InvalidSubjectFormat";
        var message = new NatsMsg<byte[]>
        {
            Subject = malformedSubject,
            Data = _fixture.DataSerializer.Serialize(SampleData.CreateAdxSignalGeneratedCompleteEvent())
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesAdxSignalEventActor.Actor}.{FuturesAdxSignalGeneratedCompleteEvent.Verb}.123";
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

    #region ReceiveAsync Tests - Happy Paths

    [Fact]
    public async Task ReceiveAsync_WithAdxSignalGeneratedCompleteEvent_ShouldNotThrow()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, default, @event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_WithMultipleEvents_ShouldProcessSequentially()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var event1 = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var event2 = SampleData.CreateAdxSignalGeneratedCompleteEvent();

        // Act
        Func<Task> act = async () =>
        {
            await actor.InvokeReceiveAsync(mockContext, default, event1);
            await actor.InvokeReceiveAsync(mockContext, default, event2);
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ReceiveAsync Tests - Edge Cases

    [Fact]
    public async Task ReceiveAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, default, @event);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, default, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_WithUnknownEventType_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        var unknownEvent = Substitute.For<IEvent>();
        unknownEvent.Subject.Returns(new ActorSubject(ActorType.Event, FuturesAdxSignalEventActor.Actor, "UnknownVerb", "123"));

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, default, unknownEvent);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAdxSignalEventActor.Actor} event from message:*");
    }

    #endregion

    #region OnExceptionAsync Tests - Happy Paths

    [Fact]
    public async Task OnExceptionAsync_WithValidException_SendsEventExceptionEvent()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
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
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var exception = new TimeoutException("Database operation timed out");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await mockContext.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage == "Database operation timed out" &&
                e.ErrorType == ErrorType.EventService));
    }

    [Fact]
    public async Task OnExceptionAsync_DoesNotThrow_WhenSuccessful()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var exception = new InvalidOperationException("Test exception");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_IncludesErrorData_WithFullExceptionDetails()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var exception = new InvalidOperationException("Detailed error");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await mockContext.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage == "Detailed error" &&
                e.ErrorData.Contains("InvalidOperationException")));
    }

    #endregion

    #region OnExceptionAsync Tests - Edge Cases

    [Fact]
    public async Task OnExceptionAsync_WithNullContext_ShouldNotThrow()
    {
        // Arrange
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FuturesAdxSignalEventActor>>();
        var actor = _fixture.CreateAdxEventActor(mockSupervisor, mockLogger);
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var exception = new Exception("Test error");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, @event, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullThreadId_ShouldNotThrow()
    {
        // Arrange
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FuturesAdxSignalEventActor>>();
        var actor = _fixture.CreateAdxEventActor(mockSupervisor, mockLogger);
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var exception = new Exception("Test error");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(mockContext, default, @event, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullEvent_ShouldNotThrow()
    {
        // Arrange
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FuturesAdxSignalEventActor>>();
        var actor = _fixture.CreateAdxEventActor(mockSupervisor, mockLogger);
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var exception = new Exception("Test error");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(mockContext, threadId, null!, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncFails_ShouldNotThrow()
    {
        // Arrange
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FuturesAdxSignalEventActor>>();
        var actor = _fixture.CreateAdxEventActor(mockSupervisor, mockLogger);
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesAdxSignalEventActor.Actor, "1");
        var @event = SampleData.CreateAdxSignalGeneratedCompleteEvent();
        var exception = new Exception("Original error");

        // First call throws, second call (from catch) succeeds
        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(
                _ => throw new Exception("SendAsync failed"),
                _ => ValueTask.CompletedTask);

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnStartAsync Tests - Happy Paths

    [Fact]
    public async Task OnStartAsync_WithValidContext_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnStartAsync(mockContext);

        // Assert — OnStartup is empty for ADX, so it should complete without any side effects
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnStartAsync_CalledMultipleTimes_ShouldCompleteEachTime()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        Func<Task> act = async () =>
        {
            await actor.InvokeOnStartAsync(mockContext);
            await actor.InvokeOnStartAsync(mockContext);
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnStartAsync_DoesNotAddEventRouter()
    {
        // Arrange — ADX EventActor has empty OnStartup (no event routing)
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        await actor.InvokeOnStartAsync(mockContext);

        // Assert — No AddEventRouter calls should be made
        mockContext.DidNotReceive().AddEventRouter(Arg.Any<ActorTypeId>(), Arg.Any<ActorMailboxId>());
    }

    #endregion

    #region OnStartAsync Tests - Edge Cases

    [Fact]
    public async Task OnStartAsync_WithNullContext_ShouldNotThrow()
    {
        // Arrange — OnStartup is empty, so even null context should not throw
        var actor = _fixture.CreateAdxEventActor();

        // Act
        Func<Task> act = async () => await actor.InvokeOnStartAsync(null!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnStopAsync Tests - Happy Paths

    [Fact]
    public async Task OnStopAsync_WithValidContext_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnStopAsync(mockContext);

        // Assert — OnShutdown is empty for ADX, so it should complete without any side effects
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnStopAsync_CalledMultipleTimes_ShouldCompleteEachTime()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        Func<Task> act = async () =>
        {
            await actor.InvokeOnStopAsync(mockContext);
            await actor.InvokeOnStopAsync(mockContext);
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnStopAsync_DoesNotRemoveEventRouter()
    {
        // Arrange — ADX EventActor has empty OnShutdown (no event routing)
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        await actor.InvokeOnStopAsync(mockContext);

        // Assert — No RemoveEventRouter calls should be made
        mockContext.DidNotReceive().RemoveEventRouter(Arg.Any<ActorTypeId>(), Arg.Any<ActorMailboxId>());
    }

    [Fact]
    public async Task OnStopAsync_AfterOnStartAsync_ShouldCompleteWithoutSideEffects()
    {
        // Arrange
        var actor = _fixture.CreateAdxEventActor();
        var mockContext = Substitute.For<IEventActorContext>();

        // Act
        await actor.InvokeOnStartAsync(mockContext);
        await actor.InvokeOnStopAsync(mockContext);

        // Assert — Both OnStartup and OnShutdown are empty for ADX, no router calls
        mockContext.DidNotReceive().AddEventRouter(Arg.Any<ActorTypeId>(), Arg.Any<ActorMailboxId>());
        mockContext.DidNotReceive().RemoveEventRouter(Arg.Any<ActorTypeId>(), Arg.Any<ActorMailboxId>());
    }

    [Fact]
    public async Task OnStopAsync_WithNullContext_ShouldNotThrow()
    {
        // Arrange — OnShutdown is empty, so even null context should not throw
        var actor = _fixture.CreateAdxEventActor();

        // Act
        Func<Task> act = async () => await actor.InvokeOnStopAsync(null!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}
