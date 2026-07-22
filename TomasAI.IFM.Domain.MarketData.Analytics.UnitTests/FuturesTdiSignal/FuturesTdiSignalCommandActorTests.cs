using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using ActorCommandExceptionEvent = TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public class FuturesTdiSignalCommandActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTdiSignalCommandActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public sealed class TestableFuturesTdiSignalCommandActor(
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
        ILogger<FuturesTdiSignalCommandActor> Logger,
        ICommandActorContext Context,
        IEventSourceActorStateRepository<FuturesTdiSignalCommandState> Repository,
        IContainerInstance Container);

    Scenario CreateScenario()
    {
        var eventDb = Substitute.For<IEventSourceActorDbContext>();
        eventDb.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateActor(eventDb, logger);
        var repository = Substitute.For<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>()
            .Returns(repository);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        return new Scenario(actor, eventDb, logger, context, repository, container);
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

    async Task InitializeRepositoryAsync(Scenario scenario)
        => await scenario.Actor.InvokeOnStartupAsync(scenario.Context);

    // ParseMessage

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ParseMessage_ValidCommand_DeserializesAndLogsCommand(
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
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
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
    public void ParseMessage_UnroutableSubject_ThrowsInvalidOperationException(
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

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_CorruptedPayload_ThrowsDeserializationException(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
        byte[] corruptedPayload = [0x00, 0x01, 0x02, 0xff, 0xfe];

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(command, corruptedPayload));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_EmptyPayload_ThrowsDeserializationException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Monthly);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(command, []));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_CommandLogFailure_PropagatesException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Weekly);
        scenario.EventDb.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("command log unavailable")));

        var act = () => scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(command));

        act.Should().Throw<InvalidOperationException>().WithMessage("command log unavailable");
    }

    // ReceiveAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_InitialState_ReturnsCommandIdAndGeneratesPeriodSpecificEvent(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
        var state = CreateState(command);

        var result = await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        var generatedEvent = state.Events.OfType<FuturesTdiSignalGeneratedEvent>().Should().ContainSingle().Subject;
        generatedEvent.EntityId.TimePeriod.Should().Be(timePeriod);
        generatedEvent.FuturesTdiSignal.TimePeriod.Should().Be(timePeriod);
        generatedEvent.FuturesTdiSignal.TDI.Should().Be(FuturesTrendDirectionType.Init);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_ExistingState_ExecutesHandlerAndGeneratesNextSignal(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
        var state = CreateState(command);
        state.Update(SampleData.CreateTdiSignalGeneratedEventFor(
            timePeriod,
            FuturesTrendDirectionType.UpTrending));

        var result = await scenario.Actor.InvokeReceiveAsync(scenario.Context, state, command);

        result.Success.Should().BeTrue();
        result.Value!.Guid.Should().Be(command.CommandId);
        state.Events.OfType<FuturesTdiSignalGeneratedEvent>().Last().FuturesTdiSignal.TDI
            .Should().Be(FuturesTrendDirectionType.UpTrending);
    }

    [Fact]
    public async Task ReceiveAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(null!, CreateState(command), command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_NullState_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, null!, command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_NullCommand_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(
            scenario.Context,
            new FuturesTdiSignalCommandState(),
            null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_WrongStateType_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(
            scenario.Context,
            Substitute.For<IActorState>(),
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedCommand_ThrowsInvalidOperationException()
    {
        var scenario = CreateScenario();
        var command = Substitute.For<ICommand>();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(
            scenario.Context,
            new FuturesTdiSignalCommandState(),
            command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalCommandActor.ActorName} command from message: *");
    }

    // OnValidateAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnValidateAsync_ValidCommand_CompletesSuccessfully(
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

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnValidateAsync_EmptyCommandId_ThrowsCommandValidationException(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod) with { CommandId = Guid.Empty };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnValidateAsync_EmptyRsiSignals_ThrowsCommandValidationException(
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
    public async Task OnValidateAsync_DefaultSignalId_ThrowsCommandValidationException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Weekly) with
        {
            FuturesTdiSignalId = default
        };

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            null!,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_EmptyThreadId_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            default,
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_NullCommand_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "null-command");

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            threadId,
            null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_UnsupportedCommand_ThrowsInvalidOperationException()
    {
        var scenario = CreateScenario();
        var command = Substitute.For<ICommand>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "unsupported");

        var act = async () => await scenario.Actor.InvokeOnValidateAsync(
            scenario.Context,
            threadId,
            command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesTdiSignalCommandActor.ActorName} commands from message: *");
    }

    // OnLoadStateAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnLoadStateAsync_PersistedState_ReturnsRepositoryState(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
        var expected = CreateState(command);
        scenario.Repository.LoadStateAsync(command).Returns(ValueTask.FromResult(expected));
        await InitializeRepositoryAsync(scenario);

        var result = await scenario.Actor.InvokeOnLoadStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        result.Should().BeSameAs(expected);
        await scenario.Repository.Received(1).LoadStateAsync(command);
    }

    [Fact]
    public async Task OnLoadStateAsync_RepositoryFailure_PropagatesException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Monthly);
        scenario.Repository.LoadStateAsync(command)
            .Returns<ValueTask<FuturesTdiSignalCommandState>>(_ =>
                throw new InvalidOperationException("load failed"));
        await InitializeRepositoryAsync(scenario);

        var act = async () => await scenario.Actor.InvokeOnLoadStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
    }

    [Fact]
    public async Task OnLoadStateAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnLoadStateAsync(
            null!,
            command.Subject.ThreadId,
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_EmptyThreadId_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnLoadStateAsync(
            scenario.Context,
            default,
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_NullCommand_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "null-command");

        var act = async () => await scenario.Actor.InvokeOnLoadStateAsync(
            scenario.Context,
            threadId,
            null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // OnSaveStateAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnSaveStateAsync_ValidState_SavesThroughRepository(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(timePeriod);
        var state = CreateState(command);
        scenario.Repository.SaveStateAsync(scenario.Context, state, command)
            .Returns(ValueTask.CompletedTask);
        await InitializeRepositoryAsync(scenario);

        await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            state,
            command);

        await scenario.Repository.Received(1).SaveStateAsync(scenario.Context, state, command);
    }

    [Fact]
    public async Task OnSaveStateAsync_RepositoryFailure_PropagatesException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Weekly);
        var state = CreateState(command);
        scenario.Repository.SaveStateAsync(scenario.Context, state, command)
            .Returns(_ => throw new InvalidOperationException("save failed"));
        await InitializeRepositoryAsync(scenario);

        var act = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            state,
            command);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
    }

    [Fact]
    public async Task OnSaveStateAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            null!,
            command.Subject.ThreadId,
            CreateState(command),
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_EmptyThreadId_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            default,
            CreateState(command),
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_NullState_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            null!,
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_WrongStateType_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            command.Subject.ThreadId,
            Substitute.For<IActorState>(),
            command);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_NullCommand_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "null-command");

        var act = async () => await scenario.Actor.InvokeOnSaveStateAsync(
            scenario.Context,
            threadId,
            new FuturesTdiSignalCommandState { Id = threadId },
            null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // OnExceptionAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_CommandFailure_SendsExceptionEventAndReturnsFailure(
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
    public async Task OnExceptionAsync_ValidationFailure_ReturnsFailedResult()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            command.Subject.ThreadId,
            command,
            new CommandValidationException(command.ErrorCode, "validation failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnExceptionAsync_FirstEventSendFails_RetriesAndReturnsFailure()
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
        await scenario.Context.Received(2)
            .SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>());
    }

    [Fact]
    public async Task OnExceptionAsync_BothEventSendsFail_ReturnsCommandFailureFallback()
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

    [Fact]
    public async Task OnExceptionAsync_NullContext_ReturnsCommandFailureFallback()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            null!,
            command.Subject.ThreadId,
            command,
            new InvalidOperationException("generation failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_EmptyThreadId_UsesRetryPathAndReturnsFailure()
    {
        var scenario = CreateScenario();
        var command = SampleData.TdiGenerateCommandFor(TradeTimePeriodType.Daily);
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            default,
            command,
            new InvalidOperationException("generation failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_NullCommand_UsesRetryPathAndReturnsFailure()
    {
        var scenario = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "null-command");
        scenario.Context.SendAsync<ActorCommandExceptionEvent, ActorEntityId>(Arg.Any<ActorCommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        var result = await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            threadId,
            null!,
            new InvalidOperationException("generation failed"));

        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        result.Success.Should().BeFalse();
    }
}
