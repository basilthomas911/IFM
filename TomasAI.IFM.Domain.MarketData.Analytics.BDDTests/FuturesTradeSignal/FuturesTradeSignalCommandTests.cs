using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using ActorCommandExceptionEvent = TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesTradeSignal;

/// <summary>
/// BDD specifications for <see cref="FuturesTradeSignalCommandActor"/>, covering parsing, validation,
/// trade-signal state transitions, persistence, and failures over daily, weekly, and monthly data.
/// </summary>
public class FuturesTradeSignalCommandTests
{
    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTradeSignalCommandTests()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    sealed class TestableFuturesTradeSignalCommandActor(
        IEventSourceActorDbContext eventDb,
        ILogger<FuturesTradeSignalCommandActor> logger)
        : FuturesTradeSignalCommandActor(eventDb, logger)
    {
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
            ICommandActorContext context,
            IActorState state,
            ICommand command)
            => ReceiveAsync(context, state, command);

        public ValueTask InvokeOnValidateAsync(
            ICommandActorContext context,
            ActorThreadId threadId,
            ICommand command)
            => OnValidateAsync(context, threadId, command);

        public ValueTask InvokeOnStartupAsync(ICommandActorContext context)
            => OnStartup(context);

        public ValueTask<IActorState> InvokeOnLoadStateAsync(
            ICommandActorContext context,
            ActorThreadId threadId,
            ICommand command)
            => OnLoadStateAsync(context, threadId, command);

        public ValueTask InvokeOnSaveStateAsync(
            ICommandActorContext context,
            ActorThreadId threadId,
            IActorState state,
            ICommand command)
            => OnSaveStateAsync(context, threadId, state, command);

