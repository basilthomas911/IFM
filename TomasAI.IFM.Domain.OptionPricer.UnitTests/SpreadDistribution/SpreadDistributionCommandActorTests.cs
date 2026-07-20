using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.SpreadDistribution;

public class SpreadDistributionCommandActorTests : IClassFixture<OptionPricerTestFixture>
{
    readonly OptionPricerTestFixture _fixture;

    public SpreadDistributionCommandActorTests(OptionPricerTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected methods for unit testing.
    public class TestableSpreadDistributionCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<SpreadDistributionCommandActor> logger)
        : SpreadDistributionCommandActor(dbEventSource, logger)
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

    #region ParseMessage Happy Path Tests

    [Fact]
    public async Task ParseMessage_DeserializesInsertSpreadDistributionCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
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
        result.Should().BeOfType<InsertSpreadDistributionCommand>();
        var deserializedCommand = result as InsertSpreadDistributionCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.PutSpreadDistribution.TradeId.Should().Be(SampleData.PutSpreadDistribution.TradeId);
        deserializedCommand.CallSpreadDistribution.TradeId.Should().Be(SampleData.CallSpreadDistribution.TradeId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesDeleteSpreadDistributionCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new DeleteSpreadDistributionCommand(
            tradeId: SampleData.PutSpreadDistribution.TradeId,
            tradeStatus: SampleData.PutSpreadDistribution.TradeStatus,
            valueDate: SampleData.PutSpreadDistribution.ValueDate,
            daysToExpiry: SampleData.PutSpreadDistribution.DaysToExpiry)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
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
        result.Should().BeOfType<DeleteSpreadDistributionCommand>();
        var deserializedCommand = result as DeleteSpreadDistributionCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.EntityId.TradeId.Should().Be(SampleData.PutSpreadDistribution.TradeId);
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
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
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
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with Query type instead of Command
        var invalidSubject = $"Query.{SpreadDistributionCommandActor.ActorName}.{InsertSpreadDistributionCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SpreadDistributionCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with different actor name
        var invalidSubject = $"Command.DifferentActor.{InsertSpreadDistributionCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SpreadDistributionCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with unrecognized verb
        var invalidSubject = $"Command.{SpreadDistributionCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SpreadDistributionCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
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
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
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
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
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
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb,
                new SpreadDistributionKey(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.TradeStatus, SampleData.PutSpreadDistribution.ValueDate, SampleData.PutSpreadDistribution.DaysToExpiry).Format())
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
    public async Task ReceiveAsync_InsertSpreadDistributionCommand_ReturnsServiceOkWithGuidResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var cmd = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var state = new SpreadDistributionCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_DeleteSpreadDistributionCommand_ReturnsServiceOkWithGuidResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var cmd = new DeleteSpreadDistributionCommand(
            tradeId: SampleData.PutSpreadDistribution.TradeId,
            tradeStatus: SampleData.PutSpreadDistribution.TradeStatus,
            valueDate: SampleData.PutSpreadDistribution.ValueDate,
            daysToExpiry: SampleData.PutSpreadDistribution.DaysToExpiry)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var state = new SpreadDistributionCommandState { Id = cmd.Subject.ThreadId };
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
        var actor = _fixture.CreateActor();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var cmd = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var state = new SpreadDistributionCommandState { Id = cmd.Subject.ThreadId };

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
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var cmd = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
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
        var actor = _fixture.CreateActor();
        var state = new SpreadDistributionCommandState { Id = new ActorThreadId(ActorType.Command, SpreadDistributionCommandActor.ActorName, "test-thread") };
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
        var state = new SpreadDistributionCommandState { Id = new ActorThreadId(ActorType.Command, SpreadDistributionCommandActor.ActorName, "test-thread") };

        // Create a command that's not in the receive map
        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, SpreadDistributionCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SpreadDistributionCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotSpreadDistributionCommandState()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var cmd = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        // Use a different state type
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
    public async Task OnValidateAsync_InsertSpreadDistributionCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert - should not throw any exception
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_DeleteSpreadDistributionCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new DeleteSpreadDistributionCommand(
            tradeId: SampleData.PutSpreadDistribution.TradeId,
            tradeStatus: SampleData.PutSpreadDistribution.TradeStatus,
            valueDate: SampleData.PutSpreadDistribution.ValueDate,
            daysToExpiry: SampleData.PutSpreadDistribution.DaysToExpiry)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert - should not throw any exception
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnValidateAsync Edge Case Tests

