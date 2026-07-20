using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal;

public class FuturesAtrSignalCommandActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesAtrSignalCommandActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesAtrSignalCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesAtrSignalCommandActor> logger)
        : FuturesAtrSignalCommandActor(dbEventSource, logger)
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

    private static GenerateFuturesAtrSignalCommand CreateAtrCommand(Guid? commandId = null)
    {
        var entityId = SampleData.AtrEntityId;
        return SampleData.AtrGenerateCommand with
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_DeserializesGenerateFuturesAtrSignalCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();

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
        result.Should().BeOfType<GenerateFuturesAtrSignalCommand>();
        var deserialized = result as GenerateFuturesAtrSignalCommand;
        deserialized.Should().NotBeNull();
        deserialized!.CommandId.Should().Be(command.CommandId);
        deserialized.FuturesAtrSignalId.ContractId.Should().Be(command.FuturesAtrSignalId.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);

        dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

    [Fact]
    public void ParseMessage_DeserializesGenerateFuturesAtrDailySignalCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var atrSignalId = SampleData.AtrSignalId;
        var command = new GenerateFuturesAtrDailySignalCommand(atrSignalId, (decimal)SampleData.FuturesPrice) with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor, GenerateFuturesAtrDailySignalCommand.Verb, atrSignalId.ToDailyEntityId().Format())
        };

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
        result.Should().BeOfType<GenerateFuturesAtrDailySignalCommand>();
        var deserialized = result as GenerateFuturesAtrDailySignalCommand;
        deserialized.Should().NotBeNull();
        deserialized!.CommandId.Should().Be(command.CommandId);
        deserialized.FuturesAtrSignalId.ContractId.Should().Be(command.FuturesAtrSignalId.ContractId);
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = CreateAtrCommand(expectedCommandId);

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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();

        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg) as GenerateFuturesAtrSignalCommand;

        // Assert
        result.Should().NotBeNull();
        result!.FuturesPrice.Should().Be(command.FuturesPrice);
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorTypeIsNotCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Event.{FuturesAtrSignalCommandActor.ActorName}.{GenerateFuturesAtrSignalCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAtrSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Command.{FuturesAtrSignalCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAtrSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsCorrupted()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
        var corruptedPayload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, corruptedPayload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
        var emptyPayload = Array.Empty<byte>();
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, emptyPayload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenDatabaseInsertFails()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
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
    public async Task ReceiveAsync_GenerateFuturesAtrSignalCommand_ExecutesHandler_ReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var state = new FuturesAtrSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_GenerateFuturesAtrDailySignalCommand_ExecutesHandler_ReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var atrSignalId = SampleData.AtrSignalId;
        var cmd = new GenerateFuturesAtrDailySignalCommand(atrSignalId, (decimal)SampleData.FuturesPrice) with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor, GenerateFuturesAtrDailySignalCommand.Verb, atrSignalId.ToDailyEntityId().Format())
        };
        var state = new FuturesAtrSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var state = new FuturesAtrSignalCommandState { Id = cmd.Subject.ThreadId };

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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var state = new FuturesAtrSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesAtrSignalCommandActor.ActorName, "test-thread") };
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var unknownCmd = Substitute.For<ICommand>();
        var state = new FuturesAtrSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesAtrSignalCommandActor.ActorName, "test-thread") };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, unknownCmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesAtrSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
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
    public async Task OnValidateAsync_ValidGenerateFuturesAtrSignalCommand_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_ValidGenerateFuturesAtrDailySignalCommand_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var atrSignalId = SampleData.AtrSignalId;
        var cmd = new GenerateFuturesAtrDailySignalCommand(atrSignalId, (decimal)SampleData.FuturesPrice) with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor, GenerateFuturesAtrDailySignalCommand.Verb, atrSignalId.ToDailyEntityId().Format())
        };
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
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenGenerateFuturesAtrSignalCommandIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand() with { CommandId = Guid.Empty };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId is empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenGenerateFuturesAtrDailySignalCommandIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var atrSignalId = SampleData.AtrSignalId;
        var cmd = new GenerateFuturesAtrDailySignalCommand(atrSignalId, (decimal)SampleData.FuturesPrice) with
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor, GenerateFuturesAtrDailySignalCommand.Verb, atrSignalId.ToDailyEntityId().Format())
        };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId is empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandTypeIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var unknownCmd = Substitute.For<ICommand>();
        unknownCmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesAtrSignalCommandActor.ActorName, "UnknownVerb", Guid.NewGuid().ToString()));
        var threadId = new ActorThreadId(ActorType.Command, FuturesAtrSignalCommandActor.ActorName, "test-thread");
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, unknownCmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesAtrSignalCommandActor.ActorName, "test-thread");
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, default, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsStateFromRepository()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var threadId = cmd.Subject.ThreadId;
        var expectedState = new FuturesAtrSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesAtrSignalCommandState>>();
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesAtrSignalCommandState>>().Returns(repo);

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

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesAtrSignalCommandActor.ActorName, "test-thread");
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesAtrSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesAtrSignalCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesAtrSignalCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesAtrSignalCommandState>>().Returns(repo);

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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesAtrSignalCommandState { Id = threadId };

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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var state = new FuturesAtrSignalCommandState { Id = cmd.Subject.ThreadId };
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesAtrSignalCommandActor.ActorName, "test-thread");
        var state = new FuturesAtrSignalCommandState { Id = threadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesAtrSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var cmd = CreateAtrCommand();
        var threadId = cmd.Subject.ThreadId;
        var invalidState = Substitute.For<IActorState>();
        invalidState.Id.Returns(threadId);

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesAtrSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesAtrSignalCommandState>>().Returns(repo);
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
        var threadId = command.Subject.ThreadId;
        var expectedMessage = "Atr signal generation failed";
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
    public async Task OnExceptionAsync_SetsEmptyCommandIdInErrorEvent_WhenCommandIsNotForwarded()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = CreateAtrCommand(expectedCommandId);
        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Test exception");

        var context = Substitute.For<ICommandActorContext>();

        Shared.EventModelActor.Events.CommandExceptionEvent? capturedEvent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(e => capturedEvent = e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert - the underlying SendErrorEventAsync overload used here does not forward the source command
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
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
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
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
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullThreadId_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesAtrSignalCommandActor>>();
        var actor = _fixture.CreateAtrCommandActor(dbEventSource, logger);

        var command = CreateAtrCommand();
        var exception = new InvalidOperationException("Test error");
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

    #endregion
}
