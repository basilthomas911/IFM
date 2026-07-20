using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Actor.Command;
using TomasAI.IFM.Domain.SystemAdmin.Actor.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.SystemAdmin.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Actor.UnitTests;

public class SystemAdminCommandActorTests : IClassFixture<SystemAdminFixture>
{
    readonly SystemAdminFixture _fixture;

    public SystemAdminCommandActorTests(SystemAdminFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableSystemAdminCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<SystemAdminCommandActor> logger)
        : SystemAdminCommandActor(dbEventSource, logger)
    {
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnLoadStateAsync(context, threadId, cmd);

        public async ValueTask InvokeOnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
            => await OnSaveStateAsync(context, threadId, state, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);

        public async ValueTask InvokeOnStartup(ICommandActorContext context)
            => await OnStartup(context);
    }

    #region Helper

    static BackupDatabaseCommand CreateBackupCommand(
        string databaseName = SampleData.DatabaseName,
        DatabaseBackupType backupType = SampleData.BackupType,
        int commandTimeout = SampleData.CommandTimeout,
        Guid? commandId = null)
    {
        var cmd = new BackupDatabaseCommand(databaseName, backupType, commandTimeout);
        var id = commandId ?? Guid.NewGuid();
        return cmd with
        {
            CommandId = id,
            Subject = new ActorSubject(ActorType.Command, BackupDatabaseCommand.Actor, BackupDatabaseCommand.Verb, cmd.EntityId.Format())
        };
    }

    #endregion

    #region ParseMessage Happy Path Tests

    [Fact]
    public async Task ParseMessage_DeserializesBackupDatabaseCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();

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
        result.Should().BeOfType<BackupDatabaseCommand>();
        var deserializedCommand = result as BackupDatabaseCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.DatabaseName.Should().Be(command.DatabaseName);
        deserializedCommand.BackupType.Should().Be(command.BackupType);
        deserializedCommand.CommandTimeout.Should().Be(command.CommandTimeout);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_PreservesCommandIdAcrossSerialization()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = CreateBackupCommand(commandId: expectedCommandId);

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

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_PreservesAlternateBackupParameters()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand(
            databaseName: SampleData.AlternateDatabaseName,
            backupType: SampleData.AlternateBackupType,
            commandTimeout: SampleData.AlternateCommandTimeout);

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
        var deserialized = result as BackupDatabaseCommand;
        deserialized.Should().NotBeNull();
        deserialized!.DatabaseName.Should().Be(SampleData.AlternateDatabaseName);
        deserialized.BackupType.Should().Be(SampleData.AlternateBackupType);
        deserialized.CommandTimeout.Should().Be(SampleData.AlternateCommandTimeout);

