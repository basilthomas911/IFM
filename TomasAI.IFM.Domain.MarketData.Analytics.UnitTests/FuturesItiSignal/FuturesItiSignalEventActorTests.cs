using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public class FuturesItiSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesItiSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesItiSignalEventActor : FuturesItiSignalEventActor
    {
        public TestableFuturesItiSignalEventActor(IActorSupervisor supervisor, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesItiSignalEventActor> logger)
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

    #region Helpers

    public static IEnumerable<object[]> SupportedTimePeriods =>
    [
        [TradeTimePeriodType.Daily],
        [TradeTimePeriodType.Weekly],
        [TradeTimePeriodType.Monthly]
    ];

    #endregion

    #region ParseMessage Tests - Happy Paths

    [Fact]
    public void ParseMessage_WithValidEodDataInsertedCompleteEvent_ShouldReturnEvent()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateEodDataInsertedCompleteEvent();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesEodDataInsertedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result.Should().BeOfType<FuturesEodDataInsertedCompleteEvent>();
        var parsedEvent = (FuturesEodDataInsertedCompleteEvent)result;
        parsedEvent.CommandId.Should().Be(@event.CommandId);
        parsedEvent.Id.Should().Be(@event.Id);
        parsedEvent.FuturesEodData.ContractId.Should().Be(SampleData.ContractId);
    }

    [Fact]
    public void ParseMessage_WithValidItiSignalGeneratedCompleteEvent_ShouldReturnEvent()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>();
        var parsedEvent = (FuturesItiSignalGeneratedCompleteEvent)result;
        parsedEvent.CommandId.Should().Be(@event.CommandId);
        parsedEvent.Id.Should().Be(@event.Id);
        parsedEvent.EntityId.ContractId.Should().Be(SampleData.ContractId);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void ParseMessage_WithItiSignalGeneratedCompleteEvent_AcrossTimePeriods_ShouldReturnEvent(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var entityId = SampleData.EntityIdFor(timePeriod);
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent() with { EntityId = entityId };
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.{entityId.Format()}";
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
        result.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>();
        var parsedEvent = (FuturesItiSignalGeneratedCompleteEvent)result;
        parsedEvent.EntityId.TimePeriod.Should().Be(timePeriod);
        parsedEvent.EntityId.ContractId.Should().Be(SampleData.ContractId);
    }

    [Fact]
    public void ParseMessage_WithValidSubjectAndEventData_ShouldDeserializeCorrectly()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>();
        var parsedEvent = (FuturesItiSignalGeneratedCompleteEvent)result;
        parsedEvent.CreatedBy.Should().Be("UnitTest");
    }

    [Fact]
    public void ParseMessage_MultipleCallsWithSameData_ShouldProduceSameResult()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result1.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>();
        result2.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>();

        var event1 = (FuturesItiSignalGeneratedCompleteEvent)result1;
        var event2 = (FuturesItiSignalGeneratedCompleteEvent)result2;

        event1.Id.Should().Be(event2.Id);
        event1.CommandId.Should().Be(event2.CommandId);
    }

    [Fact]
    public void ParseMessage_WithDifferentLoggerInstances_ShouldWorkConsistently()
    {
        // Arrange
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger1 = Substitute.For<ILogger<FuturesItiSignalEventActor>>();
        var mockLogger2 = Substitute.For<ILogger<FuturesItiSignalEventActor>>();

        var actor1 = _fixture.CreateActor(mockSupervisor, mockLogger1);
        var actor2 = _fixture.CreateActor(mockSupervisor, mockLogger2);

        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        result1.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>();
        result2.Should().BeOfType<FuturesItiSignalGeneratedCompleteEvent>();

        var event1 = (FuturesItiSignalGeneratedCompleteEvent)result1;
        var event2 = (FuturesItiSignalGeneratedCompleteEvent)result2;

        event1.Id.Should().Be(event2.Id);
        event1.CommandId.Should().Be(event2.CommandId);
    }

    #endregion

    #region ParseMessage Tests - Edge Cases

    [Fact]
    public void ParseMessage_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var message = new NatsMsg<byte[]>
        {
            Subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}",
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var invalidSubject = $"Command.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var invalidSubject = $"Event.WrongActor.{FuturesItiSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var invalidSubject = $"Event.{FuturesItiSignalEventActor.Actor}.UnknownVerb.123";
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent(commandId: Guid.Empty);

        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.{@event.EntityId.Format()}";
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var malformedSubject = "InvalidSubjectFormat";
        var message = new NatsMsg<byte[]>
        {
            Subject = malformedSubject,
            Data = _fixture.DataSerializer.Serialize(SampleData.CreateItiSignalGeneratedCompleteEvent())
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.123";
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FuturesItiSignalEventActor.Actor}.{FuturesItiSignalGeneratedCompleteEvent.Verb}.123";
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
    public async Task ReceiveAsync_WithEodDataInsertedCompleteEvent_ShouldNotThrow()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateEodDataInsertedCompleteEvent();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, @event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_WithItiSignalGeneratedCompleteEvent_ShouldNotThrow()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, @event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public async Task ReceiveAsync_WithItiSignalGeneratedCompleteEvent_AcrossTimePeriods_ShouldNotThrow(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var entityId = SampleData.EntityIdFor(timePeriod);
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent() with { EntityId = entityId };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, @event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveAsync_WithMultipleEvents_ShouldProcessSequentially()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var event1 = SampleData.CreateEodDataInsertedCompleteEvent();
        var event2 = SampleData.CreateItiSignalGeneratedCompleteEvent();

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

    #region ReceiveAsync Tests - Edge Cases

    [Fact]
    public async Task ReceiveAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, @event);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateActor();
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();

        var unknownEvent = Substitute.For<IEvent>();
        unknownEvent.Subject.Returns(new ActorSubject(ActorType.Event, FuturesItiSignalEventActor.Actor, "UnknownVerb", "123"));

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, unknownEvent);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesItiSignalEventActor.Actor} event from message:*");
    }

    #endregion

    #region OnExceptionAsync Tests - Happy Paths

    [Fact]
    public async Task OnExceptionAsync_WithValidException_SendsEventExceptionEvent()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var @event = SampleData.CreateEodDataInsertedCompleteEvent();
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

    [Fact]
    public async Task OnExceptionAsync_DoesNotThrow_WhenSuccessful()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var exception = new Exception("Generic failure");

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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var exception = new InvalidOperationException("Full detail exception message");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await mockContext.Received(1).SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage == exception.Message));
    }

    #endregion

    #region OnExceptionAsync Tests - Edge Cases

    [Fact]
    public async Task OnExceptionAsync_WithNullContext_ShouldNotThrow()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, @event, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullThreadId_ShouldNotThrow()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var exception = new InvalidOperationException("Test exception");

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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var exception = new InvalidOperationException("Test exception");

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
        var actor = _fixture.CreateActor();
        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FuturesItiSignalEventActor.Actor, "1");
        var @event = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var exception = new InvalidOperationException("Original failure");

        mockContext.SendAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Send failed"));

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}

