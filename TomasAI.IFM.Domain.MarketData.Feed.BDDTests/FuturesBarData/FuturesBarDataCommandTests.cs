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

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesBarData;

public class FuturesBarDataCommandTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesBarDataCommandTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Given_ARepositoryInTheActorContainer_When_TheActorStarts_Then_ItResolvesTheRepository()
    {
        var actor = _fixture.CreateCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repository);

        await actor.InvokeOnStartup(context);

        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
    }

    [Fact]
    public async Task Given_NoActorContext_When_TheActorStarts_Then_ItRejectsTheRequest()
    {
        var actor = _fixture.CreateCommandActor();

        Func<Task> act = () => actor.InvokeOnStartup(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(SampleData.FuturesBarDataCases), MemberType = typeof(SampleData))]
    public async Task Given_AValidOneMinuteBar_When_AnInsertMessageIsParsed_Then_TheCommandIsPreservedAndLogged(
        FuturesBarDataReadModel barData, DateTime windowStart, DateTime windowEnd)
    {
        (windowEnd - windowStart).Should().Be(TimeSpan.FromMinutes(1));
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateCommandActor(db);
        var command = CreateInsertCommand(barData);
        var message = CreateMessage(command);

        var result = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        var parsed = result.Should().BeOfType<InsertFuturesBarDataCommand>().Which;
        parsed.CommandId.Should().Be(command.CommandId);
        parsed.Subject.Should().Be(command.Subject);
        parsed.EntityId.Should().BeEquivalentTo(command.EntityId);
        parsed.FuturesBarData.Should().BeEquivalentTo(command.FuturesBarData);
        await db.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(logged => logged.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(command.CommandId.ToString())));
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("Start")]
    [InlineData("Stop")]
    public async Task Given_AValidSupportedMessage_When_ItIsParsed_Then_TheMatchingCommandIsReturnedAndLogged(string commandKind)
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateCommandActor(db);
        var command = CreateSupportedCommand(commandKind);

        var result = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), CreateMessage(command));

        result.GetType().Should().Be(command.GetType());
        result.CommandId.Should().Be(command.CommandId);
        await db.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(logged => logged.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesBarDataCommandActor.ActorName, InsertFuturesBarDataCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", InsertFuturesBarDataCommand.Verb)]
    [InlineData(ActorType.Command, FuturesBarDataCommandActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidSubject_When_ACommandMessageIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateCommandActor();
        var command = CreateInsertCommand(SampleData.FuturesBarData);
        var subject = new ActorSubject(actorType, actorName, verb, command.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(command) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataCommandActor.ActorName} command from message: *");
    }

    [Fact]
    public void Given_AValidSubjectWithCorruptData_When_TheMessageIsParsed_Then_DeserializationFails()
    {
        var actor = _fixture.CreateCommandActor();
        var command = CreateInsertCommand(SampleData.FuturesBarData);
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Theory]
    [MemberData(nameof(SampleData.FuturesBarDataCases), MemberType = typeof(SampleData))]
    public async Task Given_AValidOneMinuteBar_When_TheInsertCommandIsReceived_Then_AnInsertedEventIsRecorded(
        FuturesBarDataReadModel barData, DateTime windowStart, DateTime windowEnd)
    {
        (windowEnd - windowStart).Should().Be(TimeSpan.FromMinutes(1));
        var actor = _fixture.CreateCommandActor();
        var command = CreateInsertCommand(barData);
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        var result = await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.Should().ContainSingle().Which.Should().BeOfType<FuturesBarDataInsertedEvent>()
            .Which.FuturesBarData.Should().BeEquivalentTo(barData);
    }

    [Theory]
    [InlineData("Delete", typeof(FuturesBarDataDeletedEvent))]
    [InlineData("Start", typeof(FuturesBarDataStreamingStartedEvent))]
    [InlineData("Stop", typeof(FuturesBarDataStreamingStoppedEvent))]
    public async Task Given_AValidSupportedCommand_When_ItIsReceived_Then_TheExpectedDomainEventIsRecorded(
        string commandKind, Type expectedEventType)
    {
        var actor = _fixture.CreateCommandActor();
        var command = CreateSupportedCommand(commandKind);
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        var result = await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.Should().ContainSingle().Which.GetType().Should().Be(expectedEventType);
    }

    [Fact]
    public async Task Given_AnUnsupportedCommand_When_ItIsReceived_Then_ItIsRejected()
    {
        var actor = _fixture.CreateCommandActor();
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(ActorType.Command, FuturesBarDataCommandActor.ActorName, "Unknown", "entity"));
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        Func<Task> act = () => actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_TheCommandIsReceived_Then_EachMissingInputIsRejected()
    {
        var actor = _fixture.CreateCommandActor();
        var command = CreateInsertCommand(SampleData.FuturesBarData);
        var context = Substitute.For<ICommandActorContext>();
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, state, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(ValidCommands))]
    public async Task Given_AValidSupportedCommand_When_ItIsValidated_Then_NoValidationErrorIsRaised(ICommand command)
    {
        var actor = _fixture.CreateCommandActor();

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(CommandsWithoutIds))]
    public async Task Given_ACommandWithoutAnId_When_ItIsValidated_Then_AValidationErrorIsRaised(ICommand command)
    {
        var actor = _fixture.CreateCommandActor();

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedCommand_When_ItIsValidated_Then_ItIsRejected()
    {
        var actor = _fixture.CreateCommandActor();
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(ActorType.Command, FuturesBarDataCommandActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Given_ARepositoryState_When_StateIsLoadedAndSaved_Then_TheRepositoryReceivesTheSameCommandAndState()
    {
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
        var actor = _fixture.CreateCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        var command = CreateInsertCommand(SampleData.FuturesBarDataAlternate);
        var expectedState = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns(expectedState);

        var loaded = await actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command);
        await actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, loaded, command);

        loaded.Should().BeSameAs(expectedState);
        await repository.Received(1).LoadStateAsync(command);
        await repository.Received(1).SaveStateAsync(context, expectedState, command);
    }

    [Fact]
    public async Task Given_RepositoryFailures_When_StateIsLoadedOrSaved_Then_TheFailuresPropagate()
    {
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
        var actor = _fixture.CreateCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        var command = CreateInsertCommand(SampleData.FuturesBarData);
        var state = new FuturesBarDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns<ValueTask<FuturesBarDataCommandState>>(
            _ => throw new InvalidOperationException("load failed"));
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(
            _ => throw new InvalidOperationException("save failed"));

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Fact]
    public async Task Given_ACommandFailure_When_TheExceptionIsHandled_Then_AnErrorEventAndFailedResultAreProduced()
    {
        var actor = _fixture.CreateCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateInsertCommand(SampleData.FuturesBarData);
        Shared.EventModelActor.Events.CommandExceptionEvent? sentEvent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(value => sentEvent = value))
            .Returns(ValueTask.CompletedTask);

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("database unavailable"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        sentEvent.Should().NotBeNull();
        sentEvent!.CommandId.Should().Be(command.CommandId);
        sentEvent.ErrorMessage.Should().Be("database unavailable");
    }

    [Fact]
    public async Task Given_ErrorEventPublishingAlsoFails_When_TheExceptionIsHandled_Then_AFailedResultIsStillReturned()
    {
        var actor = _fixture.CreateCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateInsertCommand(SampleData.FuturesBarData);
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("publish failed"));

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("original failure"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
    }

    public static IEnumerable<object[]> ValidCommands()
    {
        yield return [CreateInsertCommand(SampleData.FuturesBarData)];
        yield return [CreateSupportedCommand("Delete")];
        yield return [CreateSupportedCommand("Start")];
        yield return [CreateSupportedCommand("Stop")];
    }

    public static IEnumerable<object[]> CommandsWithoutIds()
    {
        yield return [CreateInsertCommand(SampleData.FuturesBarData, Guid.Empty)];
        yield return [CreateSupportedCommand("Delete", Guid.Empty)];
        yield return [CreateSupportedCommand("Start", Guid.Empty)];
        yield return [CreateSupportedCommand("Stop", Guid.Empty)];
    }

    static InsertFuturesBarDataCommand CreateInsertCommand(
        FuturesBarDataReadModel barData, Guid? commandId = null)
    {
        var entityId = barData.Id;
        return new InsertFuturesBarDataCommand(barData)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesBarDataCommandActor.ActorName,
                InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    static ICommand CreateSupportedCommand(string commandKind, Guid? commandId = null)
        => commandKind switch
        {
            "Delete" => CreateDeleteCommand(commandId),
            "Start" => CreateStartCommand(commandId),
            "Stop" => CreateStopCommand(commandId),
            _ => throw new ArgumentOutOfRangeException(nameof(commandKind))
        };

    static DeleteFuturesBarDataCommand CreateDeleteCommand(Guid? commandId = null)
    {
        var entityId = SampleData.FuturesBarDataAlternate.Id;
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
