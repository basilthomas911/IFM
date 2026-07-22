using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesClosingPrice;

public class FuturesClosingPriceCommandTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesClosingPriceCommandTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Given_ARepository_When_TheActorStarts_Then_ItResolvesTheClosingPriceStateRepository()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repository);

        await actor.InvokeOnStartup(context);

        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();
    }

    [Fact]
    public async Task Given_AValidInsertMessage_When_ItIsParsed_Then_TheCommandIsPreservedAndLogged()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateClosingPriceCommandActor(dbEventSource);
        var command = CreateCommand();

        var result = actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(command));

        var parsed = result.Should().BeOfType<InsertFuturesClosingPriceCommand>().Which;
        parsed.CommandId.Should().Be(command.CommandId);
        parsed.FuturesClosingPriceId.Should().Be(SampleData.FuturesClosingPriceId);
        parsed.ClosingPrice.Should().Be(SampleData.ClosingPrice);
        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesClosingPriceCommandActor.ActorName, InsertFuturesClosingPriceCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", InsertFuturesClosingPriceCommand.Verb)]
    [InlineData(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidSubject_When_TheMessageIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var command = CreateCommand();
        var subject = new ActorSubject(actorType, actorName, verb, command.EntityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = subject.ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(command)
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_InvalidPayload_When_TheMessageIsParsed_Then_DeserializationFails(bool empty)
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var command = CreateCommand();
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = empty ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_TheCommandLogFails_When_TheMessageIsParsed_Then_TheFailurePropagates()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("log failed")));
        var actor = _fixture.CreateClosingPriceCommandActor(dbEventSource);

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(CreateCommand()));

        act.Should().Throw<InvalidOperationException>().WithMessage("log failed");
    }

    [Fact]
    public async Task Given_AValidInsertCommand_When_ItIsReceived_Then_AnInsertedEventAndCommandIdAreReturned()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var command = CreateCommand();
        var state = new FuturesClosingPriceCommandState { Id = command.Subject.ThreadId };

        var result = await actor.InvokeReceiveAsync(
            Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        var inserted = state.Events.Should().ContainSingle().Which
            .Should().BeOfType<FuturesClosingPriceInsertedEvent>().Which;
        inserted.FuturesClosingPriceId.Should().Be(command.FuturesClosingPriceId);
        inserted.ClosingPrice.Should().Be(command.ClosingPrice);
    }

    [Fact]
    public async Task Given_TheClosingPriceAlreadyExists_When_TheCommandIsReceived_Then_A_DuplicateExceptionIsRaised()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var command = CreateCommand();
        var state = new FuturesClosingPriceCommandState { Id = command.Subject.ThreadId };
        await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<ICommandActorContext>(), state, CreateCommand()).AsTask();

        await act.Should().ThrowAsync<InsertFuturesClosingPriceException>();
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_TheCommandIsReceived_Then_EachInputIsRejected()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand();
        var state = new FuturesClosingPriceCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, state, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedCommand_When_ItIsReceivedOrValidated_Then_ItIsRejected()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(
            ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "Unknown", "entity"));
        var state = new FuturesClosingPriceCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Given_AValidCommand_When_ItIsValidated_Then_NoValidationErrorIsRaised()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var command = CreateCommand();

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 1000001)]
    [InlineData(1, 5448.75)]
    public async Task Given_InvalidCommandValues_When_TheCommandIsValidated_Then_AValidationErrorIsRaised(
        int invalidKind, decimal closingPrice)
    {
        var id = invalidKind == 1
            ? new FuturesDataId(string.Empty, SampleData.ValueDate)
            : SampleData.FuturesClosingPriceId;
        var command = CreateCommand(id, closingPrice, invalidKind == 0 ? Guid.Empty : Guid.NewGuid());
        var actor = _fixture.CreateClosingPriceCommandActor();

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_ARepositoryState_When_ItIsLoadedAndSaved_Then_TheRepositoryReceivesExactValues()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand();
        var state = new FuturesClosingPriceCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns(state);

        var loaded = await actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command);
        await actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, loaded, command);

        loaded.Should().BeSameAs(state);
        await repository.Received(1).LoadStateAsync(command);
        await repository.Received(1).SaveStateAsync(context, state, command);
    }

    [Fact]
    public async Task Given_RepositoryFailures_When_StateIsLoadedOrSaved_Then_TheFailuresPropagate()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand();
        var state = new FuturesClosingPriceCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns<ValueTask<FuturesClosingPriceCommandState>>(
            _ => throw new InvalidOperationException("load failed"));
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(
            _ => throw new InvalidOperationException("save failed"));

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Fact]
    public async Task Given_A_GenericCommandFailure_When_ItIsHandled_Then_ACommandErrorEventIsReturned()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand();
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("insert failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await context.Received(1)
            .SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Is<Shared.EventModelActor.Events.CommandExceptionEvent>(value =>
                    value.CommandId == command.CommandId && value.ErrorMessage == "insert failed"));
    }

    [Fact]
    public async Task Given_A_DuplicateInsertFailure_When_ItIsHandled_Then_AClosingPriceFailEventIsSent()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand();
        context.SendAsync<FuturesClosingPriceInsertedFailEvent, FuturesDataId>(
                Arg.Any<FuturesClosingPriceInsertedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command,
            new InsertFuturesClosingPriceException("duplicate closing price"));

        result.Success.Should().BeFalse();
        await context.Received(1).SendAsync<FuturesClosingPriceInsertedFailEvent, FuturesDataId>(
            Arg.Is<FuturesClosingPriceInsertedFailEvent>(value =>
                value.CommandId == command.CommandId && value.ErrorMessage == "duplicate closing price"));
    }

    async Task<(TestableFuturesClosingPriceCommandActor Actor, ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesClosingPriceCommandState> Repository)> CreateActorWithRepository()
    {
        var actor = _fixture.CreateClosingPriceCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        return (actor, context, repository);
    }

    static InsertFuturesClosingPriceCommand CreateCommand(
        FuturesDataId? id = null, decimal? closingPrice = null, Guid? commandId = null)
    {
        var entityId = id ?? SampleData.FuturesClosingPriceId;
        return new InsertFuturesClosingPriceCommand(entityId, closingPrice ?? SampleData.ClosingPrice)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesClosingPriceCommandActor.ActorName,
                InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    static NatsMsg<byte[]> CreateMessage(InsertFuturesClosingPriceCommand command)
        => new()
        {
            Subject = command.Subject.ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(command)
        };
}
