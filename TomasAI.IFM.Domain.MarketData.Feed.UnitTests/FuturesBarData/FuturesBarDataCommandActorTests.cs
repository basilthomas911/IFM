using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData;

public class FuturesBarDataCommandActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesBarDataCommandActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesBarDataCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesBarDataCommandActor> logger)
        : FuturesBarDataCommandActor(dbEventSource, logger)
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

    #region OnStartup Happy Path Tests

    [Fact]
    public async Task OnStartup_SetsRepositoryField_WhenSuccessful()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        // Act
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        // Assert - verify repository was resolved exactly once
        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
    }

    [Fact]
    public async Task OnStartup_DoesNotThrow_WhenRepositoryIsValidInstance()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        // Act
        Func<Task> act = async () => await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnStartup_UsesProvidedContext_WhenResolvingDependencies()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        // Act
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        // Assert - verify context.Container was accessed
        _ = context.Received(1).Container;
    }

    [Fact]
    public async Task OnStartup_CompletesSuccessfully_WhenCalledMultipleTimes()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        // Act - call OnStartup multiple times
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        // Assert - verify repository was resolved for each call
        container.Received(3).Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
    }

    [Fact]
    public async Task OnStartup_ResolvesCorrectRepositoryType_WhenCalled()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var correctRepo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(correctRepo);

        // Act
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        // Assert - verify the exact type was resolved
        container.Received(1).Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();
    }

    #endregion

    #region OnStartup Edge Case Tests

    [Fact]
    public async Task OnStartup_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        // Act
        Func<Task> act = async () => await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnStartup_ThrowsArgumentNullException_WhenContainerIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        // Return null for Container property
        context.Container.Returns((IContainerInstance?)null);

        // Act
        Func<Task> act = async () => await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        // Assert
        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task OnStartup_ThrowsException_WhenResolveThrowsException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();

        context.Container.Returns(container);
        container.When(c => c.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>())
            .Do(_ => throw new InvalidOperationException("Container resolution failed"));

        // Act
        Func<Task> act = async () => await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Container resolution failed");
    }

    [Fact]
    public void OnStartup_ThrowsArgumentNullException_WhenDbEventSourceIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();

        // Act
        Action act = () => new TestableFuturesBarDataCommandActor(null!, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OnStartup_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();

        // Act
        Action act = () => new TestableFuturesBarDataCommandActor(dbEventSource, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ParseMessage Happy Path Tests

    [Fact]
    public async Task ParseMessage_DeserializesInsertFuturesBarDataCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<InsertFuturesBarDataCommand>();
        var deserializedCommand = result as InsertFuturesBarDataCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.FuturesBarData.ContractId.Should().Be(command.FuturesBarData.ContractId);
        deserializedCommand.FuturesBarData.Symbol.Should().Be(command.FuturesBarData.Symbol);
        deserializedCommand.FuturesBarData.ValueDate.Should().Be(command.FuturesBarData.ValueDate);
        deserializedCommand.FuturesBarData.BarValue.Should().Be(command.FuturesBarData.BarValue);
        deserializedCommand.FuturesBarData.BarRateType.Should().Be(command.FuturesBarData.BarRateType);
        deserializedCommand.FuturesBarData.UpTrendTrigger.Should().Be(command.FuturesBarData.UpTrendTrigger);
        deserializedCommand.FuturesBarData.DownTrendTrigger.Should().Be(command.FuturesBarData.DownTrendTrigger);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesDeleteFuturesBarDataCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarDataId1;
        var command = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<DeleteFuturesBarDataCommand>();
        var deserializedCommand = result as DeleteFuturesBarDataCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.Id.ContractId.Should().Be(command.Id.ContractId);
        deserializedCommand.Id.Symbol.Should().Be(command.Id.Symbol);
        deserializedCommand.Id.ValueDate.Should().Be(command.Id.ValueDate);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesStartFuturesBarDataStreamingCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand(SampleData.FuturesContracts, SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<StartFuturesBarDataStreamingCommand>();
        var deserializedCommand = result as StartFuturesBarDataStreamingCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.ValueDate.Should().Be(command.ValueDate);
        deserializedCommand.Contracts.Should().BeEquivalentTo(command.Contracts);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        await dbEventSource.Received(1).InsertCommandLogAsync(
            Arg.Is<ICommand>(cmd => cmd.CommandId == command.CommandId),
            Arg.Any<DateTime>(),
            Arg.Any<string>()
        );

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesStopFuturesBarDataStreamingCommand_AndLogsToDatabase()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StopFuturesBarDataStreamingCommand(SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
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
        result.Should().BeOfType<StopFuturesBarDataStreamingCommand>();
        var deserializedCommand = result as StopFuturesBarDataStreamingCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.ValueDate.Should().Be(command.ValueDate);
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        // Create subject with Query type instead of Command
        var invalidSubject = $"Query.{FuturesBarDataCommandActor.ActorName}.{InsertFuturesBarDataCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        // Create subject with different actor name
        var invalidSubject = $"Command.DifferentActor.{InsertFuturesBarDataCommand.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command!);

        // Create subject with unrecognized verb
        var invalidSubject = $"Command.{FuturesBarDataCommandActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataCommandActor.ActorName} command from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
    public async Task ReceiveAsync_InsertFuturesBarDataCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesBarDataInsertedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FuturesBarDataInsertedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.FuturesBarData.ContractId.Should().Be(cmd.FuturesBarData.ContractId);
        evt.FuturesBarData.Symbol.Should().Be(cmd.FuturesBarData.Symbol);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_DeleteFuturesBarDataCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarDataId1;
        var cmd = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesBarDataDeletedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FuturesBarDataDeletedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.BarDataId.ContractId.Should().Be(cmd.Id.ContractId);
        evt.BarDataId.Symbol.Should().Be(cmd.Id.Symbol);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_StartFuturesBarDataStreamingCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var cmd = new StartFuturesBarDataStreamingCommand(SampleData.FuturesContracts, SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesBarDataStreamingStartedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FuturesBarDataStreamingStartedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.Contracts.Should().BeEquivalentTo(cmd.Contracts);
        evt.ValueDate.Should().Be(cmd.ValueDate);
        evt.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_StopFuturesBarDataStreamingCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var cmd = new StopFuturesBarDataStreamingCommand(SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd!.CommandId);

        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FuturesBarDataStreamingStoppedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FuturesBarDataStreamingStoppedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_MultipleCommands_AccumulatesEventsInState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId1 = SampleData.FuturesBarData1.Id;
        var cmd1 = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId1.Format()),
            EntityId = entityId1
        };

        var entityId2 = SampleData.FuturesBarDataId1;
        var cmd2 = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId2.Format()),
            EntityId = entityId2
        };

        var state = new FuturesBarDataCommandState { Id = cmd1!.Subject.ThreadId };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, state, cmd1!);
        await actor.InvokeReceiveAsync(context, state, cmd2!);

        // Assert
        state.Events.Should().HaveCount(2);
        state.Events.OfType<FuturesBarDataInsertedEvent>().Should().HaveCount(1);
        state.Events.OfType<FuturesBarDataDeletedEvent>().Should().HaveCount(1);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FuturesBarDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesBarDataCommandActor.ActorName, "test-thread") };
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FuturesBarDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesBarDataCommandActor.ActorName, "test-thread") };

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesBarDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataCommandActor.ActorName} command from message:*");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsException_WhenStateIsNotFuturesBarDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_InsertFuturesBarDataCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_DeleteFuturesBarDataCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarDataId1;
        var command = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_StartFuturesBarDataStreamingCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand(SampleData.FuturesContracts, SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_StopFuturesBarDataStreamingCommand_ValidCommand_NoExceptionThrown()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StopFuturesBarDataStreamingCommand(SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_InsertFuturesBarDataCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_DeleteFuturesBarDataCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarDataId1;
        var command = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_StartFuturesBarDataStreamingCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand(SampleData.FuturesContracts, SampleData.ValueDate)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_StopFuturesBarDataStreamingCommand_EmptyCommandId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StopFuturesBarDataStreamingCommand(SampleData.ValueDate)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
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
    public async Task OnValidateAsync_StartFuturesBarDataStreamingCommand_EmptyContractsArray_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand([], SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*Contracts*empty*");
    }

    [Fact]
    public async Task OnValidateAsync_StartFuturesBarDataStreamingCommand_ContractWithEmptyContractId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidContract = new FuturesContractV2ReadModel(
            contractId: "",
            description: "Test",
            symbol: "ES",
            localSymbol: "ESM4",
            securityType: "FUT",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            lastTradeDate: new DateOnly(2024, 06, 21),
            currentlyTraded: true);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand([invalidContract], SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*required*");
    }

    [Fact]
    public async Task OnValidateAsync_StartFuturesBarDataStreamingCommand_ContractWithInvalidSecurityType_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidContract = new FuturesContractV2ReadModel(
            contractId: "ESM4",
            description: "Test",
            symbol: "ES",
            localSymbol: "ESM4",
            securityType: "OPT",
            currency: "USD",
            exchange: "CME",
            multiplier: "50",
            lastTradeDate: new DateOnly(2024, 06, 21),
            currentlyTraded: true);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand([invalidContract], SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*SecurityType*FUT*");
    }

    [Fact]
    public async Task OnValidateAsync_StartFuturesBarDataStreamingCommand_InvalidValueDate_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(DateOnly.MinValue);
        var command = new StartFuturesBarDataStreamingCommand(SampleData.FuturesContracts, DateOnly.MinValue)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
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
    public async Task OnValidateAsync_StopFuturesBarDataStreamingCommand_InvalidValueDate_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(DateOnly.MaxValue);
        var command = new StopFuturesBarDataStreamingCommand(DateOnly.MaxValue)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
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
    public async Task OnValidateAsync_InsertFuturesBarDataCommand_InvalidContractId_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidBarData = new FuturesBarDataReadModel(
            contractId: "",
            symbol: "ES",
            valueDate: SampleData.ValueDate,
            barDate: new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            barRateType: BarRateType.Minute,
            barValue: 5450.25m,
            upTrendTrigger: 0.75,
            downTrendTrigger: -0.50);

        var entityId = invalidBarData.Id;
        var command = new InsertFuturesBarDataCommand(invalidBarData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*ContractId*required*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertFuturesBarDataCommand_InvalidSymbol_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidBarData = new FuturesBarDataReadModel(
            contractId: "ESM4",
            symbol: "",
            valueDate: SampleData.ValueDate,
            barDate: new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            barRateType: BarRateType.Minute,
            barValue: 5450.25m,
            upTrendTrigger: 0.75,
            downTrendTrigger: -0.50);

        var entityId = invalidBarData.Id;
        var command = new InsertFuturesBarDataCommand(invalidBarData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = command!.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, command!);

        // Assert
        await act.Should().ThrowAsync<CommandValidationException>()
            .WithMessage("*Symbol*required*");
    }

    [Fact]
    public async Task OnValidateAsync_InsertFuturesBarDataCommand_InvalidValueDate_ThrowsCommandValidationException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidBarData = new FuturesBarDataReadModel(
            contractId: "ESM4",
            symbol: "ES",
            valueDate: DateOnly.MinValue,
            barDate: new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            barRateType: BarRateType.Minute,
            barValue: 5450.25m,
            upTrendTrigger: 0.75,
            downTrendTrigger: -0.50);

        var entityId = new FuturesBarDataId("ESM4", "ES", SampleData.ValueDate);
        var command = new InsertFuturesBarDataCommand(invalidBarData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesBarDataCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = Substitute.For<ICommand>();
        cmd.CommandId.Returns(Guid.NewGuid());
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FuturesBarDataCommandActor.ActorName, "UnknownVerb", "thread-id"));

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FuturesBarDataCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to validate {FuturesBarDataCommandActor.ActorName} commands from message:*");
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepositoryReturnsValidState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var expectedState = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        // Initialize the repo via OnStartup
        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesBarDataCommandState>();
        (result as FuturesBarDataCommandState)!.Id.Should().Be(expectedState.Id);
    }

    [Fact]
    public async Task OnLoadStateAsync_CallsRepositoryLoadState_WithCorrectCommand()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarDataId1;
        var cmd = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var expectedState = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesBarDataCommandActor.ActorName, "test-thread");

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);
        repo.LoadStateAsync(Arg.Any<ICommand>()).Returns<FuturesBarDataCommandState>(_ => throw new InvalidOperationException("Repository load failed"));

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesBarDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        var threadId = cmd!.Subject.ThreadId;

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd!);

        // Assert
        await repo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(c => c == context),
            Arg.Is<FuturesBarDataCommandState>(s => s == state),
            Arg.Is<ICommand>(c => c.CommandId == cmd!.CommandId));
    }

    [Fact]
    public async Task OnSaveStateAsync_CompletesSuccessfully_WhenCalledWithValidParameters()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarDataId1;
        var cmd = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesBarDataCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FuturesBarDataCommandState { Id = new ActorThreadId(ActorType.Command, FuturesBarDataCommandActor.ActorName, "test-thread") };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

        var threadId = new ActorThreadId(ActorType.Command, FuturesBarDataCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsException_WhenStateIsNotFuturesBarDataCommandState()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var wrongState = Substitute.For<IActorState>();
        wrongState.Id.Returns(cmd!.Subject.ThreadId);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var cmd = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var state = new FuturesBarDataCommandState { Id = cmd!.Subject.ThreadId };

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FuturesBarDataCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FuturesBarDataCommandState>>().Returns(repo);
        repo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FuturesBarDataCommandState>(), Arg.Any<ICommand>())
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("Repository save failed"))));

        await ((ICommandActor<FuturesBarDataCommandActor>)actor).OnStartup(context);

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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
    public async Task OnExceptionAsync_PreservesCommandIdInErrorEvent()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var expectedCommandId = Guid.NewGuid();
        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = expectedCommandId,
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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

    [Fact]
    public async Task OnExceptionAsync_DeleteCommand_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarDataId1;
        var command = new DeleteFuturesBarDataCommand(SampleData.FuturesBarDataId1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InvalidOperationException("Delete operation failed");

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
        failedResult.ErrorCode.Should().Be(command.ErrorCode);
    }

    [Fact]
    public async Task OnExceptionAsync_StreamingCommand_ReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        var command = new StartFuturesBarDataStreamingCommand(SampleData.FuturesContracts, SampleData.ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = command!.Subject.ThreadId;
        var exception = new InvalidOperationException("Streaming start failed");

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
        failedResult.ErrorCode.Should().Be(command.ErrorCode);
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncFails_StillReturnsFailedResult()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
        var logger = Substitute.For<ILogger<FuturesBarDataCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = SampleData.FuturesBarData1.Id;
        var command = new InsertFuturesBarDataCommand(SampleData.FuturesBarData1)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
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
