using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesEodData;

public class FuturesEodDataCommandTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesEodDataCommandTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Given_AnEodRepository_When_TheActorStarts_Then_ItResolvesThatRepository()
    {
        var actor = _fixture.CreateEodCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repository);

        await actor.InvokeOnStartup(context);

        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();
    }

    [Fact]
    public async Task Given_NoContext_When_TheActorStarts_Then_StartupIsRejected()
    {
        var actor = _fixture.CreateEodCommandActor();

        Func<Task> act = () => actor.InvokeOnStartup(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AValidEodCommandMessage_When_ItIsParsed_Then_TheCommandIsPreservedAndLogged(bool vix)
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateEodCommandActor(db);
        var command = vix ? (ICommand)CreateVixCommand() : CreateEodCommand();

        var parsed = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), CreateMessage(command));

        parsed.GetType().Should().Be(command.GetType());
        parsed.CommandId.Should().Be(command.CommandId);
        GetEntityId(parsed).Should().BeEquivalentTo(GetEntityId(command));
        await db.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(command.CommandId.ToString())));
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesEodDataCommandActor.ActorName, InsertFuturesEodDataCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", InsertFuturesEodDataCommand.Verb)]
    [InlineData(ActorType.Command, FuturesEodDataCommandActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidCommandSubject_When_ItIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateEodCommandActor();
        var command = CreateEodCommand();
        var subject = new ActorSubject(actorType, actorName, verb, command.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(command) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataCommandActor.ActorName} command from message: *");
    }

    [Fact]
    public void Given_CorruptCommandData_When_ItIsParsed_Then_DeserializationFails()
    {
        var actor = _fixture.CreateEodCommandActor();
        var command = CreateEodCommand();
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_TheCommandLogFails_When_AnEodMessageIsParsed_Then_TheFailurePropagates()
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("log failed")));
        var actor = _fixture.CreateEodCommandActor(db);

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(CreateEodCommand()));

        act.Should().Throw<InvalidOperationException>().WithMessage("log failed");
    }

    [Fact]
    public void Given_NoContext_When_ACommandMessageIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateEodCommandActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateEodCommand()));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AValidEodCommand_When_ItIsReceived_Then_TheExpectedEventAndCommandIdAreProduced(bool vix)
    {
        var actor = _fixture.CreateEodCommandActor();
        var command = vix ? (ICommand)CreateVixCommand() : CreateEodCommand();
        var state = new FuturesEodDataCommandState { Id = command.Subject.ThreadId };

        var result = await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.Should().ContainSingle().Which.GetType().Should().Be(
            vix ? typeof(VixFuturesEodDataInsertedEvent) : typeof(FuturesEodDataInsertedEvent));
    }

    [Fact]
    public async Task Given_AFullEodCommand_When_ItIsReceived_Then_TheCalculatedEventRetainsItsIdentity()
    {
        var actor = _fixture.CreateEodCommandActor();
        var command = CreateEodCommand();
        var state = new FuturesEodDataCommandState { Id = command.Subject.ThreadId };

        await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        var inserted = state.Events.Should().ContainSingle().Which
            .Should().BeOfType<FuturesEodDataInsertedEvent>().Which;
        inserted.CommandId.Should().Be(command.CommandId);
        inserted.EntityId.Should().Be(command.EntityId);
        inserted.FuturesEodData.ContractId.Should().Be(SampleData.EsContract.ContractId);
        inserted.FuturesEodData.ValueDate.Should().Be(SampleData.ValueDate);
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_ACommandIsReceived_Then_EachMissingInputIsRejected()
    {
        var actor = _fixture.CreateEodCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateEodCommand();
        var state = new FuturesEodDataCommandState { Id = command.Subject.ThreadId };

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
        var actor = _fixture.CreateEodCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(
            ActorType.Command, FuturesEodDataCommandActor.ActorName, "Unknown", "entity"));
        var state = new FuturesEodDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AValidEodCommand_When_ItIsValidated_Then_NoValidationFailureOccurs(bool vix)
    {
        var actor = _fixture.CreateEodCommandActor();
        var command = vix ? (ICommand)CreateVixCommand() : CreateEodCommand();

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AnEmptyCommandId_When_AnEodCommandIsValidated_Then_AValidationFailureOccurs(bool vix)
    {
        var actor = _fixture.CreateEodCommandActor();
        var command = vix ? (ICommand)CreateVixCommand(Guid.Empty) : CreateEodCommand(Guid.Empty);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_MissingValidationInputs_When_ACommandIsValidated_Then_EachMissingInputIsRejected()
    {
        var actor = _fixture.CreateEodCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateEodCommand();

        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(null!, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, default, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_ARepositoryState_When_ItIsLoadedAndSaved_Then_ExactValuesReachTheRepository()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateEodCommand();
        var state = new FuturesEodDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns(state);

        var loaded = await actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command);
        await actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, loaded, command);

        loaded.Should().BeSameAs(state);
        await repository.Received(1).LoadStateAsync(command);
        await repository.Received(1).SaveStateAsync(context, state, command);
    }

    [Fact]
    public async Task Given_RepositoryFailures_When_StateIsLoadedOrSaved_Then_TheyPropagate()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateEodCommand();
        var state = new FuturesEodDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns<ValueTask<FuturesEodDataCommandState>>(
            _ => throw new InvalidOperationException("load failed"));
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(
            _ => throw new InvalidOperationException("save failed"));

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_AKnownEodFailure_When_ItIsHandled_Then_TheTypedFailureEventIsSent(bool vix)
    {
        var actor = _fixture.CreateEodCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = vix ? (ICommand)CreateVixCommand() : CreateEodCommand();
        Exception exception = vix
            ? new InsertVixFuturesEodDataException("VIX insert failed")
            : new InsertFuturesEodDataException("EOD insert failed");

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, exception);

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        if (vix)
            await context.Received(1).SendAsync<VixFuturesEodDataInsertedFailEvent, FuturesEodDataId>(
                Arg.Is<VixFuturesEodDataInsertedFailEvent>(value =>
                    value.CommandId == command.CommandId && value.ErrorMessage == exception.Message));
        else
            await context.Received(1).SendAsync<FuturesEodDataInsertedFailEvent, FuturesEodDataId>(
                Arg.Is<FuturesEodDataInsertedFailEvent>(value =>
                    value.CommandId == command.CommandId && value.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task Given_AnUnexpectedEodFailure_When_ItIsHandled_Then_ACommandFailureEventIsSent()
    {
        var actor = _fixture.CreateEodCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateEodCommand();

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("unexpected failure"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.CommandExceptionEvent>(value =>
                value.CommandId == command.CommandId && value.ErrorMessage == "unexpected failure"));
    }

    [Fact]
    public async Task Given_AFailureWhileSendingTheTypedError_When_ItIsHandled_Then_TheGenericFallbackIsReturned()
    {
        var actor = _fixture.CreateEodCommandActor(logger: Substitute.For<ILogger<FuturesEodDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateEodCommand();
        context.SendAsync<FuturesEodDataInsertedFailEvent, FuturesEodDataId>(
                Arg.Any<FuturesEodDataInsertedFailEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("typed reply failed"));

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InsertFuturesEodDataException("insert failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>());
    }

    async Task<(TestableFuturesEodDataCommandActor Actor, ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesEodDataCommandState> Repository)> CreateActorWithRepository()
    {
        var actor = _fixture.CreateEodCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        return (actor, context, repository);
    }

    static InsertFuturesEodDataCommand CreateEodCommand(Guid? commandId = null)
    {
        var command = new InsertFuturesEodDataCommand(
            SampleData.ValueDate,
            SampleData.EsTickData,
            SampleData.EsContract,
            SampleData.EodDataToday,
            SampleData.EodDataRange,
            SampleData.NormCurveData,
            SampleData.WindowSize,
            SampleData.VixEodData);
        return command with
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesEodDataCommandActor.ActorName,
                InsertFuturesEodDataCommand.Verb, command.EntityId.Format())
        };
    }

    static InsertVixFuturesEodDataCommand CreateVixCommand(Guid? commandId = null)
    {
        var command = new InsertVixFuturesEodDataCommand(SampleData.VixTickData);
        return command with
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command, FuturesEodDataCommandActor.ActorName,
                InsertVixFuturesEodDataCommand.Verb, command.EntityId.Format())
        };
    }

    static NatsMsg<byte[]> CreateMessage(ICommand command)
        => new() { Subject = command.Subject.ToString(), Data = Serialize(command) };

    static byte[] Serialize(ICommand command)
        => command switch
        {
            InsertFuturesEodDataCommand value => ActorExtensions.DataSerializer!.Serialize(value),
            InsertVixFuturesEodDataCommand value => ActorExtensions.DataSerializer!.Serialize(value),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

    static FuturesEodDataId GetEntityId(ICommand command) => command switch
    {
        InsertFuturesEodDataCommand value => value.EntityId,
        InsertVixFuturesEodDataCommand value => value.EntityId,
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
