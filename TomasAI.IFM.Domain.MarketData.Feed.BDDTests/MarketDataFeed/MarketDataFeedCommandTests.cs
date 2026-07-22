using FluentAssertions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.MarketDataFeed;

public class MarketDataFeedCommandTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public MarketDataFeedCommandTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    public static TheoryData<string> CommandKinds => new()
    {
        "Start", "Stop", "Reset", "Add", "Remove", "TurnOn", "TurnOff", "Delete", "Halt"
    };

    [Fact]
    public async Task Given_ARepository_When_TheActorStarts_Then_ItResolvesTheMarketDataFeedRepository()
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<MarketDataFeedCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<MarketDataFeedCommandState>>().Returns(repository);

        await actor.InvokeOnStartup(context);

        container.Received(1).Resolve<IEventSourceActorStateRepository<MarketDataFeedCommandState>>();
    }

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public async Task Given_AValidMarketDataFeedCommandMessage_When_Parsed_Then_ItIsPreservedAndLogged(string kind)
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
        database.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>()).Returns(Task.CompletedTask);
        var actor = _fixture.CreateMarketDataFeedCommandActor(database);
        var command = CreateCommand(kind);

        var parsed = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), CreateMessage(command));

        parsed.GetType().Should().Be(command.GetType());
        parsed.CommandId.Should().Be(command.CommandId);
        parsed.Subject.Should().Be(command.Subject);
        await database.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(), Arg.Is<string>(value => value.Contains(command.CommandId.ToString())));
    }

    [Theory]
    [InlineData(ActorType.Query, MarketDataFeedCommandActor.ActorName, StartMarketDataFeedCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", StartMarketDataFeedCommand.Verb)]
    [InlineData(ActorType.Command, MarketDataFeedCommandActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidCommandSubject_When_Parsed_Then_ItIsRejected(ActorType type, string name, string verb)
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var command = CreateCommand("Start");
        var message = new NatsMsg<byte[]>
        {
            Subject = new ActorSubject(type, name, verb, "entity").ToString(),
            Data = Serialize(command)
        };

        var act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Given_CorruptData_When_Parsed_Then_DeserializationFails()
    {
        var command = CreateCommand("Start");
        var message = new NatsMsg<byte[]> { Subject = command.Subject.ToString(), Data = [0, 1, 255] };

        var act = () => _fixture.CreateMarketDataFeedCommandActor()
            .InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_ACommandLogFailure_When_Parsed_Then_TheFailurePropagates()
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
        database.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("log failed")));

        var act = () => _fixture.CreateMarketDataFeedCommandActor(database)
            .InvokeParseMessage(Substitute.For<ICommandActorContext>(), CreateMessage(CreateCommand("Start")));

        act.Should().Throw<InvalidOperationException>().WithMessage("log failed");
    }

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public async Task Given_AValidCommand_When_Received_Then_ItReturnsItsCommandId(string kind)
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var command = CreateCommand(kind);
        var state = new MarketDataFeedCommandState { Id = command.Subject.ThreadId };
        if (kind is "Remove" or "TurnOff" or "Halt")
            await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, CreateCommand("TurnOn"));

        var result = await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
    }

    [Fact]
    public async Task Given_TheCompleteFeedLifecycle_When_CommandsAreReceived_Then_EventsAccumulateInOrder()
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var commands = new[] { "Start", "Reset", "Stop", "Add", "TurnOn", "Remove" }
            .Select(kind => CreateCommand(kind)).ToArray();
        var state = new MarketDataFeedCommandState { Id = commands[0].Subject.ThreadId };

        foreach (var command in commands)
            await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        state.Events.Select(value => value.GetType()).Should().ContainInOrder(
            typeof(MarketDataFeedStartedEvent), typeof(MarketDataFeedResetEvent), typeof(MarketDataFeedStoppedEvent),
            typeof(TradeLiveFeedAddedEvent), typeof(TradeLiveFeedTurnedOnEvent), typeof(TradeLiveFeedRemovedEvent));
    }

    [Fact]
    public async Task Given_TradeFeedIsOff_When_TurnOffOrHaltIsReceived_Then_TheEdgeCaseIsRejected()
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var state = new MarketDataFeedCommandState { Id = CreateCommand("TurnOff").Subject.ThreadId };

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, CreateCommand("TurnOff")).AsTask()))
            .Should().ThrowAsync<ApplicationException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, CreateCommand("Halt")).AsTask()))
            .Should().ThrowAsync<ApplicationException>();
    }

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public async Task Given_AValidCommand_When_Validated_Then_NoFailureOccurs(string kind)
    {
        var command = CreateCommand(kind);

        var act = () => _fixture.CreateMarketDataFeedCommandActor().InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public async Task Given_AnEmptyCommandId_When_Validated_Then_ValidationFails(string kind)
    {
        var command = CreateCommand(kind, Guid.Empty);

        var act = () => _fixture.CreateMarketDataFeedCommandActor().InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_InvalidBusinessFields_When_Validated_Then_ValidationFails()
    {
        var invalid = Route(new AddTradeLiveFeedCommand(0, 0, DateOnly.MinValue), Guid.NewGuid());

        var act = () => _fixture.CreateMarketDataFeedCommandActor().InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), invalid.Subject.ThreadId, invalid).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_UnsupportedOrMissingInputs_When_ReceivedOrValidated_Then_TheyAreRejected()
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Start");
        var state = new MarketDataFeedCommandState { Id = command.Subject.ThreadId };
        var unsupported = Substitute.For<ICommand>();
        unsupported.Subject.Returns(new ActorSubject(ActorType.Command, MarketDataFeedCommandActor.ActorName, "Unknown", "entity"));

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, state, command).AsTask())).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!, command).AsTask())).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, null!).AsTask())).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, unsupported).AsTask())).Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, unsupported.Subject.ThreadId, unsupported).AsTask())).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Given_PersistedState_When_LoadedAndSaved_Then_TheRepositoryReceivesExactValues()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand("Start");
        var state = new MarketDataFeedCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns(state);

        var loaded = await actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command);
        await actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, loaded, command);

        loaded.Should().BeSameAs(state);
        await repository.Received(1).LoadStateAsync(command);
        await repository.Received(1).SaveStateAsync(context, state, command);
    }

    [Fact]
    public async Task Given_RepositoryFailures_When_LoadingOrSaving_Then_TheyPropagate()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand("Start");
        var state = new MarketDataFeedCommandState { Id = command.Subject.ThreadId };
        repository.LoadStateAsync(command).Returns<ValueTask<MarketDataFeedCommandState>>(_ => throw new InvalidOperationException("load failed"));
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(_ => throw new InvalidOperationException("save failed"));

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Fact]
    public async Task Given_ACommandFailure_When_Handled_Then_AFailureResultAndErrorEventAreProduced()
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Start");

        var result = await actor.InvokeOnExceptionAsync(context, command.Subject.ThreadId, command, new Exception("feed failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.CommandExceptionEvent>(value => value.ErrorMessage == "feed failed"));
    }

    async Task<(TestableMarketDataFeedCommandActor Actor, ICommandActorContext Context,
        IEventSourceActorStateRepository<MarketDataFeedCommandState> Repository)> CreateActorWithRepository()
    {
        var actor = _fixture.CreateMarketDataFeedCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<MarketDataFeedCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<MarketDataFeedCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        return (actor, context, repository);
    }

    static ICommand CreateCommand(string kind, Guid? commandId = null)
    {
        ICommand command = kind switch
        {
            "Start" => new StartMarketDataFeedCommand(SampleData.FuturesContracts, SampleData.ValueDate, true),
            "Stop" => new StopMarketDataFeedCommand(SampleData.ValueDate),
            "Reset" => new ResetMarketDataFeedCommand(SampleData.FuturesContracts, SampleData.ValueDate),
            "Add" => new AddTradeLiveFeedCommand(SampleData.OrderId, SampleData.TradeId, SampleData.ValueDate),
            "Remove" => new RemoveTradeLiveFeedCommand(SampleData.OrderId, SampleData.TradeId, SampleData.ValueDate),
            "TurnOn" => new TurnTradeLiveFeedOnCommand(SampleData.OrderId, SampleData.TradeId, SampleData.ValueDate),
            "TurnOff" => new TurnTradeLiveFeedOffCommand(SampleData.OrderId, SampleData.TradeId, SampleData.ValueDate),
            "Delete" => new DeleteStreamingRequestIdCommand(SampleData.StreamingFeedId),
            "Halt" => new HaltTradeLiveFeedCommand(SampleData.OrderId, SampleData.TradeId),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return Route(command, commandId ?? Guid.NewGuid());
    }

    static ICommand Route(ICommand command, Guid commandId) => command switch
    {
        StartMarketDataFeedCommand value => value with { CommandId = commandId, Subject = Subject(StartMarketDataFeedCommand.Verb, value.EntityId.Format()) },
        StopMarketDataFeedCommand value => value with { CommandId = commandId, Subject = Subject(StopMarketDataFeedCommand.Verb, value.EntityId.Format()) },
        ResetMarketDataFeedCommand value => value with { CommandId = commandId, Subject = Subject(ResetMarketDataFeedCommand.Verb, value.EntityId.Format()) },
        AddTradeLiveFeedCommand value => value with { CommandId = commandId, Subject = Subject(AddTradeLiveFeedCommand.Verb, value.EntityId.Format()) },
        RemoveTradeLiveFeedCommand value => value with { CommandId = commandId, Subject = Subject(RemoveTradeLiveFeedCommand.Verb, value.EntityId.Format()) },
        TurnTradeLiveFeedOnCommand value => value with { CommandId = commandId, Subject = Subject(TurnTradeLiveFeedOnCommand.Verb, value.EntityId.Format()) },
        TurnTradeLiveFeedOffCommand value => value with { CommandId = commandId, Subject = Subject(TurnTradeLiveFeedOffCommand.Verb, value.EntityId.Format()) },
        DeleteStreamingRequestIdCommand value => value with { CommandId = commandId, Subject = Subject(DeleteStreamingRequestIdCommand.Verb, value.EntityId.Format()) },
        HaltTradeLiveFeedCommand value => value with { CommandId = commandId, Subject = Subject(HaltTradeLiveFeedCommand.Verb, value.EntityId.Format()) },
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static ActorSubject Subject(string verb, string entityId)
        => new(ActorType.Command, MarketDataFeedCommandActor.ActorName, verb, entityId);

    static NatsMsg<byte[]> CreateMessage(ICommand command)
        => new() { Subject = command.Subject.ToString(), Data = Serialize(command) };

    static byte[] Serialize(ICommand command) => command switch
    {
        StartMarketDataFeedCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        StopMarketDataFeedCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        ResetMarketDataFeedCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        AddTradeLiveFeedCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        RemoveTradeLiveFeedCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        TurnTradeLiveFeedOnCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        TurnTradeLiveFeedOffCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        DeleteStreamingRequestIdCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        HaltTradeLiveFeedCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
