using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public class FuturesTdiSignalCommandActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesTdiSignalCommandActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTdiSignalCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesTdiSignalCommandActor> logger)
        : FuturesTdiSignalCommandActor(dbEventSource, logger)
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

    #region ParseMessage Happy Path Tests

    [Fact]
    public async Task ParseMessage_DeserializesGenerateFuturesTdiSignalCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var subject = command!.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GenerateFuturesTdiSignalCommand>();
        var deserializedCommand = result as GenerateFuturesTdiSignalCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.FuturesTdiSignalId.ContractId.Should().Be(command.FuturesTdiSignalId.ContractId);
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
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var subject = command!.Subject.ToString();
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

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorTypeIsNotCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        var invalidSubject = $"Query.{FuturesTdiSignalCommandActor.ActorName}.{GenerateFuturesTdiSignalCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        var invalidSubject = $"Command.DifferentActor.{GenerateFuturesTdiSignalCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        var invalidSubject = $"Command.{FuturesTdiSignalCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var subject = command!.Subject.ToString();
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
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var corruptedPayload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
        var subject = command!.Subject.ToString();
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
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var emptyPayload = Array.Empty<byte>();
        var subject = command!.Subject.ToString();
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
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var subject = command!.Subject.ToString();
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
    public async Task ReceiveAsync_GenerateFuturesTdiSignalCommand_InitializingState_ExecutesHandler_ReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var cmd = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesTdiSignalCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesTdiSignalGeneratedEvent).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_GenerateFuturesTdiSignalCommand_WithExistingState_ExecutesHandler_ReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var cmd = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesTdiSignalCommandState { Id = cmd!.Subject.ThreadId };
        var existingEvent = SampleData.CreateTdiSignalGeneratedEvent(Guid.NewGuid());
        state.Apply(existingEvent).Should().BeTrue();

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesTdiSignalGeneratedEvent).Should().BeTrue();
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var cmd = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesTdiSignalCommandState { Id = cmd!.Subject.ThreadId };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, state, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var cmd = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, null!, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var state = new FuturesTdiSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "test-thread") };
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
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var state = new FuturesTdiSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "test-thread") };

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesTdiSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var cmd = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = Substitute.For<IActorState>();
        state.Id.Returns(cmd!.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion

    #region OnValidateAsync Happy Path Tests

    [Fact]
    public async Task OnValidateAsync_GenerateFuturesTdiSignalCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnValidateAsync Edge Case Tests

    [Fact]
    public async Task OnValidateAsync_GenerateFuturesTdiSignalCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, default, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandTypeNotInValidationMap()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesTdiSignalCommandActor.ActorName} commands from message:*");
    }

    #endregion

    #region OnStartup Happy Path Tests

    [Fact]
    public async Task OnStartup_WithValidContext_ResolvesRepository()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Act
        Func<Task> act = async () => await actor.InvokeOnStartup(context);

        // Assert
        await act.Should().NotThrowAsync();
        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
    }

    [Fact]
    public async Task OnStartup_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Act
        Func<Task> act = async () =>
        {
            await actor.InvokeOnStartup(context);
            await actor.InvokeOnStartup(context);
        };

        // Assert
        await act.Should().NotThrowAsync();
        container.Received(2).Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
    }

    #endregion

    #region OnStartup Edge Case Tests

    [Fact]
    public async Task OnStartup_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        // Act
        Func<Task> act = async () => await actor.InvokeOnStartup(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnStartup_ThrowsNullReferenceException_WhenContainerIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns((IContainerInstance)null!);

        // Act
        Func<Task> act = async () => await actor.InvokeOnStartup(context);

        // Assert
        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task OnStartup_ThrowsArgumentNullException_WhenContainerReturnsNullRepo()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>()
            .Returns((IEventSourceActorStateRepository<FuturesTdiSignalCommandState>)null!);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Act
        Func<Task> act = async () => await actor.InvokeOnStartup(context);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepoIsConfigured()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
        var expectedState = new FuturesTdiSignalCommandState { Id = threadId };
        mockRepo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Trigger OnStartup to set the repo
        await actor.InvokeOnStartup(context);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, command!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesTdiSignalCommandState>();
        result.Id.Should().Be(threadId);

        await mockRepo.Received(1).LoadStateAsync(Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId));
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, default, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnSaveStateAsync Happy Path Tests

    [Fact]
    public async Task OnSaveStateAsync_CallsRepoSaveStateAsync_WhenRepoIsConfigured()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var state = new FuturesTdiSignalCommandState { Id = threadId };

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>();
        mockRepo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(state));
        mockRepo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesTdiSignalCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTdiSignalCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Trigger OnStartup to set the repo
        await actor.InvokeOnStartup(context);

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, command!);

        // Assert
        await mockRepo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(ctx => ctx == context),
            Arg.Is<FuturesTdiSignalCommandState>(s => s.Id == threadId),
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId));
    }

    #endregion

    #region OnSaveStateAsync Edge Case Tests

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var state = new FuturesTdiSignalCommandState { Id = threadId };

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesTdiSignalCommandState { Id = command!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, default, state, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, null!, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "test-thread");
        var state = new FuturesTdiSignalCommandState { Id = threadId };
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
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
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
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
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
    public async Task OnExceptionAsync_PreservesErrorCodeInFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");

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
        failedResult.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncFails_ReturnsFailedResultWithFallback()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
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
    public async Task OnExceptionAsync_NullPointerException_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new NullReferenceException("Object reference not set");

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
    public async Task OnExceptionAsync_WhenFirstSendAsyncFailsButSecondSucceeds_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InvalidOperationException("Original error");

        var context = Substitute.For<ICommandActorContext>();

        // First call throws (enters catch), second call succeeds (inner try succeeds)
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(
                _ => throw new Exception("First SendAsync failed"),
                _ => ValueTask.CompletedTask);

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
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InvalidOperationException("Test error");

        // Act - null context triggers ArgumentNullException in try, caught by catch block
        var result = await actor.InvokeOnExceptionAsync(null!, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullThreadId_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var exception = new InvalidOperationException("Test error");
        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act - null threadId triggers ArgumentNullException in try, caught by catch block
        var result = await actor.InvokeOnExceptionAsync(context, default, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullCommand_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesTdiSignalCommandActor.ActorName, "test-thread");
        var exception = new InvalidOperationException("Test error");
        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act - null command triggers ArgumentNullException in try, caught by catch block
        var result = await actor.InvokeOnExceptionAsync(context, threadId, null!, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WithTimeoutException_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTdiSignalCommandActor>>();
        var actor = _fixture.CreateTdiCommandActor(dbEventSource, logger);

        var entityId = SampleData.TdiEntityId;
        var command = SampleData.TdiGenerateCommand with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new TimeoutException("Database operation timed out");

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
