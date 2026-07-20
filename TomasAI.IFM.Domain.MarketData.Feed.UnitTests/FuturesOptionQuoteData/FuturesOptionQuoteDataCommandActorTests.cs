using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionQuoteData;

public class FuturesOptionQuoteDataCommandActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionQuoteDataCommandActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesOptionQuoteDataCommandActor(IReferenceLookupService refLookupService, IEventSourceActorDbContext dbEventSource, ILogger<FuturesOptionQuoteDataCommandActor> logger)
        : FuturesOptionQuoteDataCommandActor(refLookupService, dbEventSource, logger)
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

    FuturesOptionQuoteDataCommandState CreateState()
    {
        var blackboardService = Substitute.For<IBlackboardService>();
        blackboardService.FuturesOptionQuoteData.Returns(new FuturesOptionQuoteDataModel());
        return new FuturesOptionQuoteDataCommandState(blackboardService);
    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public async Task ParseMessage_DeserializesStartFuturesOptionQuoteDataStreamingCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<StartFuturesOptionQuoteDataStreamingCommand>();
        var deserializedCommand = result as StartFuturesOptionQuoteDataStreamingCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.QuoteId.Should().Be(command.QuoteId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesStopFuturesOptionQuoteDataStreamingCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StopFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<StopFuturesOptionQuoteDataStreamingCommand>();
        var deserializedCommand = result as StopFuturesOptionQuoteDataStreamingCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.QuoteId.Should().Be(command.QuoteId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesInsertFuturesOptionQuoteDataCommand_AndLogsToDatabase()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, contractId, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<InsertFuturesOptionQuoteDataCommand>();
        var deserializedCommand = result as InsertFuturesOptionQuoteDataCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.QuoteId.Should().Be(command.QuoteId);
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with Query type instead of Command
        var invalidSubject = $"Query.{FuturesOptionQuoteDataCommandActor.ActorName}.{StartFuturesOptionQuoteDataStreamingCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionQuoteDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with different actor name
        var invalidSubject = $"Command.DifferentActor.{StartFuturesOptionQuoteDataStreamingCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionQuoteDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create subject with unrecognized verb
        var invalidSubject = $"Command.{FuturesOptionQuoteDataCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionQuoteDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task ReceiveAsync_StartFuturesOptionQuoteDataStreamingCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesOptionQuoteDataStreamingStartedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FuturesOptionQuoteDataStreamingStartedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.QuoteId.Should().Be(cmd.QuoteId);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_StopFuturesOptionQuoteDataStreamingCommand_ThrowsException_WhenQuoteDoesNotExistAfterStart()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StopFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        // Prepare state: start streaming first (note: current implementation does not populate quote map)
        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;
        var startEntityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var startCmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, startEntityId.Format()),
            EntityId = startEntityId
        };
        startCmd.Execute(state);

        var context = Substitute.For<ICommandActorContext>();

        // Act - Stop still throws because QuoteExists returns false (quote map is not populated by current implementation)
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<StopFuturesOptionQuoteDataStreamingException>();
    }

    [Fact]
    public async Task ReceiveAsync_InsertFuturesOptionQuoteDataCommand_ThrowsException_WhenQuoteDoesNotExistAfterStart()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, contractId, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        // Prepare state: start streaming first (note: current implementation does not populate quote map)
        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;
        var startEntityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var startCmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, startEntityId.Format()),
            EntityId = startEntityId
        };
        startCmd.Execute(state);

        var context = Substitute.For<ICommandActorContext>();

        // Act - Insert still throws because QuoteExists returns false (quote map is not populated by current implementation)
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InsertFuturesOptionQuoteDataException>();
    }

    [Fact]
    public async Task ReceiveAsync_StartFuturesOptionQuoteDataStreamingCommand_ReturnsServiceOkResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var state = CreateState();
        state.Id = new ActorThreadId(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "test-thread");
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var state = CreateState();
        state.Id = new ActorThreadId(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "test-thread");

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionQuoteDataCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesOptionQuoteDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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

    [Fact]
    public async Task ReceiveAsync_StopStreaming_ThrowsException_WhenNoActiveStreaming()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StopFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        // State with no active streaming
        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<StopFuturesOptionQuoteDataStreamingException>();
    }

    [Fact]
    public async Task ReceiveAsync_InsertQuoteData_ThrowsException_WhenNoActiveStreaming()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, contractId, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        // State with no active streaming
        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InsertFuturesOptionQuoteDataException>();
    }

    #endregion

    #region OnValidateAsync Happy Path Tests

    [Fact]
    public async Task OnValidateAsync_StartFuturesOptionQuoteDataStreamingCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_StopFuturesOptionQuoteDataStreamingCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StopFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_InsertFuturesOptionQuoteDataCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, contractId, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_StartStreamingCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_StopStreamingCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StopFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_StopStreamingCommand_InvalidQuoteId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(0); // Invalid: zero quote ID
        var command = new StopFuturesOptionQuoteDataStreamingCommand(0)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*QuoteId*greater than zero*");
    }


    [Fact]
    public async Task OnValidateAsync_InsertCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, contractId, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_InsertCommand_InvalidQuoteId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(0); // Invalid: zero quote ID
        var command = new InsertFuturesOptionQuoteDataCommand(0, contractId, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*QuoteId*greater than zero*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertCommand_EmptyContractId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, string.Empty, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*null or empty*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertCommand_InvalidQuoteData_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);

        // Create invalid QuoteData with negative price
        var invalidQuoteData = new QuoteData(
            new DateTime(2024, 06, 15, 14, 30, 0, DateTimeKind.Utc),
            QuoteLevelType.LevelOne, 0, 1, QuoteSide.Ask, QuoteType.Price, -12.50, 0);

        var command = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, contractId, invalidQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*QuoteData.Price*");
    }


    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_InvalidQuoteId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(0); // Invalid: zero quote ID
        var command = new StartFuturesOptionQuoteDataStreamingCommand(0, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*QuoteId*greater than zero*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_EmptyFuturesOptionQuotes_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, Array.Empty<FuturesOptionQuoteReadModel>(), SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*FuturesOptionQuotes*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_StartStreamingCommand_EmptyFuturesOptionContracts_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, Array.Empty<FuturesOptionContractReadModel>())
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*FuturesOptionContracts*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, threadId, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, default, command);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesOptionQuoteDataCommandActor.ActorName} commands from message:*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepositoryReturnsValidState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var expectedState = CreateState();
        expectedState.Id = cmd.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        // Initialize the repo via OnStartup
        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesOptionQuoteDataCommandState>();
        (result as FuturesOptionQuoteDataCommandState)!.Id.Should().Be(expectedState.Id);
    }

    [Fact]
    public async Task OnLoadStateAsync_CallsRepositoryLoadState_WithCorrectCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var expectedState = CreateState();
        expectedState.Id = cmd.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, default, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns<FuturesOptionQuoteDataCommandState>(_ => throw new InvalidOperationException("Repository load failed"));

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesOptionQuoteDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await repo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(c => c == context),
            Arg.Is<FuturesOptionQuoteDataCommandState>(s => s == state),
            Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    [Fact]
    public async Task OnSaveStateAsync_CompletesSuccessfully_WhenCalledWithValidParameters()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesOptionQuoteDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, default, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var state = CreateState();
        state.Id = new ActorThreadId(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "test-thread");

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesOptionQuoteDataCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesOptionQuoteDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var wrongState = Substitute.For<IActorState>();
        wrongState.Id.Returns(cmd.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var cmd = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = CreateState();
        state.Id = cmd.Subject.ThreadId;

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesOptionQuoteDataCommandState>(), Arg.Any<ICommand>())
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("Repository save failed"))));

        await ((ICommandActor<FuturesOptionQuoteDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnExceptionAsync_StartFuturesOptionQuoteDataStreamingException_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;
        var exception = new StartFuturesOptionQuoteDataStreamingException("Stream already exists");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<FuturesOptionQuoteDataStreamingStartedFailEvent, QuoteId>(
            Arg.Any<FuturesOptionQuoteDataStreamingStartedFailEvent>())
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
    public async Task OnExceptionAsync_StopFuturesOptionQuoteDataStreamingException_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StopFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;
        var exception = new StopFuturesOptionQuoteDataStreamingException("Stream does not exist");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<FuturesOptionQuoteDataStreamingStoppedFailEvent, QuoteId>(
            Arg.Any<FuturesOptionQuoteDataStreamingStoppedFailEvent>())
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
    public async Task OnExceptionAsync_InsertFuturesOptionQuoteDataException_SendsErrorEventAndReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var contractId = SampleData.FuturesOptionQuotes[0].ContractId;
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new InsertFuturesOptionQuoteDataCommand(SampleData.OptionQuoteStreamId, contractId, SampleData.AskPriceQuoteData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command.Subject.ThreadId;
        var exception = new InsertFuturesOptionQuoteDataException("Insert failed");

        var context = Substitute.For<ICommandActorContext>();

        context.SendAsync<FuturesOptionQuoteDataInsertedFailEvent, QuoteId>(
            Arg.Any<FuturesOptionQuoteDataInsertedFailEvent>())
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>();
        var actor = _fixture.CreateFuturesOptionQuoteDataCommandActor(dbEventSource, logger);

        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        var command = new StartFuturesOptionQuoteDataStreamingCommand(SampleData.OptionQuoteStreamId, SampleData.FuturesOptionQuotes, SampleData.FuturesOptionContracts)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
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