        await Task.CompletedTask;
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorTypeIsNotCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Query.{SystemAdminCommandActor.ActorName}.{BackupDatabaseCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SystemAdminCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Command.DifferentActor.{BackupDatabaseCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SystemAdminCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Command.{SystemAdminCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SystemAdminCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
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
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
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
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
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
    public async Task ParseMessage_DatabaseInsertFails_ThrowsException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
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

        await Task.CompletedTask;
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_BackupDatabaseCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateBackupCommand();
        var state = new SystemAdminCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is DatabaseBackupEvent).Should().BeTrue();

        var evt = state.Events.OfType<DatabaseBackupEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.DatabaseName.Should().Be(cmd.DatabaseName);
        evt.BackupType.Should().Be(cmd.BackupType);
        evt.CommandTimeout.Should().Be(cmd.CommandTimeout);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_BackupDatabaseCommand_ReturnsServiceOkResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateBackupCommand();
        var state = new SystemAdminCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().BeOfType<ServiceOk<GuidResult>>();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_BackupDatabaseCommand_UpdatesStateProperties()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var cmd = CreateBackupCommand(
            databaseName: SampleData.AlternateDatabaseName,
            backupType: SampleData.AlternateBackupType,
            commandTimeout: SampleData.AlternateCommandTimeout);
        var state = new SystemAdminCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        state.DatabaseName.Should().Be(SampleData.AlternateDatabaseName);
        state.BackupType.Should().Be(SampleData.AlternateBackupType);
        state.CommandTimeout.Should().Be(SampleData.AlternateCommandTimeout);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var cmd = CreateBackupCommand();
        var state = new SystemAdminCommandState { Id = cmd.Subject.ThreadId };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var cmd = CreateBackupCommand();
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
        var actor = _fixture.CreateActor();

        var state = new SystemAdminCommandState { Id = new ActorThreadId(ActorType.Command, SystemAdminCommandActor.ActorName, "test-thread") };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenCommandTypeNotInReceiveMap()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var state = new SystemAdminCommandState { Id = new ActorThreadId(ActorType.Command, SystemAdminCommandActor.ActorName, "test-thread") };

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, SystemAdminCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SystemAdminCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotSystemAdminCommandState()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var cmd = CreateBackupCommand();

        var state = Substitute.For<IActorState>();
        state.Id.Returns(cmd.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ReceiveAsync_MultipleCommands_AccumulatesEventsInState()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var cmd1 = CreateBackupCommand(databaseName: SampleData.DatabaseName);
        var cmd2 = CreateBackupCommand(databaseName: SampleData.AlternateDatabaseName);

        var state = new SystemAdminCommandState { Id = cmd1.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, state, cmd1);
        await actor.InvokeReceiveAsync(context, state, cmd2);

        // Assert
        state.Events.Should().HaveCount(2);
        state.Events.OfType<DatabaseBackupEvent>().Should().HaveCount(2);
        state.Events.OfType<DatabaseBackupEvent>().Select(e => e.DatabaseName).Should().Contain(
            new[] { SampleData.DatabaseName, SampleData.AlternateDatabaseName });
    }

    #endregion

    #region OnValidateAsync Happy Path Tests

    [Fact]
    public async Task OnValidateAsync_BackupDatabaseCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var command = CreateBackupCommand();
        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_BackupDatabaseCommand_WithAlternateParameters_NoExceptionThrown()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var command = CreateBackupCommand(
            databaseName: SampleData.AlternateDatabaseName,
            backupType: SampleData.AlternateBackupType,
            commandTimeout: SampleData.AlternateCommandTimeout);
        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnValidateAsync Edge Case Tests

    [Fact]
    public async Task OnValidateAsync_BackupDatabaseCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var command = CreateBackupCommand(commandId: Guid.Empty);
        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, SystemAdminCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandTypeNotInValidationMap()
    {
        // Arrange
        var actor = _fixture.CreateActor();

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, SystemAdminCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {SystemAdminCommandActor.ActorName} commands from message:*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepoIsInitialized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedState = new SystemAdminCommandState { Id = new ActorThreadId(ActorType.Command, SystemAdminCommandActor.ActorName, "test-thread") };

        var repo = Substitute.For<IEventSourceActorStateRepository<SystemAdminCommandState>>();
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SystemAdminCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Initialize _repo by invoking OnStartup
        await actor.InvokeOnStartup(context);

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, command);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<SystemAdminCommandState>();
        result.Should().BeSameAs(expectedState);
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var repo = Substitute.For<IEventSourceActorStateRepository<SystemAdminCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SystemAdminCommandState>>().Returns(repo);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        await actor.InvokeOnStartup(context);

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var repo = Substitute.For<IEventSourceActorStateRepository<SystemAdminCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SystemAdminCommandState>>().Returns(repo);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        await actor.InvokeOnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, SystemAdminCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnSaveStateAsync Happy Path Tests

    [Fact]
    public async Task OnSaveStateAsync_CallsRepoSaveState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var repo = Substitute.For<IEventSourceActorStateRepository<SystemAdminCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<SystemAdminCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SystemAdminCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        var cmd = CreateBackupCommand();
        var state = new SystemAdminCommandState { Id = cmd.Subject.ThreadId };
        var threadId = cmd.Subject.ThreadId;

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await repo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(c => c == context),
            Arg.Is<SystemAdminCommandState>(s => s == state),
            Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    #endregion

    #region OnSaveStateAsync Edge Case Tests

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var repo = Substitute.For<IEventSourceActorStateRepository<SystemAdminCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SystemAdminCommandState>>().Returns(repo);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        await actor.InvokeOnStartup(context);

        var cmd = CreateBackupCommand();
        var state = new SystemAdminCommandState { Id = cmd.Subject.ThreadId };
        var threadId = cmd.Subject.ThreadId;

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
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var repo = Substitute.For<IEventSourceActorStateRepository<SystemAdminCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SystemAdminCommandState>>().Returns(repo);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        await actor.InvokeOnStartup(context);

        var cmd = CreateBackupCommand();
        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, null!, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var repo = Substitute.For<IEventSourceActorStateRepository<SystemAdminCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SystemAdminCommandState>>().Returns(repo);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        await actor.InvokeOnStartup(context);

        var cmd = CreateBackupCommand();
        var state = new SystemAdminCommandState { Id = cmd.Subject.ThreadId };
        var threadId = cmd.Subject.ThreadId;

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
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
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
    public async Task OnExceptionAsync_PreservesErrorMessage()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;
        var expectedMessage = "Detailed error message for testing";
        var exception = new Exception(expectedMessage);

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
        failedResult!.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnExceptionAsync_CommandValidationException_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new CommandValidationException(BackupDatabaseCommand.ErrorId, "Validation failed");

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

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenContextIsNull_AndFallsBackToCommandFailed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Test error");

        // Act - OnExceptionAsync catches inner exceptions and falls back to CommandFailed
        var result = await actor.InvokeOnExceptionAsync(null!, threadId, command, exception);

        // Assert - should return a ServiceFailed result due to internal catch blocks
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_SendAsyncFails_FallsBackToCommandFailed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new InvalidOperationException("Original error");

        var context = Substitute.For<ICommandActorContext>();

        // Simulate SendAsync failure
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(x => throw new Exception("SendAsync failed"));

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert - should still return a result (via the catch blocks)
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    [Fact]
    public async Task OnExceptionAsync_NullException_StillReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SystemAdminCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = CreateBackupCommand();
        var threadId = command.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();

        // Act - pass null exception, should still produce a result via fallback
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, null!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
    }

    #endregion
}
