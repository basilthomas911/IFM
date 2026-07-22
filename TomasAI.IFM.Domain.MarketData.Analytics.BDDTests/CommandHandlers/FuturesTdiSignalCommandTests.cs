using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
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

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.CommandHandlers;

/// <summary>
/// BDD specifications for <see cref="FuturesTdiSignalCommandActor"/>, including message parsing,
/// validation, state transitions, persistence, and error handling over daily, weekly, and monthly data.
/// </summary>
public class FuturesTdiSignalCommandTests
{
    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTdiSignalCommandTests()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    sealed class TestableFuturesTdiSignalCommandActor(
        IEventSourceActorDbContext dbEventSource,
        ILogger<FuturesTdiSignalCommandActor> logger)
        : FuturesTdiSignalCommandActor(dbEventSource, logger)
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
        TestableFuturesTdiSignalCommandActor Actor,
        IEventSourceActorDbContext EventDb,
        ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesTdiSignalCommandState> Repository,
        IContainerInstance Container);

    static Scenario CreateScenario()
    {
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var actor = new TestableFuturesTdiSignalCommandActor(
            eventDb,
            Substitute.For<ILogger<FuturesTdiSignalCommandActor>>());
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>()
            .Returns(repository);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        return new Scenario(actor, eventDb, context, repository, container);
    }

    static NatsMsg<byte[]> CreateMessage(
        GenerateFuturesTdiSignalCommand command,
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

    static FuturesTdiSignalCommandState CreateState(GenerateFuturesTdiSignalCommand command)
        => new() { Id = command.Subject.ThreadId };

    static FuturesTdiSignalCommandState CreateSeededState(
        GenerateFuturesTdiSignalCommand command,
        FuturesTrendDirectionType priorDirection)
    {
        var state = CreateState(command);
        state.Update(SampleData.CreateTdiHistoryEventFor(command.EntityId.TimePeriod, priorDirection));
        return state;
    }

    static FuturesTdiSignalReadModel LastSignal(FuturesTdiSignalCommandState state)
        => state.Events.OfType<FuturesTdiSignalGeneratedEvent>().Last().FuturesTdiSignal;

    // Domain behavior and actor dispatch

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoPriorTdiSignal_WhenGenerationIsRequested_ThenAnInitialSignalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
        var state = CreateState(command);

        var result = await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.Init);
        LastSignal(state).TimePeriod.Should().Be(timePeriod);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnUpTrend_WhenThePriorSignalIsUpTrending_ThenTheTrendContinuesAtHighStrength(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod, SampleData.TdiUpTrendingRsiSignals);
        var state = CreateSeededState(command, FuturesTrendDirectionType.UpTrending);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.UpTrending);
        LastSignal(state).TDIStrength.Should().Be(FuturesTrendDirectionStrengthType.High);
        LastSignal(state).UpTrendCount.Should().BeGreaterThan(1);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenADownTrend_WhenThePriorSignalIsDownTrending_ThenTheTrendContinuesAtHighStrength(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod, SampleData.TdiDownTrendingRsiSignals);
        var state = CreateSeededState(command, FuturesTrendDirectionType.DownTrending);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.DownTrending);
        LastSignal(state).TDIStrength.Should().Be(FuturesTrendDirectionStrengthType.High);
        LastSignal(state).DownTrendCount.Should().BeGreaterThan(1);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenADownTrendAfterAnUpTrend_WhenGenerationIsRequested_ThenATrendReversalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod, SampleData.TdiDownTrendingRsiSignals);
        var state = CreateSeededState(command, FuturesTrendDirectionType.UpTrending);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnUpTrendAfterADownTrend_WhenGenerationIsRequested_ThenATrendReversalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod, SampleData.TdiUpTrendingRsiSignals);
        var state = CreateSeededState(command, FuturesTrendDirectionType.DownTrending);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenRangeBoundInputsAndAFlatPriorSignal_WhenGenerationIsRequested_ThenAFlatLowStrengthSignalIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod, SampleData.TdiRangeBoundRsiSignals);
        var state = CreateSeededState(command, FuturesTrendDirectionType.Flat);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.Flat);
        LastSignal(state).TDIStrength.Should().Be(FuturesTrendDirectionStrengthType.Low);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenOneUpTrendingRsiInput_WhenGenerationIsRequested_ThenMediumStrengthIsProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var signal = SampleData.TdiSingleRsiSignal[0] with { RSI = 65, RSISlope = 1 };
        var command = SampleData.TdiGenerateCommandFor(timePeriod, [signal]);
        var state = CreateSeededState(command, FuturesTrendDirectionType.UpTrending);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.UpTrending);
        LastSignal(state).TDIStrength.Should().Be(FuturesTrendDirectionStrengthType.Medium);
        LastSignal(state).UpTrendCount.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenUnorderedRsiInputs_WhenGenerationIsRequested_ThenTheLatestTimestampDeterminesTheTrend(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var inputs = SampleData.TdiUpTrendingRsiSignals.Reverse().ToArray();
        var command = SampleData.TdiGenerateCommandFor(timePeriod, inputs);
        var state = CreateSeededState(command, FuturesTrendDirectionType.UpTrending);

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        LastSignal(state).TDI.Should().Be(FuturesTrendDirectionType.UpTrending);
        LastSignal(state).UpTrendCount.Should().Be(6,
            "sorting must anchor the five-minute window at the latest RSI timestamp");
    }

    // Message parsing and command logging

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAValidTdiCommandMessage_WhenItIsParsed_ThenTheCommandIsRestoredAndLogged(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.TdiGenerateCommandFor(timePeriod);

        var parsed = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        parsed.Should().BeOfType<GenerateFuturesTdiSignalCommand>();
        parsed.CommandId.Should().Be(expected.CommandId);
        parsed.Subject.Should().Be(expected.Subject);
        ((GenerateFuturesTdiSignalCommand)parsed).EntityId.TimePeriod.Should().Be(timePeriod);
        await scenario.EventDb.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(command => command.CommandId == expected.CommandId),
            Arg.Any<DateTime>(),
            Arg.Is<string>(json => json.Contains(expected.CommandId.ToString())));
    }

    [Fact]
    public void GivenANullContext_WhenACommandMessageIsParsed_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(null!, CreateMessage(command));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Query", "FuturesTdiSignalCommand", "Generate")]
    [InlineData("Command", "WrongTdiActor", "Generate")]
    [InlineData("Command", "FuturesTdiSignalCommand", "UnknownVerb")]
    public void GivenAnUnroutableSubject_WhenACommandMessageIsParsed_ThenAResolutionErrorIsReported(
        string actorType,
        string actorName,
        string verb)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);
        var subject = $"{actorType}.{actorName}.{verb}.{command.Subject.ThreadId.EntityId}";

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(command, subject: subject));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalCommandActor.ActorName} command from message: {subject}");
    }

    [Fact]
    public void GivenACorruptedPayload_WhenACommandMessageIsParsed_ThenDeserializationFails()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Weekly);
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
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Monthly);
        scenario.EventDb.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("command log unavailable")));

        var act = () => scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(command));

        act.Should().Throw<InvalidOperationException>().WithMessage("command log unavailable");
    }

    // Receive and validation edge cases

    [Fact]
    public async Task GivenANullReceiveContext_WhenGenerationIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(null!, CreateState(command), command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullState_WhenGenerationIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, null!, command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullCommand_WhenGenerationIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var state = new FuturesTdiSignalCommandState();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenTheWrongStateType_WhenGenerationIsRequested_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);
        var state = Substitute.For<IActorState>();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenAnUnsupportedCommand_WhenItIsReceived_ThenAResolutionErrorIsReported()
    {
        var scenario = CreateScenario();
        var command = Substitute.For<ICommand>();
        var state = new FuturesTdiSignalCommandState();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalCommandActor.ActorName} command from message: *");
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAValidTdiCommand_WhenItIsValidated_ThenNoValidationErrorsAreReported(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);

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
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily) with { CommandId = Guid.Empty };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoRsiInputs_WhenTheCommandIsValidated_ThenAValidationErrorIsReported(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod) with { FuturesRsiSignals = [] };

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
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "unsupported");

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(scenario.Context, threadId, command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesTdiSignalCommandActor.ActorName} commands from message: *");
    }

    [Fact]
    public async Task GivenInvalidValidationArguments_WhenValidationRuns_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var nullContext = async () => await scenario.Actor.InvokeOnValidateAsync(null!, command.Subject.ThreadId, command);
        var emptyThread = async () => await scenario.Actor.InvokeOnValidateAsync(scenario.Context, default, command);
        var nullCommand = async () => await scenario.Actor.InvokeOnValidateAsync(scenario.Context, command.Subject.ThreadId, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await emptyThread.Should().ThrowAsync<ArgumentNullException>();
        await nullCommand.Should().ThrowAsync<ArgumentNullException>();
    }

    // Repository lifecycle

    [Fact]
    public async Task GivenAConfiguredContainer_WhenTheActorStarts_ThenItsStateRepositoryIsResolved()
    {
        var scenario = CreateScenario();

        await scenario.Actor.InvokeOnStartupAsync(scenario.Context);

        scenario.Container.Received(1)
            .Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
    }

    [Fact]
    public async Task GivenANullStartupContext_WhenTheActorStarts_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeOnStartupAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenPersistedState_WhenACommandIsProcessed_ThenStateCanBeLoadedAndSaved(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
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
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var nullContext = async () => await scenario.Actor.InvokeOnLoadStateAsync(null!, command.Subject.ThreadId, command);
        var emptyThread = async () => await scenario.Actor.InvokeOnLoadStateAsync(scenario.Context, default, command);
        var nullCommand = async () => await scenario.Actor.InvokeOnLoadStateAsync(scenario.Context, command.Subject.ThreadId, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await emptyThread.Should().ThrowAsync<ArgumentNullException>();
        await nullCommand.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenInvalidSaveArguments_WhenStateIsSaved_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);
        var state = CreateState(command);

        var nullContext = async () => await scenario.Actor.InvokeOnSaveStateAsync(null!, command.Subject.ThreadId, state, command);
        var emptyThread = async () => await scenario.Actor.InvokeOnSaveStateAsync(scenario.Context, default, state, command);
        var nullState = async () => await scenario.Actor.InvokeOnSaveStateAsync(scenario.Context, command.Subject.ThreadId, null!, command);
        var nullCommand = async () => await scenario.Actor.InvokeOnSaveStateAsync(scenario.Context, command.Subject.ThreadId, state, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await emptyThread.Should().ThrowAsync<ArgumentNullException>();
        await nullState.Should().ThrowAsync<ArgumentNullException>();
        await nullCommand.Should().ThrowAsync<ArgumentNullException>();
    }

    // Exception handling

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenACommandFailure_WhenTheActorHandlesIt_ThenAnExceptionEventAndFailedResultAreProduced(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command,
            new InvalidOperationException($"{timePeriod} generation failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await scenario.Context.Received(1)
            .SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>());
    }

    [Fact]
    public async Task GivenTheFirstExceptionEventSendFails_WhenTheActorRetries_ThenTheSecondResultIsReturned()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Weekly);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>())
            .Returns(
                _ => throw new InvalidOperationException("first send failed"),
                _ => ValueTask.CompletedTask);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command,
            new InvalidOperationException("generation failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        await scenario.Context.Received(2)
            .SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>());
    }

    [Fact]
    public async Task GivenBothExceptionEventSendsFail_WhenTheActorHandlesIt_ThenACommandFailureFallbackIsReturned()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Monthly);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("send failed"));

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command,
            new InvalidOperationException("generation failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
    }
}
