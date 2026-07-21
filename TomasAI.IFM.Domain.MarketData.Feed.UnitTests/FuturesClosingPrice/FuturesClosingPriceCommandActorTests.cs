using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesClosingPrice;

public class FuturesClosingPriceCommandActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesClosingPriceCommandActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesClosingPriceCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesClosingPriceCommandActor> logger)
        : FuturesClosingPriceCommandActor(dbEventSource, logger)
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
    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public async Task ParseMessage_DeserializesInsertFuturesClosingPriceCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<InsertFuturesClosingPriceCommand>();
        var deserializedCommand = result as InsertFuturesClosingPriceCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.FuturesClosingPriceId.ContractId.Should().Be(command.FuturesClosingPriceId.ContractId);
        deserializedCommand.FuturesClosingPriceId.ValueDate.Should().Be(command.FuturesClosingPriceId.ValueDate);
        deserializedCommand.ClosingPrice.Should().Be(command.ClosingPrice);
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        // Create subject with Query type instead of Command
        var invalidSubject = $"Query.{FuturesClosingPriceCommandActor.ActorName}.{InsertFuturesClosingPriceCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesClosingPriceCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        // Create subject with different actor name
        var invalidSubject = $"Command.DifferentActor.{InsertFuturesClosingPriceCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesClosingPriceCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        // Create subject with unrecognized verb
        var invalidSubject = $"Command.{FuturesClosingPriceCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesClosingPriceCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        // Create corrupted payload
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var subject = command!.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Simulate database insert failure
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.FromException(new Exception("Database connection failed")));

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert - should throw because database insert is synchronously awaited
        act.Should().Throw<Exception>().WithMessage("Database connection failed");

        await Task.CompletedTask;
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_InsertFuturesClosingPriceCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesClosingPriceInsertedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FuturesClosingPriceInsertedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.FuturesClosingPriceId.ContractId.Should().Be(cmd.FuturesClosingPriceId.ContractId);
        evt.FuturesClosingPriceId.ValueDate.Should().Be(cmd.FuturesClosingPriceId.ValueDate);
        evt.ClosingPrice.Should().Be(cmd.ClosingPrice);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_InsertFuturesClosingPriceCommand_ReturnsServiceOkResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceOk<GuidResult>>();
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = new ActorThreadId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "test-thread") };
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = new ActorThreadId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "test-thread") };

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesClosingPriceCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesClosingPriceCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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

    [Fact]
    public async Task ReceiveAsync_DoesNotThrowInsertFuturesClosingPriceException_WhenClosingPriceAlreadyExists()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        // Simulate that closing price already exists
        db.GetFuturesClosingPriceAsync(Arg.Any<FuturesDataId>())
            .Returns(new Shared.MarketDataFeed.ViewModels.FuturesClosingPriceReadModel("ESM4", SampleData.ValueDate, SampleData.ClosingPrice1, DateTime.UtcNow, "test"));
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnValidateAsync Happy Path Tests

    [Fact]
    public async Task OnValidateAsync_InsertFuturesClosingPriceCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_InsertFuturesClosingPriceCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_InsertFuturesClosingPriceCommand_EmptyContractId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var invalidId = new FuturesDataId(string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesClosingPriceCommand(invalidId, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, invalidId.Format()),
            EntityId = invalidId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*FuturesDataId.ContractId*required*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertFuturesClosingPriceCommand_MinValueDate_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var invalidId = new FuturesDataId("ESM4", DateOnly.MinValue);
        var command = new InsertFuturesClosingPriceCommand(invalidId, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, invalidId.Format()),
            EntityId = invalidId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ValueDate*invalid*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesClosingPriceCommandActor.ActorName} commands from message:*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepositoryReturnsValidState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var expectedState = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        // Initialize the repo via OnStartup
        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesClosingPriceCommandState>();
        (result as FuturesClosingPriceCommandState)!.Id.Should().Be(expectedState.Id);
    }

    [Fact]
    public async Task OnLoadStateAsync_CallsRepositoryLoadState_WithCorrectCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var expectedState = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        await actor.InvokeOnLoadStateAsync(context, threadId, cmd!);

        // Assert
        await repo.Received(1).LoadStateAsync(Arg.Is<ICommand>(c => c.CommandId == cmd!.CommandId));
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, default, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsException_WhenRepositoryThrows()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns<FuturesClosingPriceCommandState>(_ => throw new InvalidOperationException("Repository load failed"));

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, cmd!);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Repository load failed");
    }

    #endregion

    #region OnSaveStateAsync Happy Path Tests

    [Fact]
    public async Task OnSaveStateAsync_CallsRepositorySaveState_WithCorrectParameters()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesClosingPriceCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd!);

        // Assert
        await repo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(c => c == context),
            Arg.Is<FuturesClosingPriceCommandState>(s => s == state),
            Arg.Is<ICommand>(c => c.CommandId == cmd!.CommandId));
    }

    [Fact]
    public async Task OnSaveStateAsync_CompletesSuccessfully_WhenCalledWithValidParameters()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesClosingPriceCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnSaveStateAsync Edge Case Tests

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, default, state, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, null!, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = new ActorThreadId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "test-thread") };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesClosingPriceCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesClosingPriceCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var wrongState = Substitute.For<IActorState>();
        wrongState.Id.Returns(cmd!.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, wrongState, cmd!);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenRepositoryThrows()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var cmd = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var db = Substitute.For<IMarketDataDbContext>();
        var state = new FuturesClosingPriceCommandState(db) { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesClosingPriceCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesClosingPriceCommandState>(), Arg.Any<ICommand>())
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("Repository save failed"))));

        await ((ICommandActor<FuturesClosingPriceCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd!);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Repository save failed");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_GenericException_SendsCommandExceptionEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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
    public async Task OnExceptionAsync_InsertFuturesClosingPriceException_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InsertFuturesClosingPriceException("Closing price already exists");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<FuturesClosingPriceInsertedFailEvent, FuturesDataId>(
            Arg.Any<FuturesClosingPriceInsertedFailEvent>())
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
    public async Task OnExceptionAsync_PreservesCommandIdInErrorEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");

        var context = Substitute.For<ICommandActorContext>();

        Shared.EventModelActor.Events.CommandExceptionEvent? capturedEvent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(e => capturedEvent = e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.CommandId.Should().Be(expectedCommandId);
    }

    [Fact]
    public async Task OnExceptionAsync_IncludesExceptionMessageInErrorEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var expectedMessage = "Detailed error message for testing";
        var exception = new InvalidOperationException(expectedMessage);

        var context = Substitute.For<ICommandActorContext>();

        Shared.EventModelActor.Events.CommandExceptionEvent? capturedEvent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(e => capturedEvent = e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.ErrorMessage.Should().Be(expectedMessage);
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncFails_StillReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InvalidOperationException("Original error");

        var context = Substitute.For<ICommandActorContext>();

        // First call throws, second call (in catch) also throws, forcing CommandFailed path
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(_ => throw new Exception("SendAsync failed"));

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert - should still return a failed result via CommandFailed fallback
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_CommandValidationException_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesClosingPriceCommandActor>>();
        var actor = _fixture.CreateFuturesClosingPriceCommandActor(dbEventSource, logger);

        var entityId = SampleData.FuturesClosingPriceId1;
        var command = new InsertFuturesClosingPriceCommand(SampleData.FuturesClosingPriceId1, SampleData.ClosingPrice1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
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

    #endregion
}
