using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal;

public class FuturesTradeSignalCommandActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesTradeSignalCommandActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTradeSignalCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesTradeSignalCommandActor> logger)
        : FuturesTradeSignalCommandActor(dbEventSource, logger)
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

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task ParseMessage_DeserializesUpdateFuturesTradeSignalCommand_AndLogsToDatabase(
        TradeTimePeriodType timePeriod)
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommandFor(timePeriod);

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
        result.Should().BeOfType<UpdateFuturesTradeSignalCommand>();
        var deserializedCommand = result as UpdateFuturesTradeSignalCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.TimePeriod.Should().Be(timePeriod);
        deserializedCommand.EntityId.Should().Be(SampleData.TradeSignalEntityIdFor(timePeriod));
        deserializedCommand.FuturesEodData.ContractId.Should().Be(command.FuturesEodData.ContractId);
        deserializedCommand.FuturesEodData.ValueDate.Should().Be(command.FuturesEodData.ValueDate);
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var command = SampleData.CreateTradeSignalUpdateCommand() with { CommandId = expectedCommandId };

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
    public async Task ParseMessage_PreservesEodDataProperties_AfterDeserialization()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommand();

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
        var deserialized = result as UpdateFuturesTradeSignalCommand;
        deserialized.Should().NotBeNull();
        deserialized!.FuturesEodData.ClosePrice.Should().Be(command.FuturesEodData.ClosePrice);
        deserialized.FuturesEodData.OpenPrice.Should().Be(command.FuturesEodData.OpenPrice);
        deserialized.FuturesEodData.HighPrice.Should().Be(command.FuturesEodData.HighPrice);
        deserialized.FuturesEodData.LowPrice.Should().Be(command.FuturesEodData.LowPrice);

        await Task.CompletedTask;
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorTypeIsNotCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.TradeSignalUpdateCommand;
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Query.{FuturesTradeSignalCommandActor.ActorName}.{UpdateFuturesTradeSignalCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.TradeSignalUpdateCommand;
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Command.DifferentActor.{UpdateFuturesTradeSignalCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.TradeSignalUpdateCommand;
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        var invalidSubject = $"Command.{FuturesTradeSignalCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.TradeSignalUpdateCommand;
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.TradeSignalUpdateCommand;
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.TradeSignalUpdateCommand;
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.TradeSignalUpdateCommand;
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

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task ReceiveAsync_UpdateFuturesTradeSignalCommand_ExecutesHandler_ReturnsGuid(
        TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommandFor(timePeriod);

        var state = new FuturesTradeSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
        cmd.TimePeriod.Should().Be(timePeriod);
        state.Events.Should().ContainSingle(e => e is FuturesTradeSignalUpdatedEvent);
    }

    [Fact]
    public async Task ReceiveAsync_UpdateFuturesTradeSignalCommand_ProducesUpdatedEvent_WhenSignalChanged()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();

        // State with no prior signal - HasFuturesTradeSignalChanged returns true
        var state = new FuturesTradeSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesTradeSignalUpdatedEvent).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_UpdateFuturesTradeSignalCommand_NoChange_DoesNotProduceEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();

        var state = new FuturesTradeSignalCommandState { Id = cmd.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Apply same command twice - second should not produce event since signal is unchanged
        await actor.InvokeReceiveAsync(context, state, cmd);
        var eventCountAfterFirst = state.Events.Count;

        // Create identical command (same EodData => same computed signal)
        var cmd2 = SampleData.CreateTradeSignalUpdateCommand();

        // Act
        await actor.InvokeReceiveAsync(context, state, cmd2);

        // Assert - no new events should be added since signal hasn't changed
        state.Events.Count.Should().Be(eventCountAfterFirst);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
        var state = new FuturesTradeSignalCommandState { Id = cmd.Subject.ThreadId };

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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FuturesTradeSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "test-thread") };
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FuturesTradeSignalCommandState { Id = new ActorThreadId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "test-thread") };

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesTradeSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();

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

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task OnValidateAsync_ValidCommand_DoesNotThrow(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommandFor(timePeriod, vixFuturesPrice: 15.75m);
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
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenCommandIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand() with { CommandId = Guid.Empty };
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "test-thread");
        var context = Substitute.For<ICommandActorContext>();

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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesTradeSignalCommandActor.ActorName} commands from message:*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenVixFuturesPriceIsZero()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand(vixFuturesPrice: 0m);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*VixFuturesPrice must be greater than zero*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenVixFuturesPriceIsNegative()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand(vixFuturesPrice: -5.0m);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*VixFuturesPrice must be greater than zero*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenVixFuturesPriceExceedsMaximum()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand(vixFuturesPrice: 250m);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*VixFuturesPrice exceeds reasonable maximum of 200*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenVixFuturesPriceIsUnreasonablyLow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand(vixFuturesPrice: 0.005m);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*VixFuturesPrice is unreasonably low*");
    }

    [Fact]
    public async Task OnValidateAsync_ValidCommand_WithValidVixFuturesPrice_DoesNotThrow()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand(vixFuturesPrice: 15.75m);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenFuturesEodDataVolumeIsNegative()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidEodData = SampleData.EodData with { Volume = -100 };
        var cmd = SampleData.CreateTradeSignalUpdateCommand(eodData: invalidEodData, vixFuturesPrice: 15.75m);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*FuturesEodDataV2.Volume must be non-negative*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenFuturesEodDataContractIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidEodData = SampleData.EodData with { ContractId = string.Empty };
        var cmd = SampleData.CreateTradeSignalUpdateCommand(eodData: invalidEodData, vixFuturesPrice: 15.75m);
        var threadId = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*FuturesEodDataV2.ContractId is required*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsCommandValidationException_WhenTimePeriodIsInvalid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand(vixFuturesPrice: 15.75m);
        // Create command with invalid enum value by using reflection or direct cast
        var invalidCmd = cmd with { TimePeriod = (TradeTimePeriodType)999 };
        var threadId = invalidCmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, invalidCmd);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*TimePeriod has an invalid value*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task OnLoadStateAsync_LoadsState_FromRepository(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommandFor(timePeriod);
        var threadId = cmd.Subject.ThreadId;

        var expectedState = new FuturesTradeSignalCommandState { Id = threadId };
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>();
        repo.LoadStateAsync(cmd).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Invoke OnStartup to set _repo
        await actor.InvokeOnStartup(context);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedState);
        await repo.Received(1).LoadStateAsync(cmd);
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "test-thread");
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, default, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnSaveStateAsync Happy Path Tests

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task OnSaveStateAsync_SavesState_ToRepository(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommandFor(timePeriod);
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesTradeSignalCommandState { Id = threadId };

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>();
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesTradeSignalCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>().Returns(repo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        // Invoke OnStartup to set _repo
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = new FuturesTradeSignalCommandState { Id = threadId };

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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
        var state = new FuturesTradeSignalCommandState { Id = cmd.Subject.ThreadId };
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "test-thread");
        var state = new FuturesTradeSignalCommandState { Id = threadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesTradeSignalCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = SampleData.CreateTradeSignalUpdateCommand();
        var threadId = cmd.Subject.ThreadId;
        var invalidState = Substitute.For<IActorState>();
        invalidState.Id.Returns(threadId);

        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>();
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FuturesTradeSignalCommandState>>().Returns(repo);
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

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task OnExceptionAsync_GenericException_SendsCommandExceptionEventAndReturnsFailedResult(
        TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommandFor(timePeriod);
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommand();
        var threadId = command.Subject.ThreadId;
        var expectedMessage = "Trade signal update failed";
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

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenContextIsNull_FallsBackToCommandFailed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommand();
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommand();
        var threadId = command.Subject.ThreadId;
        var exception = new Exception("Original error");

        var context = Substitute.For<ICommandActorContext>();

        // First SendAsync call fails, second also fails -> CommandFailed fallback
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
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommand();
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
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WithNullCommand_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var threadId = new ActorThreadId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName, "test-thread");
        var exception = new InvalidOperationException("Test error");
        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, null!, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        var failedResult = result as ServiceFailed<GuidResult>;
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WithCommandValidationException_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommand();
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
        failedResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WithTimeoutException_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesTradeSignalCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var command = SampleData.CreateTradeSignalUpdateCommand();
        var threadId = command.Subject.ThreadId;
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
        failedResult!.Success.Should().BeFalse();
    }

    #endregion
}
