using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Reference.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesOptionTickData;

public class FuturesOptionTickDataCommandTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesOptionTickDataCommandTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Given_AnOptionTickRepository_When_TheActorStarts_Then_ItResolvesThatRepository()
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repository);

        await actor.InvokeOnStartup(context);

        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();
    }

    [Fact]
    public async Task Given_NoContext_When_TheActorStarts_Then_StartupIsRejected()
    {
        var actor = _fixture.CreateOptionTickCommandActor();

        Func<Task> act = () => actor.InvokeOnStartup(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Insert")]
    [InlineData("Start")]
    [InlineData("Stop")]
    public async Task Given_AValidOptionTickCommandMessage_When_ItIsParsed_Then_ItIsPreservedAndLogged(string kind)
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateOptionTickCommandActor(db);
        var command = CreateCommand(kind);

        var parsed = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), CreateMessage(command));

        parsed.GetType().Should().Be(command.GetType());
        parsed.CommandId.Should().Be(command.CommandId);
        parsed.Subject.Should().Be(command.Subject);
        await db.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(command.CommandId.ToString())));
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesOptionTickDataCommandActor.ActorName, InsertFuturesOptionTickDataCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", InsertFuturesOptionTickDataCommand.Verb)]
    [InlineData(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidOptionTickCommandSubject_When_ItIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var command = CreateCommand("Insert");
        var message = new NatsMsg<byte[]>
        {
            Subject = new ActorSubject(actorType, actorName, verb, GetEntityId(command)).ToString(),
            Data = Serialize(command)
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataCommandActor.ActorName} command from message: *");
    }

    [Fact]
    public void Given_CorruptOptionTickCommandData_When_ItIsParsed_Then_DeserializationFails()
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var command = CreateCommand("Insert");
        var message = new NatsMsg<byte[]> { Subject = command.Subject.ToString(), Data = [0x00, 0x01, 0xFF] };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_TheCommandLogFails_When_AnOptionTickMessageIsParsed_Then_TheFailurePropagates()
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("log failed")));
        var actor = _fixture.CreateOptionTickCommandActor(db);

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(CreateCommand("Insert")));

        act.Should().Throw<InvalidOperationException>().WithMessage("log failed");
    }

    [Fact]
    public void Given_NoContext_When_AnOptionTickCommandIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateOptionTickCommandActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateCommand("Insert")));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Insert", typeof(FuturesOptionTickDataInsertedEvent))]
    [InlineData("Start", typeof(FuturesOptionTickDataStreamingStartedEvent))]
    [InlineData("Stop", typeof(FuturesOptionTickDataStreamingStoppedEvent))]
    public async Task Given_AValidOptionTickCommand_When_ItIsReceived_Then_TheExpectedEventAndIdAreProduced(
        string kind, Type expectedEventType)
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var command = CreateCommand(kind);
        var state = new FuturesOptionTickDataCommandState { Id = command.Subject.ThreadId };

        var result = await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.Should().ContainSingle().Which.GetType().Should().Be(expectedEventType);
    }

    [Fact]
    public async Task Given_AllOptionTickCommands_When_TheyAreReceived_Then_StateAccumulatesTheirEvents()
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var commands = new[] { CreateCommand("Insert"), CreateCommand("Start"), CreateCommand("Stop") };
        var state = new FuturesOptionTickDataCommandState { Id = commands[0].Subject.ThreadId };

        foreach (var command in commands)
            await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        state.Events.Should().HaveCount(3);
        state.Events.Select(value => value.GetType()).Should().ContainInOrder(
            typeof(FuturesOptionTickDataInsertedEvent),
            typeof(FuturesOptionTickDataStreamingStartedEvent),
            typeof(FuturesOptionTickDataStreamingStoppedEvent));
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_AnOptionTickCommandIsReceived_Then_EachIsRejected()
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Insert");
        var state = new FuturesOptionTickDataCommandState { Id = command.Subject.ThreadId };

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
        var actor = _fixture.CreateOptionTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(
            ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "Unknown", "entity"));
        var state = new FuturesOptionTickDataCommandState { Id = command.Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Insert")]
    [InlineData("Start")]
    [InlineData("Stop")]
    public async Task Given_AValidOptionTickCommand_When_ItIsValidated_Then_NoFailureOccurs(string kind)
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var command = CreateCommand(kind);
        var context = CreateValidationContext();

        Func<Task> act = () => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("Insert")]
    [InlineData("Start")]
    [InlineData("Stop")]
    public async Task Given_AnEmptyCommandId_When_AnOptionTickCommandIsValidated_Then_ValidationFails(string kind)
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var command = CreateCommand(kind, Guid.Empty);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            CreateValidationContext(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Theory]
    [InlineData("StartRisk")]
    [InlineData("StartDate")]
    [InlineData("StopContract")]
    public async Task Given_InvalidOptionTickDetails_When_TheCommandIsValidated_Then_ValidationFails(string kind)
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var command = CreateInvalidCommand(kind);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            CreateValidationContext(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_MissingValidationInputs_When_AnOptionTickCommandIsValidated_Then_EachIsRejected()
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var context = CreateValidationContext();
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
        var state = new FuturesOptionTickDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns(state);

        var loaded = await actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command);
        await actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, loaded, command);

        loaded.Should().BeSameAs(state);
        await repository.Received(1).LoadStateAsync(command);
        await repository.Received(1).SaveStateAsync(context, state, command);
    }

    [Fact]
    public async Task Given_RepositoryFailures_When_OptionTickStateIsLoadedOrSaved_Then_TheyPropagate()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand("Insert");
        var state = new FuturesOptionTickDataCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns<ValueTask<FuturesOptionTickDataCommandState>>(
            _ => throw new InvalidOperationException("load failed"));
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(
            _ => throw new InvalidOperationException("save failed"));

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Fact]
    public async Task Given_AnOptionTickFailure_When_ItIsHandled_Then_ACommandFailureEventIsSent()
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Insert");

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("option tick failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.CommandExceptionEvent>(value =>
                value.CommandId == command.CommandId && value.ErrorMessage == "option tick failed"));
    }

    [Fact]
    public async Task Given_ErrorEventDeliveryFailsTwice_When_TheFailureIsHandled_Then_AFailedResultStillReturns()
    {
        var actor = _fixture.CreateOptionTickCommandActor(logger: Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>());
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

    async Task<(TestableFuturesOptionTickDataCommandActor Actor, ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesOptionTickDataCommandState> Repository)> CreateActorWithRepository()
    {
        var actor = _fixture.CreateOptionTickCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        return (actor, context, repository);
    }

    static ICommandActorContext CreateValidationContext()
    {
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var lookup = Substitute.For<IReferenceLookupService>();
        var contract = SampleData.FuturesOptionContracts[0];
        lookup.SymbolExists(contract.Symbol).Returns(true);
        lookup.CurrencyExists(contract.Currency).Returns(true);
        lookup.ExchangeExists(contract.Exchange).Returns(true);
        lookup.MultiplierExists(contract.Multiplier).Returns(true);
        container.Resolve<IReferenceLookupService>().Returns(lookup);
        context.Container.Returns(container);
        return context;
    }

    static ICommand CreateCommand(string kind, Guid? commandId = null)
    {
        ICommand command = kind switch
        {
            "Insert" => new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData),
            "Start" => new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, SampleData.RiskFreeRate),
            "Stop" => new StopFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0].ContractId),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return SetRouting(command, commandId ?? Guid.NewGuid());
    }

    static ICommand CreateInvalidCommand(string kind)
    {
        ICommand command = kind switch
        {
            "StartRisk" => new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, double.NaN),
            "StartDate" => new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                DateOnly.MinValue, DateOnly.MinValue, SampleData.RiskFreeRate),
            "StopContract" => new StopFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, string.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return SetRouting(command, Guid.NewGuid());
    }

    static ICommand SetRouting(ICommand command, Guid commandId) => command switch
    {
        InsertFuturesOptionTickDataCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(InsertFuturesOptionTickDataCommand.Verb, value.EntityId.Format())
        },
        StartFuturesOptionTickDataStreamingCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(StartFuturesOptionTickDataStreamingCommand.Verb, value.EntityId.Format())
        },
        StopFuturesOptionTickDataStreamingCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(StopFuturesOptionTickDataStreamingCommand.Verb, value.EntityId.Format())
        },
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static ActorSubject CreateSubject(string verb, string entityId)
        => new(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, verb, entityId);

    static NatsMsg<byte[]> CreateMessage(ICommand command)
        => new() { Subject = command.Subject.ToString(), Data = Serialize(command) };

    static byte[] Serialize(ICommand command) => command switch
    {
        InsertFuturesOptionTickDataCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        StartFuturesOptionTickDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        StopFuturesOptionTickDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static string GetEntityId(ICommand command) => command switch
    {
        InsertFuturesOptionTickDataCommand value => value.EntityId.Format(),
        StartFuturesOptionTickDataStreamingCommand value => value.EntityId.Format(),
        StopFuturesOptionTickDataStreamingCommand value => value.EntityId.Format(),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
