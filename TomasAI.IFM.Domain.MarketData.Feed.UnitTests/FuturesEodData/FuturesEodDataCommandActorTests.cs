using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Exceptions;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData;

public class FuturesEodDataCommandActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesEodDataCommandActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesEodDataCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesEodDataCommandActor> logger)
        : FuturesEodDataCommandActor(dbEventSource, logger)
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
    public async Task ParseMessage_DeserializesInsertFuturesEodDataCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
        result.Should().BeOfType<InsertFuturesEodDataCommand>();
        var deserializedCommand = result as InsertFuturesEodDataCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.ValueDate.Should().Be(command.ValueDate);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesInsertVixFuturesEodDataCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.VixTickData.ContractId ?? string.Empty, SampleData.VixTickData.ValueDate);
        var command = new InsertVixFuturesEodDataCommand(SampleData.VixTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertVixFuturesEodDataCommand;

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
        result.Should().BeOfType<InsertVixFuturesEodDataCommand>();
        var deserializedCommand = result as InsertVixFuturesEodDataCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.VixFuturesTickData.ContractId.Should().Be(command.VixFuturesTickData.ContractId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>());

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_PreservesCommandIdAcrossSerialization()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var invalidSubject = $"Query.{FuturesEodDataCommandActor.ActorName}.{InsertFuturesEodDataCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var invalidSubject = $"Command.DifferentActor.{InsertFuturesEodDataCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var payload = ActorExtensions.DataSerializer.Serialize(command!);
        var invalidSubject = $"Command.{FuturesEodDataCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
    public async Task ReceiveAsync_InsertFuturesEodDataCommand_ExecutesHandlerAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        cmd.Should().NotBeNull();

        var state = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesEodDataInsertedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FuturesEodDataInsertedEvent>().First();
        evt.Should().NotBeNull();
        evt.FuturesEodData.ContractId.Should().Be(cmd.Contract.ContractId);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_InsertVixFuturesEodDataCommand_ExecutesHandlerAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.VixTickData.ContractId ?? string.Empty, SampleData.VixTickData.ValueDate);
        var cmd = new InsertVixFuturesEodDataCommand(SampleData.VixTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertVixFuturesEodDataCommand;

        cmd.Should().NotBeNull();

        var state = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is VixFuturesEodDataInsertedEvent).Should().BeTrue();

        var evt = state.Events.OfType<VixFuturesEodDataInsertedEvent>().First();
        evt.Should().NotBeNull();
        evt.VixFuturesTickData.ContractId.Should().Be(cmd.VixFuturesTickData.ContractId);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var state = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FuturesEodDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesEodDataCommandActor.ActorName, "test-thread") };
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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FuturesEodDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesEodDataCommandActor.ActorName, "test-thread") };

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesEodDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesEodDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var state = Substitute.For<IActorState>();
        state.Id.Returns(cmd!.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ReceiveAsync_MultipleCommands_AccumulatesEventsInState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId1 = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd1 = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId1.Format()),
            EntityId = entityId1
        } as InsertFuturesEodDataCommand;

        var entityId2 = new FuturesEodDataId(SampleData.VixTickData.ContractId ?? string.Empty, SampleData.VixTickData.ValueDate);
        var cmd2 = new InsertVixFuturesEodDataCommand(SampleData.VixTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId2.Format()),
            EntityId = entityId2
        } as InsertVixFuturesEodDataCommand;

        var state = new FuturesEodDataCommandState { Id = cmd1!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, state, cmd1!);
        await actor.InvokeReceiveAsync(context, state, cmd2!);

        // Assert
        state.Events.Should().HaveCount(2);
        state.Events.OfType<FuturesEodDataInsertedEvent>().Should().HaveCount(1);
        state.Events.OfType<VixFuturesEodDataInsertedEvent>().Should().HaveCount(1);
    }

    #endregion

    #region OnValidateAsync Happy Path Tests

    [Fact]
    public async Task OnValidateAsync_InsertFuturesEodDataCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_InsertVixFuturesEodDataCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.VixTickData.ContractId ?? string.Empty, SampleData.VixTickData.ValueDate);
        var command = new InsertVixFuturesEodDataCommand(SampleData.VixTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertVixFuturesEodDataCommand;

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
    public async Task OnValidateAsync_InsertFuturesEodDataCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertVixFuturesEodDataCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.VixTickData.ContractId ?? string.Empty, SampleData.VixTickData.ValueDate);
        var command = new InsertVixFuturesEodDataCommand(SampleData.VixTickData)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertVixFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*CommandId*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertVixFuturesEodDataCommand_InvalidVixFuturesTickData_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidTickData = new FuturesTickDataV2ReadModel(
            contractId: string.Empty,  // Invalid: empty contract ID
            valueDate: SampleData.ValueDate,
            tickId: 0,
            tickTime: new TimeOnly(14, 30, 0),
            price: -100m,  // Invalid: negative price
            size: 0);      // Invalid: zero size

        var entityId = new FuturesEodDataId("VX", SampleData.ValueDate);
        var command = new InsertVixFuturesEodDataCommand(invalidTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertVixFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId is required and cannot be empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandTypeNotInValidationMap()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesEodDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesEodDataCommandActor.ActorName, "thread-id");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesEodDataCommandActor.ActorName} commands from message:*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepositoryReturnsValidState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var expectedState = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesEodDataCommandState>();
        (result as FuturesEodDataCommandState)!.Id.Should().Be(expectedState.Id);
    }

    [Fact]
    public async Task OnLoadStateAsync_CallsRepositoryLoadState_WithCorrectCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var expectedState = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesEodDataCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns<FuturesEodDataCommandState>(_ => throw new InvalidOperationException("Repository load failed"));

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var state = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesEodDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd!);

        // Assert
        await repo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(c => c == context),
            Arg.Is<FuturesEodDataCommandState>(s => s == state),
            Arg.Is<ICommand>(c => c.CommandId == cmd!.CommandId));
    }

    [Fact]
    public async Task OnSaveStateAsync_CompletesSuccessfully_WhenCalledWithValidParameters()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var state = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesEodDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var state = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, null!, cmd!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesEodDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var wrongState = Substitute.For<IActorState>();
        wrongState.Id.Returns(cmd!.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var cmd = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var state = new FuturesEodDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesEodDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesEodDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesEodDataCommandState>(), Arg.Any<ICommand>())
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("Repository save failed"))));

        await ((ICommandActor<FuturesEodDataCommandActor>)actor).OnStartup(context);

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
    public async Task OnExceptionAsync_InsertFuturesEodDataException_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var threadId = command!.Subject.ThreadId;
        var exception = new InsertFuturesEodDataException("Insert futures EOD data failed");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<FuturesEodDataInsertedFailEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
        failedResult.ErrorCode.Should().Be(command.ErrorCode);

        await context.Received(1).SendAsync<FuturesEodDataInsertedFailEvent, FuturesEodDataId>(
            Arg.Is<FuturesEodDataInsertedFailEvent>(e =>
                e.CommandId == command.CommandId &&
                e.ErrorMessage == exception.Message &&
                e.ErrorType == ErrorType.Command));
    }

    [Fact]
    public async Task OnExceptionAsync_InsertVixFuturesEodDataException_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.VixTickData.ContractId ?? string.Empty, SampleData.VixTickData.ValueDate);
        var command = new InsertVixFuturesEodDataCommand(SampleData.VixTickData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertVixFuturesEodDataCommand;

        var threadId = command!.Subject.ThreadId;
        var exception = new InsertVixFuturesEodDataException("Insert VIX futures EOD data failed");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<VixFuturesEodDataInsertedFailEvent, FuturesEodDataId>(Arg.Any<VixFuturesEodDataInsertedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult.Should().NotBeNull();
        failedResult!.Success.Should().BeFalse();
        failedResult.ErrorCode.Should().Be(command.ErrorCode);

        await context.Received(1).SendAsync<VixFuturesEodDataInsertedFailEvent, FuturesEodDataId>(
            Arg.Is<VixFuturesEodDataInsertedFailEvent>(e =>
                e.CommandId == command.CommandId &&
                e.ErrorMessage == exception.Message &&
                e.ErrorType == ErrorType.Command));
    }

    [Fact]
    public async Task OnExceptionAsync_GenericException_SendsCommandExceptionEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

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
    public async Task OnExceptionAsync_PreservesCommandIdInErrorEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var threadId = command!.Subject.ThreadId;
        var exception = new InsertFuturesEodDataException("Test exception");

        var context = Substitute.For<ICommandActorContext>();

        FuturesEodDataInsertedFailEvent? capturedEvent = null;
        context.SendAsync<FuturesEodDataInsertedFailEvent, FuturesEodDataId>(Arg.Do<FuturesEodDataInsertedFailEvent>(e => capturedEvent = e))
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
        var logger = Substitute.For<ILogger<FuturesEodDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesEodDataId(SampleData.EsContract.ContractId ?? string.Empty, SampleData.ValueDate);
        var command = new InsertFuturesEodDataCommand(
                SampleData.ValueDate, SampleData.EsTickData, SampleData.EsContract,
                SampleData.EodDataToday, SampleData.EodDataRange, SampleData.NormCurveData,
                SampleData.WindowSize, SampleData.VixEodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        } as InsertFuturesEodDataCommand;

        var threadId = command!.Subject.ThreadId;
        var expectedMessage = "Detailed error message for testing";
        var exception = new InsertFuturesEodDataException(expectedMessage);

        var context = Substitute.For<ICommandActorContext>();

        FuturesEodDataInsertedFailEvent? capturedEvent = null;
        context.SendAsync<FuturesEodDataInsertedFailEvent, FuturesEodDataId>(Arg.Do<FuturesEodDataInsertedFailEvent>(e => capturedEvent = e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.ErrorMessage.Should().Be(expectedMessage);
    }

    #endregion
}
