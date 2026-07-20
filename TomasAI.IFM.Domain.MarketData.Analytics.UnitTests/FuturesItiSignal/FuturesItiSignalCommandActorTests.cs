using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public class FuturesItiSignalCommandActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesItiSignalCommandActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesItiSignalCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesItiSignalCommandActor> logger)
        : FuturesItiSignalCommandActor(dbEventSource, logger)
    {
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask InvokeOnStartup(ICommandActorContext context)
            => await OnStartup(context);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnLoadStateAsync(context, threadId, cmd);

        public async ValueTask InvokeOnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
            => await OnSaveStateAsync(context, threadId, state, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);
    }

    // Time periods supported end-to-end by the ITI compute model's default trading-day lookup.
    public static readonly TheoryData<TradeTimePeriodType> SupportedTimePeriods = new()
    {
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    // All three time periods requested for coverage; Daily is intentionally unsupported by the ITI compute model.
    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    private static GenerateFuturesItiSignalCommand CreateGenerateCommand(Guid? commandId = null, TradeTimePeriodType? timePeriod = null)
    {
        var entityId = timePeriod.HasValue ? SampleData.EntityIdFor(timePeriod.Value) : SampleData.EntityId;
        var baseCommand = timePeriod.HasValue ? SampleData.GenerateCommandFor(timePeriod.Value) : SampleData.GenerateCommand;
        return baseCommand with
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    private static SetFuturesItiSignalHoldTradeCommand CreateSetHoldTradeCommand(Guid? commandId = null, TradeTimePeriodType? timePeriod = null)
    {
        var entityId = timePeriod.HasValue ? SampleData.EntityIdFor(timePeriod.Value) : SampleData.EntityId;
        var baseCommand = timePeriod.HasValue ? SampleData.SetHoldTradeCommandFor(timePeriod.Value) : SampleData.SetHoldTradeCommand;
        return baseCommand with
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, SetFuturesItiSignalHoldTradeCommand.Actor, SetFuturesItiSignalHoldTradeCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    private static ClearFuturesItiSignalHoldTradeCommand CreateClearHoldTradeCommand(Guid? commandId = null, TradeTimePeriodType? timePeriod = null)
    {
        var entityId = timePeriod.HasValue ? SampleData.EntityIdFor(timePeriod.Value) : SampleData.EntityId;
        var baseCommand = timePeriod.HasValue ? SampleData.ClearHoldTradeCommandFor(timePeriod.Value) : SampleData.ClearHoldTradeCommand;
        return baseCommand with
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ClearFuturesItiSignalHoldTradeCommand.Actor, ClearFuturesItiSignalHoldTradeCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_DeserializesGenerateFuturesItiSignalCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GenerateFuturesItiSignalCommand>();
        var deserialized = result as GenerateFuturesItiSignalCommand;
        deserialized.Should().NotBeNull();
        deserialized!.CommandId.Should().Be(command.CommandId);
        deserialized.ContractId.Should().Be(command.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);

        dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

    [Fact]
    public void ParseMessage_DeserializesSetFuturesItiSignalHoldTradeCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateSetHoldTradeCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<SetFuturesItiSignalHoldTradeCommand>();
        var deserialized = result as SetFuturesItiSignalHoldTradeCommand;
        deserialized!.CommandId.Should().Be(command.CommandId);

        dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

    [Fact]
    public void ParseMessage_DeserializesClearFuturesItiSignalHoldTradeCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateClearHoldTradeCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ClearFuturesItiSignalHoldTradeCommand>();
        var deserialized = result as ClearFuturesItiSignalHoldTradeCommand;
        deserialized!.CommandId.Should().Be(command.CommandId);

        dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_PreservesCommandId_AcrossSerialization_ForAllTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = CreateGenerateCommand(expectedCommandId, timePeriod);
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.CommandId.Should().Be(expectedCommandId);
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenSubjectActorTypeDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var badSubject = new ActorSubject(ActorType.Event, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, command.EntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(badSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsUnknown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var badSubject = new ActorSubject(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "UnknownVerb", command.EntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(badSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var badSubject = new ActorSubject(ActorType.Command, "SomeOtherActor", GenerateFuturesItiSignalCommand.Verb, command.EntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(badSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenDatabaseInsertFails()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new Exception("Database connection failed")));

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<Exception>().WithMessage("Database connection failed");
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_GenerateFuturesItiSignalCommand_ExecutesHandler_ReturnsGuid()
    {
        // Arrange
        // Daily is unsupported by the ITI compute model's default trading-day lookup, so use Weekly here.
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand(timePeriod: TradeTimePeriodType.Weekly);
        var state = new FuturesItiSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public async Task ReceiveAsync_GenerateFuturesItiSignalCommand_UpdatesState_ForSupportedTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand(timePeriod: timePeriod);
        var state = new FuturesItiSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_SetFuturesItiSignalHoldTradeCommand_ExecutesHandler_ReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateSetHoldTradeCommand();
        var state = new FuturesItiSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_ClearFuturesItiSignalHoldTradeCommand_ExecutesHandler_ReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateClearHoldTradeCommand();
        var state = new FuturesItiSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var state = new FuturesItiSignalCommandState { Id = cmd.Subject.ThreadId };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, null!, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var state = new FuturesItiSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "test-thread") };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenCommandTypeIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var unknownCmd = Substitute.For<ICommand>();
        var state = new FuturesItiSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "test-thread") };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, unknownCmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesItiSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var invalidState = Substitute.For<IActorState>();
        invalidState.Id.Returns(cmd.Subject.ThreadId);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, invalidState, cmd);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion

    #region OnValidateAsync Happy Path Tests

    [Fact]
    public async Task OnValidateAsync_ValidGenerateFuturesItiSignalCommand_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnValidateAsync_ValidGenerateFuturesItiSignalCommand_DoesNotThrow_ForAllTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand(timePeriod: timePeriod);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_ValidSetFuturesItiSignalHoldTradeCommand_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateSetHoldTradeCommand();
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_ValidClearFuturesItiSignalHoldTradeCommand_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateClearHoldTradeCommand();
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnValidateAsync Edge Case Tests

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, default, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "test-thread");
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandTypeIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var unknownCmd = Substitute.For<ICommand>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "test-thread");
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, unknownCmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenCommandIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { CommandId = Guid.Empty };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId is empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenContractIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { ContractId = string.Empty };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId is empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenValueDateIsMinValue()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { ValueDate = DateOnly.MinValue };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ValueDate is invalid*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenTimePeriodIsNotDefined()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { TimePeriod = (TradeTimePeriodType)999 };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*TimePeriod has an invalid value*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenTimestampIsDefault()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { Timestamp = default };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*Timestamp is not set*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenFuturesPriceIsZero()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { FuturesPrice = 0 };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*FuturesPrice must be greater than zero*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenFuturesPriceIsNaN()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { FuturesPrice = double.NaN };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*FuturesPrice is not a number*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenVixFuturesPriceIsNegative()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand() with { VixFuturesPrice = -1 };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*VixFuturesPrice must be greater than zero*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenSetHoldTradeContractIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateSetHoldTradeCommand() with { ContractId = string.Empty };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId is empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenClearHoldTradeValueDateIsMinValue()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateClearHoldTradeCommand() with { ValueDate = DateOnly.MinValue };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ValueDate is invalid*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_FromRepository()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;
        var expectedState = new FuturesItiSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>();
        repo.LoadStateAsync(cmd).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(expectedState);
        await repo.Received(1).LoadStateAsync(cmd);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnLoadStateAsync_ReturnsState_FromRepository_ForAllTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand(timePeriod: timePeriod);
        var threadId = cmd.Subject.ThreadId;
        var expectedState = new FuturesItiSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>();
        repo.LoadStateAsync(cmd).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().BeSameAs(expectedState);
        await repo.Received(1).LoadStateAsync(cmd);
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "test-thread");
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, default, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnSaveStateAsync Happy Path Tests

    [Fact]
    public async Task OnSaveStateAsync_SavesState_ToRepository()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesItiSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesItiSignalCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await repo.Received(1).SaveStateAsync(context, state, cmd);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnSaveStateAsync_SavesState_ToRepository_ForAllTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand(timePeriod: timePeriod);
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesItiSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesItiSignalCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await repo.Received(1).SaveStateAsync(context, state, cmd);
    }

    #endregion

    #region OnSaveStateAsync Edge Case Tests

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesItiSignalCommandState { Id = threadId };

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, null!, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var state = new FuturesItiSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, default, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "test-thread");
        var state = new FuturesItiSignalCommandState { Id = threadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesItiSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var cmd = CreateGenerateCommand();
        var threadId = cmd.Subject.ThreadId;
        var invalidState = Substitute.For<IActorState>();
        invalidState.Id.Returns(threadId);

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesItiSignalCommandState>>().Returns(repo);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, invalidState, cmd);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_GenericException_SendsCommandExceptionEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new InvalidOperationException("Unexpected error occurred");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_PreservesExceptionMessage_InErrorEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var threadId = command.Subject.ThreadId;
        var expectedMessage = "Iti signal generation failed";
        var exception = new Exception(expectedMessage);

        var context = Substitute.For<ICommandActorContext>();

        Shared.EventModelActor.Events.CommandExceptionEvent? capturedEvent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(e => capturedEvent = e))
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        capturedEvent.Should().NotBeNull();
        capturedEvent!.ErrorMessage.Should().Be(expectedMessage);
    }

    [Fact]
    public async Task OnExceptionAsync_DoesNotPropagateCommandId_WhenNotSuppliedToSendErrorEventAsync()
    {
        // Arrange
        // The actor's SendErrorEventAsync<TFailedEvent, TEntityId>(ErrorType, context) overload does not
        // pass the originating command, so the resulting error event always carries an empty CommandId.
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = CreateGenerateCommand(expectedCommandId);
        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Test exception");

        var context = Substitute.For<ICommandActorContext>();

        Shared.EventModelActor.Events.CommandExceptionEvent? capturedEvent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(e => capturedEvent = e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.CommandId.Should().Be(Guid.Empty);
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenContextIsNull_FallsBackToCommandFailed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Test error");

        // Act - OnExceptionAsync catches inner exceptions and falls back to CommandFailed
        var result = await actor.InvokeOnExceptionAsync(null!, threadId, command, exception);

        // Assert - should still return a failed result via CommandFailed fallback
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncFails_FallsBackToCommandFailed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Original error");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(_ => throw new Exception("SendAsync also failed"));

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenThreadIdIsNull_FallsBackToCommandFailed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var command = CreateGenerateCommand();
        var exception = new Exception("Test error");
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, default, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenCommandIsNull_FallsBackToCommandFailed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesItiSignalCommandActor>>();
        var actor = _fixture.CreateCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesItiSignalCommandActor.ActorName, "test-thread");
        var exception = new Exception("Test error");
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, null!, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    #endregion
}