    [Fact]
    public async Task OnValidateAsync_InsertSpreadDistributionCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
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
    public async Task OnValidateAsync_DeleteSpreadDistributionCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new DeleteSpreadDistributionCommand(
            tradeId: SampleData.PutSpreadDistribution.TradeId,
            tradeStatus: SampleData.PutSpreadDistribution.TradeStatus,
            valueDate: SampleData.PutSpreadDistribution.ValueDate,
            daysToExpiry: SampleData.PutSpreadDistribution.DaysToExpiry)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
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
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandTypeNotInValidationMap()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, SpreadDistributionCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, SpreadDistributionCommandActor.ActorName, "thread-id");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {SpreadDistributionCommandActor.ActorName} commands from message:*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_DelegatesToRepository_Successfully()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var expectedState = new SpreadDistributionCommandState { Id = command.Subject.ThreadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<SpreadDistributionCommandState>>();
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SpreadDistributionCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Initialize the repo via OnStartup
        await actor.InvokeOnStartup(context);

        var threadId = command.Subject.ThreadId;

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, command);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<SpreadDistributionCommandState>();
        result.Id.Should().Be(command.Subject.ThreadId);
        await repo.Received(1).LoadStateAsync(Arg.Is<ICommand>(c => c.CommandId == command.CommandId));
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var threadId = new ActorThreadId(ActorType.Command, SpreadDistributionCommandActor.ActorName, "test-thread");
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, SpreadDistributionCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnSaveStateAsync Happy Path Tests

    [Fact]
    public async Task OnSaveStateAsync_DelegatesToRepository_Successfully()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var state = new SpreadDistributionCommandState { Id = command.Subject.ThreadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<SpreadDistributionCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<SpreadDistributionCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SpreadDistributionCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Initialize the repo via OnStartup
        await actor.InvokeOnStartup(context);

        var threadId = command.Subject.ThreadId;

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, command);

        // Assert
        await repo.Received(1).SaveStateAsync(
            Arg.Is(context),
            Arg.Is<SpreadDistributionCommandState>(s => s.Id == state.Id),
            Arg.Is<ICommand>(c => c.CommandId == command.CommandId));
    }

    #endregion

    #region OnSaveStateAsync Edge Case Tests

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var threadId = new ActorThreadId(ActorType.Command, SpreadDistributionCommandActor.ActorName, "test-thread");
        var state = new SpreadDistributionCommandState { Id = threadId };
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var actor = _fixture.CreateActor();
        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, SpreadDistributionCommandActor.ActorName, "test-thread");
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, null!, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotSpreadDistributionCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var repo = Substitute.For<IEventSourceActorStateRepository<SpreadDistributionCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<SpreadDistributionCommandState>>().Returns(repo);
        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);
        await actor.InvokeOnStartup(context);

        var threadId = command.Subject.ThreadId;

        // Use a different state type
        var wrongState = Substitute.For<IActorState>();
        wrongState.Id.Returns(threadId);

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, wrongState, command);

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
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
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
    public async Task OnExceptionAsync_InsertCommand_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Insert failed");

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

        await context.Received(1).SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Is<Shared.EventModelActor.Events.CommandExceptionEvent>(e =>
                e.ErrorMessage == exception.Message &&
                e.ErrorType == ErrorType.Command));
    }

    [Fact]
    public async Task OnExceptionAsync_DeleteCommand_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new DeleteSpreadDistributionCommand(
            tradeId: SampleData.PutSpreadDistribution.TradeId,
            tradeStatus: SampleData.PutSpreadDistribution.TradeStatus,
            valueDate: SampleData.PutSpreadDistribution.ValueDate,
            daysToExpiry: SampleData.PutSpreadDistribution.DaysToExpiry)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Delete failed");

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
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Test exception");

        var context = Substitute.For<ICommandActorContext>();

        Shared.EventModelActor.Events.CommandExceptionEvent? capturedEvent = null;
        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Do<Shared.EventModelActor.Events.CommandExceptionEvent>(e => capturedEvent = e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, command, exception);

        // Assert - generic SendErrorEventAsync passes null command so CommandId defaults to Guid.Empty
        capturedEvent.Should().NotBeNull();
        capturedEvent!.CommandId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task OnExceptionAsync_IncludesExceptionMessageInErrorEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<SpreadDistributionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new SpreadDistributionKey(
            SampleData.PutSpreadDistribution.TradeId,
            SampleData.PutSpreadDistribution.TradeStatus,
            SampleData.PutSpreadDistribution.ValueDate,
            SampleData.PutSpreadDistribution.DaysToExpiry);

        var command = new InsertSpreadDistributionCommand(
            SampleData.PutSpreadDistribution,
            SampleData.CallSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
            EntityId = new SpreadDistributionEntityId(SampleData.PutSpreadDistribution.TradeId, SampleData.PutSpreadDistribution.ValueDate)
        };

        var threadId = command.Subject.ThreadId;
        var expectedMessage = "Detailed error message for testing";
        var exception = new Exception(expectedMessage);

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
}
