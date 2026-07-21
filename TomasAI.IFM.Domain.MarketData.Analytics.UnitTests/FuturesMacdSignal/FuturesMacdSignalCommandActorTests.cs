using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesMacdSignal;

public class FuturesMacdSignalCommandActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesMacdSignalCommandActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesMacdSignalCommandActor : FuturesMacdSignalCommandActor
    {
        public TestableFuturesMacdSignalCommandActor(IEventSourceActorDbContext dbEventSource, IDbContextFactory dbFactory, ILogger<FuturesMacdSignalCommandActor> logger)
            : base(dbEventSource, dbFactory, logger)
        {
        }

        public TestableFuturesMacdSignalCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesMacdSignalCommandActor> logger)
            : base(dbEventSource, null, logger)
        {
        }

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

    private static GenerateFuturesMacdSignalCommand CreateMacdCommand(Guid? commandId = null, TradeTimePeriodType? timePeriod = null)
    {
        var entityId = timePeriod is null
            ? SampleData.MacdEntityId
            : SampleData.MacdEntityId with { TimePeriod = timePeriod.Value };

        var signalId = timePeriod is null
            ? SampleData.MacdSignalId
            : SampleData.MacdSignalId with { TimePeriod = timePeriod.Value };

        return new GenerateFuturesMacdSignalCommand(signalId, (decimal)SampleData.FuturesPrice) with
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    public static IEnumerable<object[]> TimePeriods()
    {
        yield return new object[] { TradeTimePeriodType.Daily };
        yield return new object[] { TradeTimePeriodType.Weekly };
        yield return new object[] { TradeTimePeriodType.Monthly };
    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_DeserializesGenerateFuturesMacdSignalCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
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
        result.Should().BeOfType<GenerateFuturesMacdSignalCommand>();
        var deserialized = result as GenerateFuturesMacdSignalCommand;
        deserialized.Should().NotBeNull();
        deserialized!.CommandId.Should().Be(command.CommandId);
        deserialized.FuturesMacdSignalId.ContractId.Should().Be(command.FuturesMacdSignalId.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);

        dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

    [Fact]
    public void ParseMessage_PreservesCommandIdAcrossSerialization()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = CreateMacdCommand(expectedCommandId);

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

    [Fact]
    public void ParseMessage_PreservesFuturesPrice_AfterDeserialization()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg) as GenerateFuturesMacdSignalCommand;

        // Assert
        result.Should().NotBeNull();
        result!.FuturesPrice.Should().Be(command.FuturesPrice);
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public void ParseMessage_DeserializesCommand_ForEachTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand(timePeriod: timePeriod);
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg) as GenerateFuturesMacdSignalCommand;

        // Assert
        result.Should().NotBeNull();
        result!.FuturesMacdSignalId.TimePeriod.Should().Be(timePeriod);
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenSubjectActorTypeIsNotCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = new ActorSubject(ActorType.Event, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, SampleData.MacdEntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = new ActorSubject(ActorType.Command, "SomeOtherActor", GenerateFuturesMacdSignalCommand.Verb, SampleData.MacdEntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsUnrecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, "UnknownVerb", SampleData.MacdEntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_ExecutesGenerateFuturesMacdSignalCommand_AndReturnsGuidResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var state = new FuturesMacdSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceOk<GuidResult>>();
        var okResult = result as ServiceOk<GuidResult>;
        okResult.Should().NotBeNull();
        okResult!.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task ReceiveAsync_ExecutesCommand_ForEachTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand(timePeriod: timePeriod);
        var state = new FuturesMacdSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceOk<GuidResult>>();
        var okResult = result as ServiceOk<GuidResult>;
        okResult!.Value.Guid.Should().Be(cmd.CommandId);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var state = new FuturesMacdSignalCommandState { Id = cmd.Subject.ThreadId };

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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var state = new FuturesMacdSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesMacdSignalCommandActor.ActorName, "test-thread") };
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var unknownCmd = Substitute.For<ICommand>();
        var state = new FuturesMacdSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesMacdSignalCommandActor.ActorName, "test-thread") };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, unknownCmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesMacdSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
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
    public async Task OnValidateAsync_ValidGenerateFuturesMacdSignalCommand_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_ValidGenerateFuturesMacdDailySignalCommand_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var dailyId = SampleData.MacdSignalId;
        var cmd = new GenerateFuturesMacdDailySignalCommand(dailyId, (decimal)SampleData.FuturesPrice) with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdDailySignalCommand.Actor, GenerateFuturesMacdDailySignalCommand.Verb, dailyId.ToDailyEntityId().Format())
        };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task OnValidateAsync_ValidCommand_DoesNotThrow_ForEachTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand(timePeriod: timePeriod);
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
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenContractIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var invalidId = SampleData.MacdSignalId with { ContractId = string.Empty };
        var cmd = new GenerateFuturesMacdSignalCommand(invalidId, (decimal)SampleData.FuturesPrice) with
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, invalidId.ToEntityId().Format())
        };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId is required*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenValueDateIsMinValue()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var invalidId = SampleData.MacdSignalId with { ValueDate = DateOnly.MinValue };
        var cmd = new GenerateFuturesMacdSignalCommand(invalidId, (decimal)SampleData.FuturesPrice) with
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, invalidId.ToEntityId().Format())
        };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ValueDate must be greater than DateOnly.MinValue*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenTimestampIsMinValue()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var invalidId = SampleData.MacdSignalId with { Timestamp = TimeOnly.MinValue };
        var cmd = new GenerateFuturesMacdSignalCommand(invalidId, (decimal)SampleData.FuturesPrice) with
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, invalidId.ToEntityId().Format())
        };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*Timestamp must be greater than TimeOnly.MinValue*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesMacdSignalCommandActor.ActorName, "test-thread");
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var unknownCmd = Substitute.For<ICommand>();
        unknownCmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesMacdSignalCommandActor.ActorName, "UnknownVerb", SampleData.MacdEntityId.Format()));
        var threadId = unknownCmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, unknownCmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsStateFromRepository()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var threadId = cmd.Subject.ThreadId;
        var expectedState = new FuturesMacdSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>();
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>().Returns(repo);

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
    [MemberData(nameof(TimePeriods))]
    public async Task OnLoadStateAsync_ReturnsStateFromRepository_ForEachTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand(timePeriod: timePeriod);
        var threadId = cmd.Subject.ThreadId;
        var expectedState = new FuturesMacdSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>();
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().BeSameAs(expectedState);
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesMacdSignalCommandActor.ActorName, "test-thread");
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesMacdSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesMacdSignalCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await repo.Received(1).SaveStateAsync(context, state, cmd);
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task OnSaveStateAsync_SavesState_ForEachTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand(timePeriod: timePeriod);
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesMacdSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesMacdSignalCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesMacdSignalCommandState>>().Returns(repo);

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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesMacdSignalCommandState { Id = threadId };

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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var state = new FuturesMacdSignalCommandState { Id = cmd.Subject.ThreadId };
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
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var cmd = CreateMacdCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesMacdSignalCommandState { Id = threadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_GenericException_SendsCommandExceptionEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
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
    public async Task OnExceptionAsync_CommandValidationException_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new CommandValidationException(command.ErrorCode, "Validation failed");

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
    public async Task OnExceptionAsync_PreservesErrorMessage_InFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
        failedResult.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task OnExceptionAsync_ReturnsFailedResult_ForEachTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand(timePeriod: timePeriod);
        var threadId = command.Subject.ThreadId;
        var exception = new InvalidOperationException("Error for time period");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncFails_ReturnsFailedResultWithFallback()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new InvalidOperationException("Original error");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(x => throw new Exception("SendAsync failed"));

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
    public async Task OnExceptionAsync_WithNullContext_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new InvalidOperationException("No context");

        // Act
        var result = await actor.InvokeOnExceptionAsync(null!, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullThreadId_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var exception = new InvalidOperationException("No thread id");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, default, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullCommand_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesMacdSignalCommandActor.ActorName, "test-thread");
        var exception = new InvalidOperationException("No command");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, null!, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_WithTimeoutException_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesMacdSignalCommandActor>>();
        var actor = _fixture.CreateMacdCommandActor(dbEventSource, logger);

        var command = CreateMacdCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new TimeoutException("Operation timed out");

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

    #endregion

}