        public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
            ICommandActorContext context,
            ActorThreadId threadId,
            ICommand command,
            Exception exception)
            => OnExceptionAsync(context, threadId, command, exception);
    }

    sealed record Scenario(
        TestableFuturesTradeSignalCommandActor Actor,
        IEventSourceActorDbContext EventDb,
        ILogger<FuturesTradeSignalCommandActor> Logger,
        ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesTradeSignalCommandState> Repository,
        IContainerInstance Container);

    static Scenario CreateScenario()
    {
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = new TestableFuturesTradeSignalCommandActor(eventDb, logger);
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>()
            .Returns(repository);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        return new Scenario(actor, eventDb, logger, context, repository, container);
    }

    static NatsMsg<byte[]> CreateMessage(
        UpdateFuturesTradeSignalCommand command,
        byte[]? payload = null,
        string? subject = null)
        => new(
            subject ?? command.Subject.ToString(),
            string.Empty,
            0,
            default!,
            payload ?? ActorExtensions.DataSerializer.Serialize(command),
            default!,
            NatsMsgFlags.None);

    static FuturesTradeSignalCommandState CreateState(UpdateFuturesTradeSignalCommand command)
        => new() { Id = command.Subject.ThreadId };

    static FuturesTradeSignalUpdatedEvent LastUpdatedEvent(FuturesTradeSignalCommandState state)
        => state.Events.OfType<FuturesTradeSignalUpdatedEvent>().Last();

    // Domain behavior and actor dispatch

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoPriorTradeSignal_WhenAnUpdateIsRequested_ThenANewPeriodSpecificSignalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod);
        var state = CreateState(command);

        var result = await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.Should().ContainSingle().Which.Should().BeOfType<FuturesTradeSignalUpdatedEvent>();
        var updated = LastUpdatedEvent(state);
        updated.CommandId.Should().Be(command.CommandId);
        updated.EntityId.TimePeriod.Should().Be(timePeriod);
        updated.FuturesTradeSignal!.TimePeriod.Should().Be(timePeriod);
        updated.FuturesTradeSignal.ContractId.Should().Be(SampleData.ContractId);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenDownTrendingInputs_WhenAnUpdateIsRequested_ThenABuySignalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(
            timePeriod,
            direction: FuturesTrendDirectionType.DownTrending);
        var state = CreateState(command);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        var signal = LastUpdatedEvent(state).FuturesTradeSignal!;
        signal.TrendType.Should().Be(FuturesTrendType.DownTrending);
        signal.TDI.Should().Be(FuturesTrendDirectionType.DownTrending);
        signal.TradeSignal.Should().Be(TradeSignalType.Buy);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoOptionalIndicators_WhenAnUpdateIsRequested_ThenARangeBoundInitialSignalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod, includeIndicators: false);
        var state = CreateState(command);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        var signal = LastUpdatedEvent(state).FuturesTradeSignal!;
        signal.TrendType.Should().Be(FuturesTrendType.RangeBound);
        signal.TDI.Should().Be(FuturesTrendDirectionType.Init);
        signal.TradeSignal.Should().Be(TradeSignalType.None);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnIdenticalExistingSignal_WhenTheSameUpdateIsRequested_ThenNoDuplicateEventIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod);
        var state = CreateState(command);
        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        var result = await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        result.Success.Should().BeTrue();
        state.Events.OfType<FuturesTradeSignalUpdatedEvent>().Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAChangedClosingPrice_WhenAnUpdateIsRequested_ThenAReplacementSignalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var initial = SampleData.TradeSignalUpdateCommandFor(timePeriod);
        var changed = SampleData.TradeSignalUpdateCommandFor(
            timePeriod,
            SampleData.TradeSignalEodDataChanged);
        var state = CreateState(initial);
        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, initial);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, changed);

        state.Events.OfType<FuturesTradeSignalUpdatedEvent>().Should().HaveCount(2);
        LastUpdatedEvent(state).FuturesTradeSignal!.FuturesPrice
            .Should().Be(Convert.ToDouble(SampleData.TradeSignalEodDataChanged.ClosePrice));
    }

    // Message parsing and command logging

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAValidTradeSignalCommandMessage_WhenItIsParsed_ThenAllInputsAreRestoredAndLogged(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.TradeSignalUpdateCommandFor(timePeriod);

        var parsed = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var restored = parsed.Should().BeOfType<UpdateFuturesTradeSignalCommand>().Subject;
        restored.CommandId.Should().Be(expected.CommandId);
        restored.Subject.Should().Be(expected.Subject);
        restored.EntityId.Should().Be(expected.EntityId);
        restored.TimePeriod.Should().Be(timePeriod);
        restored.FuturesRsiSignal!.TimePeriod.Should().Be(timePeriod);
        restored.FuturesTdiSignal!.TimePeriod.Should().Be(timePeriod);
        restored.FuturesItiSignalData!.TrendDirectionChange!.TimePeriod.Should().Be(timePeriod);
        await scenario.EventDb.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(command => command.CommandId == expected.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(expected.CommandId.ToString())));
    }

    [Fact]
    public void GivenANullContext_WhenACommandMessageIsParsed_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(null!, CreateMessage(command));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Query", "FuturesTradeSignalCommand", "Update")]
    [InlineData("Command", "WrongTradeSignalActor", "Update")]
    [InlineData("Command", "FuturesTradeSignalCommand", "UnknownVerb")]
    public void GivenAnUnroutableSubject_WhenACommandMessageIsParsed_ThenAResolutionErrorIsReported(
        string actorType,
        string actorName,
        string verb)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);
        var subject = $"{actorType}.{actorName}.{verb}.{command.Subject.ThreadId.EntityId}";

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(command, subject: subject));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalCommandActor.ActorName} command from message: {subject}");
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenACorruptedPayload_WhenACommandMessageIsParsed_ThenDeserializationFails(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod);
        byte[] corruptedPayload = [0x00, 0x01, 0x02, 0xff, 0xfe];

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(command, corruptedPayload));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void GivenCommandLoggingFails_WhenACommandMessageIsParsed_ThenTheFailureIsPropagated()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Monthly);
        scenario.EventDb.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("command log unavailable")));

        var act = () => scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(command));

        act.Should().Throw<InvalidOperationException>().WithMessage("command log unavailable");
    }

    // Receive and validation edge cases

    [Fact]
    public async Task GivenANullReceiveContext_WhenAnUpdateIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(null!, CreateState(command), command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullState_WhenAnUpdateIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, null!, command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullCommand_WhenAnUpdateIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(
            scenario.Context,
            new FuturesTradeSignalCommandState(),
            null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenTheWrongStateType_WhenAnUpdateIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Weekly);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(
            scenario.Context,
            Substitute.For<IActorState>(),
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenAnUnsupportedCommand_WhenItIsReceived_ThenAResolutionErrorIsReported()
    {
        var scenario = CreateScenario();
        var command = Substitute.For<ICommand>();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(
            scenario.Context,
            new FuturesTradeSignalCommandState(),
            command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalCommandActor.ActorName} command from message: *");
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenACompleteTradeSignalCommand_WhenItIsValidated_ThenNoErrorsAreReported(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod);

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenOnlyRequiredEodData_WhenTheCommandIsValidated_ThenNoErrorsAreReported(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod, includeIndicators: false);

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GivenAnEmptyCommandId_WhenTheCommandIsValidated_ThenAValidationErrorIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily) with
        {
            CommandId = Guid.Empty
        };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task GivenMissingEodData_WhenTheCommandIsValidated_ThenAValidationErrorIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Weekly) with
        {
            FuturesEodData = null!
        };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.001)]
    [InlineData(201.0)]
    public async Task GivenAnInvalidVixPrice_WhenTheCommandIsValidated_ThenAValidationErrorIsReported(
        double invalidVixPrice)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Monthly) with
        {
            VixFuturesPrice = Convert.ToDecimal(invalidVixPrice)
        };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task GivenAnInvalidIndicator_WhenTheCommandIsValidated_ThenAValidationErrorIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily) with
        {
            FuturesRsiSignal = SampleData.TradeSignalRsiSignalFor(TradeTimePeriodType.Daily) with
            {
                ContractId = string.Empty
            }
        };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task GivenAnUndefinedTimePeriod_WhenTheCommandIsValidated_ThenAValidationErrorIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily) with
        {
            TimePeriod = (TradeTimePeriodType)999
        };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task GivenAnUnsupportedCommand_WhenItIsValidated_ThenAResolutionErrorIsReported()
    {
        var scenario = CreateScenario();
        var command = Substitute.For<ICommand>();
        var threadId = new ActorThreadId(
            ActorType.Command,
            FuturesTradeSignalCommandActor.ActorName,
            "unsupported");

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            threadId,
            command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesTradeSignalCommandActor.ActorName} commands from message: *");
    }

    [Fact]
    public async Task GivenInvalidValidationArguments_WhenValidationRuns_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);

        var nullContext = async () => await scenario.Actor.InvokeOnValidateAsync(
            null!, command.Subject.ThreadId, command);
        var emptyThread = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context, default, command);
        var nullCommand = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context, command.Subject.ThreadId, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await emptyThread.Should().ThrowAsync<ArgumentNullException>();
        await nullCommand.Should().ThrowAsync<ArgumentNullException>();
    }

    // Repository lifecycle

    [Fact]
    public async Task GivenAConfiguredContainer_WhenTheActorStarts_ThenItsTradeSignalRepositoryIsResolved()
    {
        var scenario = CreateScenario();

        await scenario.Actor.InvokeOnStartupAsync(scenario.Context);

        scenario.Container.Received(1)
            .Resolve<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>();
    }

    [Fact]
    public async Task GivenANullStartupContext_WhenTheActorStarts_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeOnStartupAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenAContainerWithoutARepository_WhenTheActorStarts_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        scenario.Container.Resolve<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>()
            .Returns((IEventSourceActorStateRepository<FuturesTradeSignalCommandState>)null!);

        var act = async () => await scenario.Actor.InvokeOnStartupAsync(scenario.Context);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenPersistedTradeSignalState_WhenACommandRuns_ThenStateCanBeLoadedAndSaved(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod);
        var expectedState = CreateState(command);
        scenario.Repository.LoadStateAsync(command).Returns(ValueTask.FromResult(expectedState));
        scenario.Repository.SaveStateAsync(scenario.Context, expectedState, command)
            .Returns(ValueTask.CompletedTask);
        await scenario.Actor.InvokeOnStartupAsync(scenario.Context);

        var loaded = await scenario.Actor.InvokeOnLoadStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);
        await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            loaded,
            command);

        loaded.Should().BeSameAs(expectedState);
        await scenario.Repository.Received(1).LoadStateAsync(command);
        await scenario.Repository.Received(1).SaveStateAsync(scenario.Context, expectedState, command);
    }

    [Fact]
    public async Task GivenInvalidLoadArguments_WhenStateIsLoaded_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);

        var nullContext = async () => await scenario.Actor.InvokeOnLoadStateAsync(
            null!, command.Subject.ThreadId, command);
        var emptyThread = async () => await scenario.Actor.InvokeOnLoadStateAsync(
            scenario.Context, default, command);
        var nullCommand = async () => await scenario.Actor.InvokeOnLoadStateAsync(
            scenario.Context, command.Subject.ThreadId, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await emptyThread.Should().ThrowAsync<ArgumentNullException>();
        await nullCommand.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenInvalidSaveArguments_WhenStateIsSaved_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);
        var state = CreateState(command);

        var nullContext = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            null!, command.Subject.ThreadId, state, command);
        var emptyThread = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context, default, state, command);
        var nullState = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context, command.Subject.ThreadId, null!, command);
        var nullCommand = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context, command.Subject.ThreadId, state, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await emptyThread.Should().ThrowAsync<ArgumentNullException>();
        await nullState.Should().ThrowAsync<ArgumentNullException>();
        await nullCommand.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenTheWrongStateType_WhenStateIsSaved_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Weekly);
        await scenario.Actor.InvokeOnStartupAsync(scenario.Context);

        var act = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            Substitute.For<IActorState>(),
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // Exception handling

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenATradeSignalCommandFailure_WhenTheActorHandlesIt_ThenAnExceptionEventAndFailedResultAreProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} trade signal update failed");
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(
                Arg.Any<ActorCommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command,
            exception);

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await scenario.Context.Received(1)
            .SendAsync<ActorCommandExceptionEvent, ActorEntityId>(
                Arg.Is<ActorCommandExceptionEvent>(error =>
                    error.ErrorMessage == exception.Message
                    && error.ErrorType == ErrorType.Command));
    }

    [Fact]
    public async Task GivenTheFirstExceptionEventSendFails_WhenTheActorRetries_ThenTheSecondResultIsReturnedAndLogged()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Weekly);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(
                Arg.Any<ActorCommandExceptionEvent>())
            .Returns(
                _ => throw new InvalidOperationException("first send failed"),
                _ => ValueTask.CompletedTask);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command,
            new InvalidOperationException("trade signal update failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await scenario.Context.Received(2)
            .SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>());
        scenario.Logger.ReceivedCalls()
            .Should().ContainSingle(call =>
                call.GetMethodInfo().Name == nameof(ILogger.Log)
                && (LogLevel)call.GetArguments()[0]! == LogLevel.Error);
    }

    [Fact]
    public async Task GivenBothExceptionEventSendsFail_WhenTheActorHandlesIt_ThenACommandFailureFallbackIsReturned()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Monthly);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(
                Arg.Any<ActorCommandExceptionEvent>())
            .Returns(_ => throw new InvalidOperationException("send failed"));

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command,
            new InvalidOperationException("trade signal update failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await scenario.Context.Received(2)
            .SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>());
    }

    [Fact]
    public async Task GivenInvalidExceptionArguments_WhenTheActorHandlesThem_ThenFailedResultsAreReturned()
    {
        var scenario = CreateScenario();
        var command = SampleData.TradeSignalUpdateCommandFor(TradeTimePeriodType.Daily);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(
                Arg.Any<ActorCommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var emptyThreadResult = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            default,
            command,
            new Exception("empty thread"));
        var nullCommandResult = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            null!,
            new Exception("null command"));
        var nullContextResult = await scenario.Actor.InvokeOnExceptionAsync(
            null!,
            command.Subject.ThreadId,
            command,
            new Exception("null context"));

        emptyThreadResult.Success.Should().BeFalse();
        nullCommandResult.Success.Should().BeFalse();
        nullContextResult.Success.Should().BeFalse();
    }
}
