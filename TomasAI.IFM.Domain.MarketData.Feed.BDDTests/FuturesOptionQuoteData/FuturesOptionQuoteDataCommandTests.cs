using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Reference.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesOptionQuoteData;

public class FuturesOptionQuoteDataCommandTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesOptionQuoteDataCommandTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Given_AnOptionQuoteRepository_When_TheActorStarts_Then_ItResolvesThatRepository()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repository);

        await actor.InvokeOnStartup(context);

        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();
    }

    [Fact]
    public async Task Given_NoContext_When_TheOptionQuoteActorStarts_Then_StartupIsRejected()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();

        Func<Task> act = () => actor.InvokeOnStartup(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Stop")]
    [InlineData("Insert")]
    public async Task Given_AValidOptionQuoteCommandMessage_When_Parsed_Then_ItIsPreservedAndLogged(string kind)
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
        database.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = _fixture.CreateOptionQuoteCommandActor(dbEventSource: database);
        var command = CreateCommand(kind);

        var parsed = actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), CreateMessage(command));

        parsed.GetType().Should().Be(command.GetType());
        parsed.CommandId.Should().Be(command.CommandId);
        parsed.Subject.Should().Be(command.Subject);
        GetQuoteId(parsed).Should().Be(SampleData.OptionQuoteStreamId);
        await database.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(value => value.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(command.CommandId.ToString())));
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesOptionQuoteDataCommandActor.ActorName, StartFuturesOptionQuoteDataStreamingCommand.Verb)]
    [InlineData(ActorType.Command, "WrongActor", StartFuturesOptionQuoteDataStreamingCommand.Verb)]
    [InlineData(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidOptionQuoteSubject_When_Parsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var command = CreateCommand("Start");
        var subject = new ActorSubject(actorType, actorName, verb, command.Subject.EntityId);
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(command) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionQuoteDataCommandActor.ActorName} command from message: *");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_InvalidOptionQuotePayload_When_Parsed_Then_DeserializationFails(bool empty)
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var command = CreateCommand("Start");
        var message = new NatsMsg<byte[]>
        {
            Subject = command.Subject.ToString(),
            Data = empty ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<ICommandActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_NoContext_When_AnOptionQuoteMessageIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateCommand("Start")));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Given_CommandLoggingFails_When_AnOptionQuoteMessageIsParsed_Then_TheFailurePropagates()
    {
        var database = Substitute.For<IEventSourceActorDbContext>();
        database.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("log failed")));
        var actor = _fixture.CreateOptionQuoteCommandActor(dbEventSource: database);

        Action act = () => actor.InvokeParseMessage(
            Substitute.For<ICommandActorContext>(), CreateMessage(CreateCommand("Start")));

        act.Should().Throw<InvalidOperationException>().WithMessage("log failed");
    }

    [Fact]
    public async Task Given_AValidStartCommand_When_Received_Then_AStartedEventAndCommandIdAreProduced()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var command = CreateCommand("Start");
        var state = CreateState(command.Subject.ThreadId);

        var result = await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        var started = state.Events.Should().ContainSingle().Which
            .Should().BeOfType<FuturesOptionQuoteDataStreamingStartedEvent>().Which;
        started.CommandId.Should().Be(command.CommandId);
        started.QuoteId.Should().Be(SampleData.OptionQuoteStreamId);
        started.FuturesOptionQuotes.Should().BeEquivalentTo(SampleData.FuturesOptionQuotes);
    }

    [Theory]
    [InlineData("Stop", typeof(StopFuturesOptionQuoteDataStreamingException))]
    [InlineData("Insert", typeof(InsertFuturesOptionQuoteDataException))]
    public async Task Given_NoActiveQuoteStream_When_StopOrInsertIsReceived_Then_TheDomainFailureIsRaised(
        string kind, Type expectedException)
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var command = CreateCommand(kind);
        var state = CreateState(command.Subject.ThreadId);

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<ICommandActorContext>(), state, command).AsTask();

        (await act.Should().ThrowAsync<Exception>()).Which.GetType().Should().Be(expectedException);
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_AnOptionQuoteCommandIsReceived_Then_EachIsRejected()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Start");
        var state = CreateState(command.Subject.ThreadId);

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, state, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedCommand_When_ReceivedOrValidated_Then_ItIsRejected()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = Substitute.For<ICommand>();
        command.Subject.Returns(new ActorSubject(
            ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "Unknown", "entity"));
        var state = CreateState(command.Subject.ThreadId);

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Stop")]
    [InlineData("Insert")]
    public async Task Given_AValidOptionQuoteCommand_When_Validated_Then_NoFailureOccurs(string kind)
    {
        var actor = _fixture.CreateOptionQuoteCommandActor(CreateReferenceLookup());
        var command = CreateCommand(kind);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Stop")]
    [InlineData("Insert")]
    public async Task Given_AnEmptyCommandId_When_Validated_Then_AValidationFailureOccurs(string kind)
    {
        var actor = _fixture.CreateOptionQuoteCommandActor(CreateReferenceLookup());
        var command = CreateCommand(kind, Guid.Empty);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Theory]
    [InlineData("StartQuoteId")]
    [InlineData("StartQuotes")]
    [InlineData("StartContracts")]
    [InlineData("StopQuoteId")]
    [InlineData("InsertContract")]
    [InlineData("InsertQuoteId")]
    public async Task Given_InvalidOptionQuoteDetails_When_Validated_Then_AValidationFailureOccurs(string kind)
    {
        var actor = _fixture.CreateOptionQuoteCommandActor(CreateReferenceLookup());
        var command = CreateInvalidCommand(kind);

        Func<Task> act = () => actor.InvokeOnValidateAsync(
            Substitute.For<ICommandActorContext>(), command.Subject.ThreadId, command).AsTask();

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task Given_MissingValidationInputs_When_Validated_Then_EachIsRejected()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Start");

        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(null!, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, default, command).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnValidateAsync(context, command.Subject.ThreadId, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_RepositoryState_When_LoadedAndSaved_Then_ExactValuesReachTheRepository()
    {
        var (actor, context, repository) = await CreateActorWithRepository();
        var command = CreateCommand("Start");
        var state = CreateState(command.Subject.ThreadId);
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
        var command = CreateCommand("Start");
        var state = CreateState(command.Subject.ThreadId);
        repository.LoadStateAsync(command).Returns<ValueTask<FuturesOptionQuoteDataCommandState>>(
            _ => throw new InvalidOperationException("load failed"));
        repository.SaveStateAsync(context, state, command).Returns<ValueTask>(
            _ => throw new InvalidOperationException("save failed"));

        await ((Func<Task>)(() => actor.InvokeOnLoadStateAsync(context, command.Subject.ThreadId, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        await ((Func<Task>)(() => actor.InvokeOnSaveStateAsync(context, command.Subject.ThreadId, state, command).AsTask()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Stop")]
    [InlineData("Insert")]
    public async Task Given_AKnownOptionQuoteFailure_When_Handled_Then_TheTypedErrorEventIsSent(string kind)
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand(kind);
        Exception exception = kind switch
        {
            "Start" => new StartFuturesOptionQuoteDataStreamingException("start failed"),
            "Stop" => new StopFuturesOptionQuoteDataStreamingException("stop failed"),
            _ => new InsertFuturesOptionQuoteDataException("insert failed")
        };

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, exception);

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await VerifyTypedFailure(context, command, exception.Message);
    }

    [Fact]
    public async Task Given_AnUnexpectedOptionQuoteFailure_When_Handled_Then_AGenericFailureEventIsSent()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Start");

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command, new InvalidOperationException("unexpected failure"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.CommandExceptionEvent>(value =>
                value.CommandId == command.CommandId && value.ErrorMessage == "unexpected failure"));
    }

    [Fact]
    public async Task Given_ErrorEventDeliveryFails_When_TheFailureIsHandled_Then_TheGenericFallbackIsReturned()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor(
            logger: Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>());
        var context = Substitute.For<ICommandActorContext>();
        var command = CreateCommand("Start");
        context.SendAsync<FuturesOptionQuoteDataStreamingStartedFailEvent, QuoteId>(
                Arg.Any<FuturesOptionQuoteDataStreamingStartedFailEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("typed delivery failed"));

        var result = await actor.InvokeOnExceptionAsync(
            context, command.Subject.ThreadId, command,
            new StartFuturesOptionQuoteDataStreamingException("start failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>());
    }

    async Task<(TestableFuturesOptionQuoteDataCommandActor Actor, ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState> Repository)> CreateActorWithRepository()
    {
        var actor = _fixture.CreateOptionQuoteCommandActor();
        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();
        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repository);
        await actor.InvokeOnStartup(context);
        return (actor, context, repository);
    }

    static FuturesOptionQuoteDataCommandState CreateState(ActorThreadId threadId)
    {
        var blackboard = Substitute.For<IBlackboardService>();
        blackboard.FuturesOptionQuoteData.Returns(new FuturesOptionQuoteDataModel());
        return new FuturesOptionQuoteDataCommandState(blackboard) { Id = threadId };
    }

    static IReferenceLookupService CreateReferenceLookup()
    {
        var lookup = Substitute.For<IReferenceLookupService>();
        foreach (var contract in SampleData.FuturesOptionContracts)
        {
            lookup.SymbolExists(contract.Symbol).Returns(true);
            lookup.CurrencyExists(contract.Currency).Returns(true);
            lookup.ExchangeExists(contract.Exchange).Returns(true);
            lookup.MultiplierExists(contract.Multiplier).Returns(true);
        }
        return lookup;
    }

    static ICommand CreateCommand(string kind, Guid? commandId = null)
    {
        ICommand command = kind switch
        {
            "Start" => new StartFuturesOptionQuoteDataStreamingCommand(
                SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts),
            "Stop" => new StopFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId),
            "Insert" => new InsertFuturesOptionQuoteDataCommand(
                SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes[0].ContractId, SampleData.AskPriceQuoteData),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return SetRouting(command, commandId ?? Guid.NewGuid());
    }

    static ICommand CreateInvalidCommand(string kind)
    {
        ICommand command = kind switch
        {
            "StartQuoteId" => new StartFuturesOptionQuoteDataStreamingCommand(
                0, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts),
            "StartQuotes" => new StartFuturesOptionQuoteDataStreamingCommand(
                SampleData.OptionQuoteStreamId, [], SampleData.FuturesOptionContracts),
            "StartContracts" => new StartFuturesOptionQuoteDataStreamingCommand(
                SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, []),
            "StopQuoteId" => new StopFuturesOptionQuoteDataStreamingCommand(0),
            "InsertContract" => new InsertFuturesOptionQuoteDataCommand(
                SampleData.OptionQuoteStreamId, string.Empty, SampleData.AskPriceQuoteData),
            "InsertQuoteId" => new InsertFuturesOptionQuoteDataCommand(
                0, SampleData.FuturesOptionQuotes[0].ContractId, SampleData.AskPriceQuoteData),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return SetRouting(command, Guid.NewGuid());
    }

    static ICommand SetRouting(ICommand command, Guid commandId) => command switch
    {
        StartFuturesOptionQuoteDataStreamingCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(StartFuturesOptionQuoteDataStreamingCommand.Verb, value.EntityId.Format())
        },
        StopFuturesOptionQuoteDataStreamingCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(StopFuturesOptionQuoteDataStreamingCommand.Verb, value.EntityId.Format())
        },
        InsertFuturesOptionQuoteDataCommand value => value with
        {
            CommandId = commandId,
            Subject = CreateSubject(InsertFuturesOptionQuoteDataCommand.Verb, value.EntityId.Format())
        },
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static ActorSubject CreateSubject(string verb, string entityId)
        => new(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, verb, entityId);

    static int GetQuoteId(ICommand command) => command switch
    {
        StartFuturesOptionQuoteDataStreamingCommand value => value.QuoteId,
        StopFuturesOptionQuoteDataStreamingCommand value => value.QuoteId,
        InsertFuturesOptionQuoteDataCommand value => value.QuoteId,
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static NatsMsg<byte[]> CreateMessage(ICommand command)
        => new() { Subject = command.Subject.ToString(), Data = Serialize(command) };

    static byte[] Serialize(ICommand command) => command switch
    {
        StartFuturesOptionQuoteDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        StopFuturesOptionQuoteDataStreamingCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        InsertFuturesOptionQuoteDataCommand value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static async Task VerifyTypedFailure(ICommandActorContext context, ICommand command, string message)
    {
        switch (command)
        {
            case StartFuturesOptionQuoteDataStreamingCommand:
                await context.Received(1).SendAsync<FuturesOptionQuoteDataStreamingStartedFailEvent, QuoteId>(
                    Arg.Is<FuturesOptionQuoteDataStreamingStartedFailEvent>(value =>
                        value.CommandId == command.CommandId && value.ErrorMessage == message));
                break;
            case StopFuturesOptionQuoteDataStreamingCommand:
                await context.Received(1).SendAsync<FuturesOptionQuoteDataStreamingStoppedFailEvent, QuoteId>(
                    Arg.Is<FuturesOptionQuoteDataStreamingStoppedFailEvent>(value =>
                        value.CommandId == command.CommandId && value.ErrorMessage == message));
                break;
            case InsertFuturesOptionQuoteDataCommand:
                await context.Received(1).SendAsync<FuturesOptionQuoteDataInsertedFailEvent, QuoteId>(
                    Arg.Is<FuturesOptionQuoteDataInsertedFailEvent>(value =>
                        value.CommandId == command.CommandId && value.ErrorMessage == message));
                break;
        }
    }
}
