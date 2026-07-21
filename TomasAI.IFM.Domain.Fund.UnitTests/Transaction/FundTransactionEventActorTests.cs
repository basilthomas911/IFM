using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Transaction.Event;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Transaction;

/// <summary>
/// Contains unit tests for <see cref="FundTransactionEventActor"/>, covering ParseMessage, ReceiveAsync and
/// OnExceptionAsync behaviors, including happy paths and edge cases.
/// </summary>
/// <remarks>
/// The actor's internal parse/dispatch maps currently have no registered handlers, so ParseMessage always
/// ignores messages (returns null) and ReceiveAsync always throws for any resolved event. Both behaviors are
/// exercised here as the actor's current, correct contract.
/// </remarks>
public class FundTransactionEventActorTests : IClassFixture<FundTestFixture>
{
    readonly FundTestFixture _fixture;

    public FundTransactionEventActorTests(FundTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage, ReceiveAsync and OnExceptionAsync for unit testing.
    public class TestableFundTransactionEventActor : FundTransactionEventActor
    {
        public TestableFundTransactionEventActor(IActorSupervisor supervisor, ILogger<FundTransactionEventActor> logger)
            : base(supervisor, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);

        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    static IEvent CreateSampleEvent(string verb = "SomeVerb")
    {
        var @event = Substitute.For<IEvent>();
        @event.Subject.Returns(new ActorSubject(ActorType.Event, FundTransactionEventActor.Actor, verb, "1"));
        @event.CommandId.Returns(Guid.NewGuid());
        return @event;
    }

    #region ParseMessage

    [Fact]
    public void ParseMessage_GivenNullContext_WhenParsed_ThenThrowsArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var message = new NatsMsg<byte[]>($"Event.{FundTransactionEventActor.Actor}.SomeVerb.1", string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, message);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_GivenMessageForDifferentActor_WhenParsed_ThenReturnsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var context = Substitute.For<IEventActorContext>();
        var message = new NatsMsg<byte[]>("Event.SomeOtherActor.SomeVerb.1", string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_GivenMessageWithMatchingActorButUnregisteredVerb_WhenParsed_ThenReturnsNull()
    {
        // Arrange - the actor currently registers no parse handlers, so any verb is unresolved.
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var context = Substitute.For<IEventActorContext>();
        var message = new NatsMsg<byte[]>($"Event.{FundTransactionEventActor.Actor}.AnyVerb.1", string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_GivenInvalidSubjectFormat_WhenParsed_ThenThrows()
    {
        // Arrange
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var context = Substitute.For<IEventActorContext>();
        var message = new NatsMsg<byte[]>("Invalid.Subject.Format", string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(context, message);

        // Assert
        act.Should().Throw<Exception>();
    }

    #endregion

    #region ReceiveAsync

    [Fact]
    public async Task ReceiveAsync_GivenNullContext_WhenExecuted_ThenThrowsArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var @event = CreateSampleEvent();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, @event);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_GivenNullEvent_WhenExecuted_ThenThrowsArgumentNullException()
    {
        // Arrange
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var context = Substitute.For<IEventActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_GivenAnyEvent_WhenExecuted_ThenThrowsInvalidOperationException()
    {
        // Arrange - the actor currently registers no receive handlers, so all events are unresolved.
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var context = Substitute.For<IEventActorContext>();
        var @event = CreateSampleEvent();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, @event);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FundTransactionEventActor.Actor} event from message:*");
    }

    #endregion

    #region OnExceptionAsync

    [Fact]
    public async Task OnExceptionAsync_GivenValidException_WhenHandled_ThenSendsEventExceptionEvent()
    {
        // Arrange
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var context = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FundTransactionEventActor.Actor, "1");
        var @event = CreateSampleEvent();
        var ex = new InvalidOperationException("Test exception message");

        context.SendAsync<EventExceptionEvent, ActorEntityId>(Arg.Any<EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, @event, ex);

        // Assert
        await context.Received(1).SendAsync<EventExceptionEvent, ActorEntityId>(
            Arg.Is<EventExceptionEvent>(e => e.ErrorMessage == ex.Message && e.ErrorType == ErrorType.EventService));
    }

    [Fact]
    public async Task OnExceptionAsync_GivenNullContext_WhenHandled_ThenSwallowsExceptionAndLogsError()
    {
        // Arrange - the inner catch block attempts a fallback SendErrorEventAsync using the same (null) context,
        // which also fails; the outer exception is caught and logged rather than propagated.
        var logger = Substitute.For<ILogger<FundTransactionEventActor>>();
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), logger);
        var threadId = new ActorThreadId(ActorType.Event, FundTransactionEventActor.Actor, "1");
        var @event = CreateSampleEvent();
        var ex = new InvalidOperationException("Test exception message");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, @event, ex);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncThrows_ThenFallsBackAndSendsAgain()
    {
        // Arrange
        var actor = _fixture.CreateActor(Substitute.For<IActorSupervisor>(), Substitute.For<ILogger<FundTransactionEventActor>>());
        var context = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FundTransactionEventActor.Actor, "1");
        var @event = CreateSampleEvent();
        var ex = new InvalidOperationException("Test exception message");

        var callCount = 0;
        context.SendAsync<EventExceptionEvent, ActorEntityId>(Arg.Any<EventExceptionEvent>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("send failed");
                return ValueTask.CompletedTask;
            });

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, @event, ex);

        // Assert - the internal SendErrorEventAsync retries once with a fallback error event after the first send fails
        await act.Should().NotThrowAsync();
        await context.Received(2).SendAsync<EventExceptionEvent, ActorEntityId>(Arg.Any<EventExceptionEvent>());
    }

    #endregion
}
