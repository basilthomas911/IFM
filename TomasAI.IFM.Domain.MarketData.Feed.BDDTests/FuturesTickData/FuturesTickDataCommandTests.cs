using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesTickData;

public class FuturesTickDataCommandTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesTickDataCommandTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Given_ATickRepository_When_TheActorStarts_Then_ItResolvesThatRepository()
    {
        var actor = _fixture.CreateTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesTickDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesTickDataCommandState>>().Returns(repository);

        await actor.InvokeOnStartup(context);

        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesTickDataCommandState>>();
    }

    [Fact]
    public async Task Given_NoContext_When_TheTickActorStarts_Then_StartupIsRejected()
    {
        var actor = _fixture.CreateTickCommandActor();

        Func<Task> act = () => actor.InvokeOnStartup(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Insert")]
    [InlineData("Start")]
    [InlineData("Stop")]
    public async Task Given_AValidTickCommandMessage_When_ItIsParsed_Then_ItIsPreservedAndLogged(string kind)
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
        database.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateTickCommandActor(database);
        var command = CreateCommand(kind);

        var parsed = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), CreateMessage(command));

        parsed.GetType().Should().Be(command.GetType());
        parsed.CommandId.Should().Be(command.CommandId);
        parsed.Subject.Should().Be(command.Subject);
        await database.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(command.CommandId.ToString())));
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesTickDataCommandActor.ActorName, InsertFuturesTickDataCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", InsertFuturesTickDataCommand.Verb)]
    [InlineData(ActorType.Command, FuturesTickDataCommandActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidTickCommandSubject_When_ItIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateTickCommandActor();
        var command = CreateCommand("Insert");
        var message = new NatsMsg<byte[]>
        {
            Subject = new ActorSubject(actorType, actorName, verb, GetEntityId(command)).ToString(),
            Data = Serialize(command)
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTickDataCommandActor.ActorName} command from message: *");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_InvalidTickCommandData_When_ItIsParsed_Then_DeserializationFails(bool empty)
    {
        var actor = _fixture.CreateTickCommandActor();
        var command = CreateCommand("Insert");
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = empty ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_TheCommandLogFails_When_ATickMessageIsParsed_Then_TheFailurePropagates()
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
        database.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("log failed")));
        var actor = _fixture.CreateTickCommandActor(database);

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(CreateCommand("Insert")));

        act.Should().Throw<InvalidOperationException>().WithMessage("log failed");
    }

    [Fact]
    public void Given_NoContext_When_ATickCommandIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateTickCommandActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateCommand("Insert")));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Insert", typeof(FuturesTickDataInsertedEvent))]
    [InlineData("Start", typeof(FuturesTickDataStreamingStartedEvent))]
    [InlineData("Stop", typeof(FuturesTickDataStreamingStoppedEvent))]
    public async Task Given_AValidTickCommand_When_ItIsReceived_Then_TheExpectedEventAndIdAreProduced(
        string kind, Type expectedEventType)
    {
        var actor = _fixture.CreateTickCommandActor();
        var command = CreateCommand(kind);
        var state = new FuturesTickDataCommandState { Id = command.Subject.ThreadId };

        var result = await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.Should().ContainSingle().Which.GetType().Should().Be(expectedEventType);
    }

    [Fact]
    public async Task Given_AllTickCommands_When_TheyAreReceived_Then_StateAccumulatesTheirEventsInOrder()
    {
        var actor = _fixture.CreateTickCommandActor();
        var commands = new[] { CreateCommand("Insert"), CreateCommand("Start"), CreateCommand("Stop") };
        var state = new FuturesTickDataCommandState { Id = commands[0].Subject.ThreadId };

        foreach (var command in commands)
            await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        state.Events.Select(value => value.GetType()).Should().ContainInOrder(
            typeof(FuturesTickDataInsertedEvent),
            typeof(FuturesTickDataStreamingStartedEvent),
            typeof(FuturesTickDataStreamingStoppedEvent));
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_ATickCommandIsReceived_Then_EachIsRejected()
    {
        var actor = _fixture.CreateTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Insert");
        var state = new FuturesTickDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, state, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedTickCommand_When_ItIsReceivedOrValidated_Then_ItIsRejected()
    {
        var actor = _fixture.CreateTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(
            ActorType.Command, FuturesTickDataCommandActor.ActorName, "Unknown", "entity"));
        var state = new FuturesTickDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Insert")]
    [InlineData("Start")]
    [InlineData("Stop")]
    public async Task Given_AValidTickCommand_When_ItIsValidated_Then_NoFailureOccurs(string kind)
    {
        var actor = _fixture.CreateTickCommandActor();
        var command = CreateCommand(kind);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("Insert")]
    [InlineData("Start")]
    [InlineData("Stop")]
    public async Task Given_AnEmptyCommandId_When_ATickCommandIsValidated_Then_ValidationFails(string kind)
    {
        var actor = _fixture.CreateTickCommandActor();
        var command = CreateCommand(kind, Guid.Empty);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Theory]
    [InlineData("InsertTick")]
    [InlineData("InsertContract")]
    [InlineData("StartDate")]
    [InlineData("StartContract")]
    public async Task Given_InvalidTickDetails_When_TheCommandIsValidated_Then_ValidationFails(string kind)
    {
        var actor = _fixture.CreateTickCommandActor();
        var command = CreateInvalidCommand(kind);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_MissingValidationInputs_When_ATickCommandIsValidated_Then_EachIsRejected()
    {
        var actor = _fixture.CreateTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Insert");

        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(null!, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, default, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_RepositoryState_When_ItIsLoadedAndSaved_Then_ExactValuesReachTheRepository()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand("Insert");
        var state = new FuturesTickDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns(state);

        var loaded = await actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command);
        await actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, loaded, command);

        loaded.Should().BeSameAs(state);
        await repository.Received(1).LoadStateAsync(command);
        await repository.Received(1).SaveStateAsync(context, state, command);
    }

    [Fact]
    public async Task Given_RepositoryFailures_When_TickStateIsLoadedOrSaved_Then_TheyPropagate()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand("Insert");
        var state = new FuturesTickDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns<ValueTask<FuturesTickDataCommandState>>(
            _ => throw new InvalidOperationException("load failed"));
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(
            _ => throw new InvalidOperationException("save failed"));

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Fact]
    public async Task Given_MissingStateInputs_When_TickStateIsLoadedOrSaved_Then_EachIsRejected()
    {
        var (actor, context, _) = await CreateActorWithRepository();
        var command = CreateCommand("Insert");
        var state = new FuturesTickDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(null!, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, default, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, null!, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_ATickFailure_When_ItIsHandled_Then_ACommandFailureEventIsSent()
    {
        var actor = _fixture.CreateTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Insert");

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("tick failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.CommandExceptionEvent>(value =>
                value.CommandId == command.CommandId && value.ErrorMessage == "tick failed"));
    }

    [Fact]
    public async Task Given_FirstErrorDeliveryFails_When_TheFailureIsRetried_Then_AFailedResultReturns()
    {
        var actor = _fixture.CreateTickCommandActor(logger: Substitute.For<ILogger<FuturesTickDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Insert");
        var attempts = 0;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(_ => ++attempts == 1
                ? ValueTask.FromException(new InvalidOperationException("first delivery failed"))
                : ValueTask.CompletedTask);

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new Exception("original failure"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task Given_ErrorDeliveryAlwaysFails_When_TheFailureIsHandled_Then_AFailedResultStillReturns()
    {
        var actor = _fixture.CreateTickCommandActor(logger: Substitute.For<ILogger<FuturesTickDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Insert");
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
                Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("delivery failed"));

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new Exception("original failure"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await context.Received(2).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>());
    }

    async Task<(TestableFuturesTickDataCommandActor Actor, ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesTickDataCommandState> Repository)> CreateActorWithRepository()
    {
        var actor = _fixture.CreateTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesTickDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesTickDataCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        return (actor, context, repository);
    }

    static ICommand CreateCommand(string kind, Guid? commandId = null)
    {
        ICommand command = kind switch
        {
            "Insert" => new InsertFuturesTickDataCommand(SampleData.EsContract, SampleData.EsTickData),
            "Start" => new StartFuturesTickDataStreamingCommand(SampleData.EsContract, SampleData.ValueDate, true),
            "Stop" => new StopFuturesTickDataStreamingCommand(SampleData.EsContract.ContractId, SampleData.ValueDate),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return SetRouting(command, commandId ?? Guid.NewGuid());
    }

    static ICommand CreateInvalidCommand(string kind)
    {
        ICommand command = kind switch
        {
            "InsertTick" => new InsertFuturesTickDataCommand(
                SampleData.EsContract, SampleData.EsTickData with { TickId = 0 }),
            "InsertContract" => new InsertFuturesTickDataCommand(
                SampleData.EsContract with { ContractId = string.Empty }, SampleData.EsTickData),
            "StartDate" => new StartFuturesTickDataStreamingCommand(
                SampleData.EsContract, DateOnly.MinValue, false),
            "StartContract" => new StartFuturesTickDataStreamingCommand(
                SampleData.EsContract with { Symbol = string.Empty }, SampleData.ValueDate, false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return SetRouting(command, Guid.NewGuid());
    }

    static ICommand SetRouting(ICommand command, Guid commandId) => command switch
    {
        InsertFuturesTickDataCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(InsertFuturesTickDataCommand.Verb, value.EntityId.Format())
        },
        StartFuturesTickDataStreamingCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(StartFuturesTickDataStreamingCommand.Verb, value.EntityId.Format())
        },
        StopFuturesTickDataStreamingCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(StopFuturesTickDataStreamingCommand.Verb, value.EntityId.Format())
        },
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static ActorSubject CreateSubject(string verb, string entityId)
        => new(ActorType.Command, FuturesTickDataCommandActor.ActorName, verb, entityId);

    static NatsMsg<byte[]> CreateMessage(ICommand command)
        => new() { Subject = command.Subject.ToString(), Data = Serialize(command) };

    static byte[] Serialize(ICommand command) => command switch
    {
        InsertFuturesTickDataCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        StartFuturesTickDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        StopFuturesTickDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static string GetEntityId(ICommand command) => command switch
    {
        InsertFuturesTickDataCommand value => value.EntityId.Format(),
        StartFuturesTickDataStreamingCommand value => value.EntityId.Format(),
        StopFuturesTickDataStreamingCommand value => value.EntityId.Format(),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
