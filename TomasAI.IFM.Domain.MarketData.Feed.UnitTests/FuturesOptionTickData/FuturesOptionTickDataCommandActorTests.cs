using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData;

public class FuturesOptionTickDataCommandActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionTickDataCommandActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesOptionTickDataCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesOptionTickDataCommandActor> logger)
        : FuturesOptionTickDataCommandActor(dbEventSource, logger)
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
    public async Task ParseMessage_DeserializesInsertFuturesOptionTickDataCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
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
        result.Should().BeOfType<InsertFuturesOptionTickDataCommand>();
        var deserializedCommand = result as InsertFuturesOptionTickDataCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.Contract.ContractId.Should().Be(command.Contract.ContractId);
        deserializedCommand.OptionTickData.ContractId.Should().Be(command.OptionTickData.ContractId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesStartFuturesOptionTickDataStreamingCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, SampleData.RiskFreeRate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
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
        result.Should().BeOfType<StartFuturesOptionTickDataStreamingCommand>();
        var deserializedCommand = result as StartFuturesOptionTickDataStreamingCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.EntityId.Should().Be(command.EntityId);
        deserializedCommand.Contract.ContractId.Should().Be(command.Contract.ContractId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesStopFuturesOptionTickDataStreamingCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StopFuturesOptionTickDataStreamingCommand(SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0].ContractId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
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
        result.Should().BeOfType<StopFuturesOptionTickDataStreamingCommand>();
        var deserializedCommand = result as StopFuturesOptionTickDataStreamingCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.EntityId.Should().Be(command.EntityId);
        deserializedCommand.ContractId.Should().Be(command.ContractId);
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with Query type instead of Command
        var invalidSubject = $"Query.{FuturesOptionTickDataCommandActor.ActorName}.{InsertFuturesOptionTickDataCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with different actor name
        var invalidSubject = $"Command.DifferentActor.{InsertFuturesOptionTickDataCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with unrecognized verb
        var invalidSubject = $"Command.{FuturesOptionTickDataCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        // Create corrupted payload
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
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
    public async Task ReceiveAsync_InsertFuturesOptionTickDataCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);

        var evt = state.Events.OfType<FuturesOptionTickDataInsertedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.Contract.ContractId.Should().Be(cmd.Contract.ContractId);
        evt.TickData.ContractId.Should().Be(cmd.OptionTickData.ContractId);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_StartFuturesOptionTickDataStreamingCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var cmd = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, SampleData.RiskFreeRate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);

        var evt = state.Events.OfType<FuturesOptionTickDataStreamingStartedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.EntityId.Should().Be(cmd.EntityId);
        evt.Contract.ContractId.Should().Be(cmd.Contract.ContractId);
        evt.BaseContract.ContractId.Should().Be(cmd.BaseContract.ContractId);
        evt.ValueDate.Should().Be(cmd.ValueDate);
        evt.MaturityDate.Should().Be(cmd.MaturityDate);
        evt.RiskFreeRate.Should().Be(cmd.RiskFreeRate);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_StopFuturesOptionTickDataStreamingCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var cmd = new StopFuturesOptionTickDataStreamingCommand(SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0].ContractId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);

        var evt = state.Events.OfType<FuturesOptionTickDataStreamingStoppedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.EntityId.Should().Be(cmd.EntityId);
        evt.ContractId.Should().Be(cmd.ContractId);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_InsertCommand_ReturnsServiceOkResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceOk<GuidResult>>();
    }

    [Fact]
    public async Task ReceiveAsync_MultipleCommands_AccumulatesEventsInState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId1 = SampleData.EsOptionTickData.EntityId;
        var cmd1 = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId1.Format()),
            EntityId = entityId1
        };

        var optionTickData2 = SampleData.EsOptionTickData with { TickId = 2, TickTime = new TimeOnly(14, 31, 0) };
        var entityId2 = optionTickData2.EntityId;
        var cmd2 = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, optionTickData2)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId2.Format()),
            EntityId = entityId2
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd1.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, state, cmd1);
        await actor.InvokeReceiveAsync(context, state, cmd2);

        // Assert
        state.Events.Should().HaveCount(2);
        state.Events.OfType<FuturesOptionTickDataInsertedEvent>().Should().HaveCount(2);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var state = new FuturesOptionTickDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "test-thread") };
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var state = new FuturesOptionTickDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "test-thread") };

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesOptionTickDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = Substitute.For<IActorState>();
        state.Id.Returns(cmd.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion

    #region OnValidateAsync Happy Path Tests

    [Fact]
    public async Task OnValidateAsync_InsertFuturesOptionTickDataCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_StartFuturesOptionTickDataStreamingCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, SampleData.RiskFreeRate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_StopFuturesOptionTickDataStreamingCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StopFuturesOptionTickDataStreamingCommand(SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0].ContractId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

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
    public async Task OnValidateAsync_InsertCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, SampleData.RiskFreeRate)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_InvalidContract_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var invalidContract = SampleData.FuturesOptionContracts[0] with { ContractId = string.Empty };
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, invalidContract, SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, SampleData.RiskFreeRate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_InvalidBaseContract_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var invalidBaseContract = SampleData.EsContract with { ContractId = string.Empty };
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], invalidBaseContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, SampleData.RiskFreeRate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_InvalidValueDate_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                DateOnly.MinValue, SampleData.OptionMaturityDate, SampleData.RiskFreeRate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ValueDate*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_InvalidMaturityDate_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, DateOnly.MaxValue, SampleData.RiskFreeRate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*MaturityDate*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_InvalidRiskFreeRate_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StartFuturesOptionTickDataStreamingCommand(
                SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0], SampleData.EsContract,
                SampleData.ValueDate, SampleData.OptionMaturityDate, double.NaN)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*RiskFreeRate*");
    }

    [Fact]
    public async Task OnValidateAsync_StopStreamingCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StopFuturesOptionTickDataStreamingCommand(SampleData.OptionTickStreamingFeedId, SampleData.FuturesOptionContracts[0].ContractId)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_StopStreamingCommand_EmptyContractId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.OptionTickStreamingFeedId;
        var command = new StopFuturesOptionTickDataStreamingCommand(SampleData.OptionTickStreamingFeedId, string.Empty)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertCommand_InvalidContract_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var invalidContract = SampleData.EsContract with { ContractId = string.Empty };
        var command = new InsertFuturesOptionTickDataCommand(invalidContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertCommand_InvalidOptionTickData_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var invalidTickData = SampleData.EsOptionTickData with { ContractId = string.Empty };
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, invalidTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenThreadIdIsDefault()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act — ActorThreadId is a readonly record struct; default is a valid (non-null) value
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, default, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesOptionTickDataCommandActor.ActorName} commands from message:*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepositoryReturnsValidState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var expectedState = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        // Initialize the repo via OnStartup
        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesOptionTickDataCommandState>();
        (result as FuturesOptionTickDataCommandState)!.Id.Should().Be(expectedState.Id);
    }

    [Fact]
    public async Task OnLoadStateAsync_CallsRepositoryLoadState_WithCorrectCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var expectedState = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        await repo.Received(1).LoadStateAsync(Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenThreadIdIsDefault()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var expectedState = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        // Act — ActorThreadId is a readonly record struct; default is a valid (non-null) value
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, default, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns<FuturesOptionTickDataCommandState>(_ => throw new InvalidOperationException("Repository load failed"));

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesOptionTickDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await repo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(c => c == context),
            Arg.Is<FuturesOptionTickDataCommandState>(s => s == state),
            Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    [Fact]
    public async Task OnSaveStateAsync_CompletesSuccessfully_WhenCalledWithValidParameters()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesOptionTickDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenThreadIdIsDefault()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesOptionTickDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        // Act — ActorThreadId is a readonly record struct; default is a valid (non-null) value
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, default, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var state = new FuturesOptionTickDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "test-thread") };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionTickDataCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesOptionTickDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var wrongState = Substitute.For<IActorState>();
        wrongState.Id.Returns(cmd.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, wrongState, cmd);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenRepositoryThrows()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var cmd = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesOptionTickDataCommandState { Id = cmd.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesOptionTickDataCommandState>(), Arg.Any<ICommand>())
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("Repository save failed"))));

        await ((ICommandActor<FuturesOptionTickDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

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
    public async Task OnExceptionAsync_PreservesCommandIdInErrorEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataCommandActor(dbEventSource, logger);

        var entityId = SampleData.EsOptionTickData.EntityId;
        var command = new InsertFuturesOptionTickDataCommand(SampleData.EsContract, SampleData.EsOptionTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

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

    #endregion
}
