using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData;

public class FuturesBarDataCommandActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesBarDataCommandActorTests(MarketDataFeedTestFixture fixture) => _fixture = fixture;

    public class TestableFuturesBarDataCommandActor(
        IEventSourceActorDbContext dbEventSource,
        ILogger<FuturesBarDataCommandActor> logger)
        : FuturesBarDataCommandActor(dbEventSource, logger)
    {
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
            ICommandActorContext context, IActorState state, ICommand command)
            => ReceiveAsync(context, state, command);

        public ValueTask InvokeOnValidateAsync(
            ICommandActorContext context, ActorThreadId threadId, ICommand command)
            => OnValidateAsync(context, threadId, command);

        public ValueTask<IActorState> InvokeOnLoadStateAsync(
            ICommandActorContext context, ActorThreadId threadId, ICommand command)
            => OnLoadStateAsync(context, threadId, command);

        public ValueTask InvokeOnSaveStateAsync(
            ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
            => OnSaveStateAsync(context, threadId, state, command);

        public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
            ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
            => OnExceptionAsync(context, threadId, command, exception);
    }

    [Theory]
    [MemberData(nameof(ValidCommands))]
    public async Task ParseMessage_ValidSupportedCommand_ReturnsConcreteCommandAndWritesLog(ICommand command)
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var result = actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(command));

        result.GetType().Should().Be(command.GetType());
        result.CommandId.Should().Be(command.CommandId);
        result.Subject.Should().Be(command.Subject);
        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(command.CommandId.ToString())));
    }

    [Fact]
    public void ParseMessage_InsertCommand_PreservesOneMinuteSampleData()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateActor(
            dbEventSource, Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var command = CreateInsertCommand();

        var result = actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(command));

        var parsed = result.Should().BeOfType<InsertFuturesBarDataCommand>().Which;
        parsed.FuturesBarData.Should().BeEquivalentTo(SampleData.FuturesBarData1);
        parsed.FuturesBarData.BarRateType.Should().Be(BarRateType.Minute);
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesBarDataCommandActor.ActorName, InsertFuturesBarDataCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", InsertFuturesBarDataCommand.Verb)]
    [InlineData(ActorType.Command, FuturesBarDataCommandActor.ActorName, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ThrowsInvalidOperationException(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var command = CreateInsertCommand();
        var subject = new ActorSubject(actorType, actorName, verb, command.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(command) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataCommandActor.ActorName} command from message: *");
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateInsertCommand()));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidPayload_Throws(bool useEmptyPayload)
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var command = CreateInsertCommand();
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = useEmptyPayload ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_CommandLogFails_PropagatesFailure()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("log unavailable")));
        var actor = _fixture.CreateActor(
            dbEventSource, Substitute.For<ILogger<FuturesBarDataCommandActor>>());

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(CreateInsertCommand()));

        act.Should().Throw<InvalidOperationException>().WithMessage("log unavailable");
    }

    [Theory]
    [MemberData(nameof(SupportedCommandsAndEvents))]
    public async Task ReceiveAsync_SupportedCommand_RecordsExpectedEventAndReturnsCommandId(
        ICommand command, Type eventType)
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        var result = await actor.InvokeReceiveAsync(
            Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.Should().ContainSingle().Which.GetType().Should().Be(eventType);
    }

    [Fact]
    public async Task ReceiveAsync_InsertThenDelete_AccumulatesEvents()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var insert = CreateInsertCommand();
        var state = new FuturesBarDataCommandState { Id = insert.Subject.ThreadId };

        await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, insert);
        await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, CreateDeleteCommand());

        state.Events.Should().HaveCount(2);
        state.Events.Should().ContainSingle(value => value is FuturesBarDataInsertedEvent);
        state.Events.Should().ContainSingle(value => value is FuturesBarDataDeletedEvent);
    }

    [Fact]
    public async Task ReceiveAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateInsertCommand();
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, state, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedCommand_ThrowsInvalidOperationException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(
            ActorType.Command, FuturesBarDataCommandActor.ActorName, "Unknown", "entity"));
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<ICommandActorContext>(), state, command).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_WrongStateType_Throws()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var command = CreateInsertCommand();
        var state = Substitute.For<IActorState>();

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<ICommandActorContext>(), state, command).AsTask();

        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [MemberData(nameof(ValidCommands))]
    public async Task OnValidateAsync_ValidSupportedCommand_DoesNotThrow(ICommand command)
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(CommandsWithoutIds))]
    public async Task OnValidateAsync_EmptyCommandId_ThrowsCommandValidationException(ICommand command)
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_StartWithNoContracts_ThrowsCommandValidationException()
    {
        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand([], SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesBarDataCommandActor.ActorName,
                StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var command = CreateInsertCommand();
        var context = Substitute.For<ICommandActorContext>();

        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(null!, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, default, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_UnsupportedCommand_ThrowsInvalidOperationException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(
            ActorType.Command, FuturesBarDataCommandActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_RepositoryReturnsState_ReturnsSameState()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateInsertCommand();
        var expected = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns(expected);

        var result = await actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command);

        result.Should().BeSameAs(expected);
        await repository.Received(1).LoadStateAsync(command);
    }

    [Fact]
    public async Task OnLoadStateAsync_RepositoryFails_PropagatesFailure()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateInsertCommand();
        repository.LoadStateAsync(command).Returns<ValueTask<FuturesBarDataCommandState>>(
            _ => throw new InvalidOperationException("load failed"));

        Func<Task> act = () => actor.InvokeOnLoadStateAsync(
            context, command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
    }

    [Fact]
    public async Task OnLoadStateAsync_NullInputs_ThrowArgumentNullException()
    {
        var (actor, context, _) = await CreateActorWithRepository();
        var command = CreateInsertCommand();

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(null!, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, default, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ValidState_SavesExactParameters()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateInsertCommand();
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        await actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command);

        await repository.Received(1).SaveStateAsync(context, state, command);
    }

    [Fact]
    public async Task OnSaveStateAsync_RepositoryFails_PropagatesFailure()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateInsertCommand();
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(
            _ => throw new InvalidOperationException("save failed"));

        Func<Task> act = () => actor.InvokeOnSaveStateAsync(
            context, command.Subject.ThreadId, state, command).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Fact]
    public async Task OnSaveStateAsync_NullInputs_ThrowArgumentNullException()
    {
        var (actor, context, _) = await CreateActorWithRepository();
        var command = CreateInsertCommand();
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(null!, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, default, state, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, null!, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_WrongStateType_Throws()
    {
        var (actor, context, _) = await CreateActorWithRepository();
        var command = CreateInsertCommand();

        Func<Task> act = () => actor.InvokeOnSaveStateAsync(
            context, command.Subject.ThreadId, Substitute.For<IActorState>(), command).AsTask();

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task OnExceptionAsync_ValidInputs_SendsErrorEventAndReturnsFailedResult()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateInsertCommand();
        Shared.EventModelActor.Events.CommandExceptionEvent? sent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(value => sent = value))
            .Returns(ValueTask.CompletedTask);

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("command failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        sent.Should().NotBeNull();
        sent!.CommandId.Should().Be(command.CommandId);
        sent.ErrorMessage.Should().Be("command failed");
    }

    [Fact]
    public async Task OnExceptionAsync_ErrorPublishingFails_ReturnsFallbackFailedResult()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateDeleteCommand();
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("publish failed"));

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new Exception("original failure"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await context.Received(2).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>());
    }

    [Fact]
    public async Task OnExceptionAsync_NullRequiredInputs_AreConvertedToFailedResults()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateInsertCommand();
        var exception = new Exception("failure");

        var nullContextResult = await actor.InvokeOnExceptionAsync(
            null!, command.Subject.ThreadId, command, exception);
        var nullThreadResult = await actor.InvokeOnExceptionAsync(
            context, default, command, exception);
        var nullCommandResult = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, null!, exception);

        nullContextResult.Success.Should().BeFalse();
        nullThreadResult.Success.Should().BeFalse();
        nullCommandResult.Success.Should().BeFalse();
    }

    async Task<(TestableFuturesBarDataCommandActor Actor, ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesBarDataCommandState> Repository)> CreateActorWithRepository()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<ILogger<FuturesBarDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repository);
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);
        return (actor, context, repository);
    }

    public static IEnumerable<object[]> ValidCommands()
    {
        yield return [CreateInsertCommand()];
        yield return [CreateDeleteCommand()];
        yield return [CreateStartCommand()];
        yield return [CreateStopCommand()];
    }

    public static IEnumerable<object[]> CommandsWithoutIds()
    {
        yield return [CreateInsertCommand(Guid.Empty)];
        yield return [CreateDeleteCommand(Guid.Empty)];
        yield return [CreateStartCommand(Guid.Empty)];
        yield return [CreateStopCommand(Guid.Empty)];
    }

    public static IEnumerable<object[]> SupportedCommandsAndEvents()
    {
        yield return [CreateInsertCommand(), typeof(FuturesBarDataInsertedEvent)];
        yield return [CreateDeleteCommand(), typeof(FuturesBarDataDeletedEvent)];
        yield return [CreateStartCommand(), typeof(FuturesBarDataStreamingStartedEvent)];
        yield return [CreateStopCommand(), typeof(FuturesBarDataStreamingStoppedEvent)];
    }

    static InsertFuturesBarDataCommand CreateInsertCommand(Guid? commandId = null)
    {
        var entityId = SampleData.FuturesBarData1.Id;
        return new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesBarDataCommandActor.ActorName,
                InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    static DeleteFuturesBarDataCommand CreateDeleteCommand(Guid? commandId = null)
    {
        var entityId = SampleData.FuturesBarDataId1;
        return new DeleteFuturesBarDataCommand(entityId)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesBarDataCommandActor.ActorName,
                DeleteFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    static StartFuturesBarDataStreamingCommand CreateStartCommand(Guid? commandId = null)
    {
        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        return new StartFuturesBarDataStreamingCommand(SampleData.FuturesContracts, SampleData.ValueDate)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesBarDataCommandActor.ActorName,
                StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    static StopFuturesBarDataStreamingCommand CreateStopCommand(Guid? commandId = null)
    {
        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        return new StopFuturesBarDataStreamingCommand(SampleData.ValueDate)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesBarDataCommandActor.ActorName,
                StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    static NatsMsg<byte[]> CreateMessage(ICommand command)
        => new() { Subject = command.Subject.ToString(), Data = Serialize(command) };

    static byte[] Serialize(ICommand command)
        => command switch
        {
            InsertFuturesBarDataCommand value => ActorExtensions.DataSerializer!.Serialize(value),
            DeleteFuturesBarDataCommand value => ActorExtensions.DataSerializer!.Serialize(value),
            StartFuturesBarDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
            StopFuturesBarDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
}
