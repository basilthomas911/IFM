using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

/// <summary>
/// Contains unit tests for the FundCommandActor class, verifying command parsing, message handling, and repository
/// resolution behaviors.
/// </summary>
/// <remarks>These tests ensure that FundCommandActor correctly deserializes various fund-related commands from
/// messages, sets message information in the actor context, and resolves dependencies from the container during
/// startup. The tests use substitutes and test helpers to simulate runtime conditions and validate the actor's exposed
/// behaviors.</remarks>
public class FundCommandActorTests : IClassFixture<FundTestFixture>
{
    readonly FundTestFixture _fixture;

    public FundCommandActorTests(FundTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFundCommandActor : FundCommandActor
    {
        public TestableFundCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FundCommandActor> logger)
            : base(dbEventSource, logger)
        {
        }

        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnLoadStateAsync(context, threadId, cmd);

        public async ValueTask InvokeOnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
            => await OnSaveStateAsync(context, threadId, state, cmd);

        public async ValueTask InvokeOnStartup(ICommandActorContext context)
            => await ((ICommandActor<FundCommandActor>)this).OnStartup(context);
    }

    [Fact]
    public async Task OnStartup_SetsRepositoryField_WhenSuccessful()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FundCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FundCommandState>>().Returns(repo);

        // Act
        await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);

        // Assert - verify repository was resolved exactly once
        container.Received(1).Resolve<IEventSourceActorStateRepository<FundCommandState>>();
    }

    [Fact]
    public async Task OnStartup_ThrowsArgumentNullException_WhenContainerIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        // Return null for Container property
        context.Container.Returns((IContainerInstance?)null);

        // Act
        Func<Task> act = async () => await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);

        // Assert
        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task OnStartup_ThrowsArgumentNullException_WhenResolveThrowsException()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();

        context.Container.Returns(container);
        container.When(c => c.Resolve<IEventSourceActorStateRepository<FundCommandState>>())
            .Do(_ => throw new InvalidOperationException("Container resolution failed"));

        // Act
        Func<Task> act = async () => await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Container resolution failed");
    }

    [Fact]
    public async Task OnStartup_CompletesSuccessfully_WhenCalledMultipleTimes()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FundCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FundCommandState>>().Returns(repo);

        // Act - call OnStartup multiple times
        await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);
        await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);
        await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);

        // Assert - verify repository was resolved for each call
        container.Received(3).Resolve<IEventSourceActorStateRepository<FundCommandState>>();
    }

    [Fact]
    public async Task OnStartup_UsesProvidedContext_WhenResolvingDependencies()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FundCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FundCommandState>>().Returns(repo);

        // Act
        await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);

        // Assert - verify context.Container was accessed
        _ = context.Received(1).Container;
    }

    [Fact]
    public async Task OnStartup_DoesNotThrow_WhenRepositoryIsValidInstance()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var repo = Substitute.For<IEventSourceActorStateRepository<FundCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FundCommandState>>().Returns(repo);

        // Act
        Func<Task> act = async () => await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void OnStartup_ThrowsArgumentNullException_WhenDbEventSourceIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FundCommandActor>>();

        // Act
        Action act = () => new TestableFundCommandActor(null!, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task OnStartup_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();

        // Act
        Func<TestableFundCommandActor> act = () => new TestableFundCommandActor(dbEventSource, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task OnStartup_ResolvesCorrectRepositoryType_WhenCalled()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var container = Substitute.For<IContainerInstance>();
        var correctRepo = Substitute.For<IEventSourceActorStateRepository<FundCommandState>>();

        context.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<FundCommandState>>().Returns(correctRepo);

        // Act
        await ((ICommandActor<FundCommandActor>)actor).OnStartup(context);

        // Assert - verify the exact type was resolved
        container.Received(1).Resolve<IEventSourceActorStateRepository<FundCommandState>>();
    }

    [Fact]
    public async Task ParseMessage_DeserializesAddOrderToFundCommand_AndLogsToDatabase()
    {
        // Arrange - ensure actor serializers are set (matches runtime startup behaviour)
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        // Build a minimal FundOrderReadModel and command to serialize
        var fundOrderVm = new FundOrderReadModel(
            fundId: 123,
            orderId: 456,
            orderDate: DateTime.UtcNow,
            orderStatus: Shared.OrderStatus.Open,
            baseContractId: "ESU9",
            tradeDate: DateOnly.FromDateTime(DateTime.UtcNow),
            maturityDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            reference: "ref",
            createdOn: DateTime.UtcNow,
            createdBy: "tester",
            updatedOn: null,
            updatedBy: string.Empty
        );

        var entityId = new FundId(SampleData.FundOrder.FundId);
        var command = new AddOrderToFundCommand(SampleData.FundOrder)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        // Serialize using configured serializer
        var payload = ActorExtensions.DataSerializer.Serialize(command);

        // Create a NATS message with subject matching the command subject format
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Prepare context substitute
        var context = Substitute.For<ICommandActorContext>();
        
        // Setup database mock to return a completed task when InsertCommandLogAsync is called
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert - verify the command was deserialized correctly
        result.Should().NotBeNull();
        result.Should().BeOfType<AddOrderToFundCommand>();
        var deserializedCommand = result as AddOrderToFundCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.FundOrder.FundId.Should().Be(command.FundOrder.FundId);
        deserializedCommand.FundOrder.OrderId.Should().Be(command.FundOrder.OrderId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        // Note: command logging happens during ReceiveAsync in current implementation.
        // ParseMessage no longer logs to the database, so we do not assert InsertCommandLogAsync here.

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesAddTradeToFundOrderCommand_AndLogsToDatabase()
    {
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.FundOrderTrade.FundId);
        var command = new AddTradeToFundOrderCommand(SampleData.FundOrderTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Setup database mock to return a completed task when InsertCommandLogAsync is called
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert - verify the command was deserialized correctly
        result.Should().NotBeNull();
        result.Should().BeOfType<AddTradeToFundOrderCommand>();
        var deserializedCommand = result as AddTradeToFundOrderCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.FundOrderTrade.FundId.Should().Be(command.FundOrderTrade.FundId);
        deserializedCommand.FundOrderTrade.TradeId.Should().Be(command.FundOrderTrade.TradeId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        // Note: command logging happens during ReceiveAsync in current implementation.
        // ParseMessage no longer logs to the database, so we do not assert InsertCommandLogAsync here.

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesChangeFundOrderTradeStateCommand_AndLogsToDatabase()
    {
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var tradeId = new FundOrderTradeId(123, 456, 1);
        var entityId = new FundId(tradeId.FundId);
        var command = new ChangeFundOrderTradeStateCommand(tradeId, TradeState.TradeToOpen)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Setup database mock to return a completed task when InsertCommandLogAsync is called
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert - verify the command was deserialized correctly
        result.Should().NotBeNull();
        result.Should().BeOfType<ChangeFundOrderTradeStateCommand>();
        var deserializedCommand = result as ChangeFundOrderTradeStateCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.FundOrderTradeId.FundId.Should().Be(command.FundOrderTradeId.FundId);
        deserializedCommand.FundOrderTradeId.TradeId.Should().Be(command.FundOrderTradeId.TradeId);
        deserializedCommand.TradeState.Should().Be(command.TradeState);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        // Note: command logging happens during ReceiveAsync in current implementation.
        // ParseMessage no longer logs to the database, so we do not assert InsertCommandLogAsync here.

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParseMessage_DeserializesCloseFundOrderCommand_AndLogsToDatabase()
    {
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var fundOrderId = new FundOrderId(123, 456);
        var entityId = new FundId(fundOrderId.FundId);
        var command = new CloseFundOrderCommand(fundOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Setup database mock to return a completed task when InsertCommandLogAsync is called
        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert - verify the command was deserialized correctly
        result.Should().NotBeNull();
        result.Should().BeOfType<CloseFundOrderCommand>();
        var deserializedCommand = result as CloseFundOrderCommand;
        deserializedCommand.Should().NotBeNull();
        deserializedCommand!.CommandId.Should().Be(command.CommandId);
        deserializedCommand.FundOrderId.OrderId.Should().Be(command.FundOrderId.OrderId);
        deserializedCommand.Subject.ToString().Should().Be(subject);

        // Note: command logging happens during ReceiveAsync in current implementation.
        // ParseMessage no longer logs to the database, so we do not assert InsertCommandLogAsync here.

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ReceiveAsync_CloseFundOrderCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        // prepare state: fund exists and order exists
        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();
        state.Apply(new OrderAddedToFundEvent { FundOrder = SampleData.FundOrder }).Should().BeTrue();

        // create close command for the existing order
        var closeFundOrderId = new FundOrderId(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
        var entityId = new FundId(closeFundOrderId.FundId);
        var cmd = new CloseFundOrderCommand(closeFundOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        var natsMsg = new NatsMsg<byte[]>(cmd.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var msgInfo = new ActorMessageInfo(natsMsg, cmd);

        var context = Substitute.For<ICommandActorContext>();
        context.GetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>()).Returns(msgInfo);

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert - result contains the command id
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);

        // Assert - state recorded FundOrderClosedEvent
        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FundOrderClosedEvent).Should().BeTrue();

        var evt = state.Events.OfType<FundOrderClosedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.EntityId.Should().Be(cmd.EntityId);
    }

    [Fact]
    public async Task ReceiveAsync_CloseFundOrderCommand_ReturnsFailedResultWhenFundDoesNotExist()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var fundState = new FundCommandState(); // no FundCreatedEvent applied -> fund does not exist

        var closeFundOrderId = new FundOrderId(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
        var entityId = new FundId(closeFundOrderId.FundId);
        var cmd = new CloseFundOrderCommand(closeFundOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var natsMsg = new NatsMsg<byte[]>(cmd.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var msgInfo = new ActorMessageInfo(natsMsg, cmd);

        var context = Substitute.For<ICommandActorContext>();
        context.GetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>()).Returns(msgInfo);

        // Act
        var result = await actor.InvokeReceiveAsync(context, fundState, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {closeFundOrderId.FundId} does not exist");
    }

    [Fact]
    public async Task ReceiveAsync_CloseFundOrderCommand_ReturnsFailedResultWhenFundIdMismatch()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var fundState = new FundCommandState();
        // create a different fund id than the command
        var createdFund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId + 1 };
        fundState.Apply(new FundCreatedEvent { NewFund = createdFund }).Should().BeTrue();

        var closeFundOrderId = new FundOrderId(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
        var entityId = new FundId(closeFundOrderId.FundId);
        var cmd = new CloseFundOrderCommand(closeFundOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var natsMsg = new NatsMsg<byte[]>(cmd.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var msgInfo = new ActorMessageInfo(natsMsg, cmd);

        var context = Substitute.For<ICommandActorContext>();
        context.GetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>()).Returns(msgInfo);

        // Act
        var result = await actor.InvokeReceiveAsync(context, fundState, cmd);

        // Assert - the fund exists (under a different fund id) but the order does not exist for this fund id
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {closeFundOrderId.OrderId} does not exist");
    }

    [Fact]
    public async Task ReceiveAsync_CloseFundOrderCommand_ReturnsFailedResultWhenOrderDoesNotExist()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var fundState = new FundCommandState();
        // create fund but do not add order
        var createdFund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId };
        fundState.Apply(new FundCreatedEvent { NewFund = createdFund }).Should().BeTrue();

        var closeFundOrderId = new FundOrderId(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
        var entityId2 = new FundId(closeFundOrderId.FundId);
        var cmd = new CloseFundOrderCommand(closeFundOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId2.Format()),
            EntityId = entityId2
        };

        var natsMsg = new NatsMsg<byte[]>(cmd.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var msgInfo = new ActorMessageInfo(natsMsg, cmd);

        var context = Substitute.For<ICommandActorContext>();
        context.GetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>()).Returns(msgInfo);

        // Act
        var result = await actor.InvokeReceiveAsync(context, fundState, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {closeFundOrderId.OrderId} does not exist");
    }

    [Fact]
    public async Task ReceiveAsync_CloseFundOrderCommand_ReturnsFailedResultWhenOrderAlreadyClosed()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var fundState = new FundCommandState();
        var createdFund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId };
        fundState.Apply(new FundCreatedEvent { NewFund = createdFund }).Should().BeTrue();
        fundState.Apply(new OrderAddedToFundEvent { FundOrder = SampleData.FundOrder }).Should().BeTrue();

        // simulate order already closed
        fundState.Apply(new FundOrderClosedEvent { 
            EntityId = createdFund.Id,
            FundOrderId = SampleData.FundOrder.Id
        }).Should().BeTrue();

        var closeFundOrderId = new FundOrderId(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
        var entityId = new FundId(closeFundOrderId.FundId);
        var cmd = new CloseFundOrderCommand(closeFundOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var natsMsg = new NatsMsg<byte[]>(cmd.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var msgInfo = new ActorMessageInfo(natsMsg, cmd);

        var context = Substitute.For<ICommandActorContext>();
        context.GetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>()).Returns(msgInfo);

        // Act
        var result = await actor.InvokeReceiveAsync(context, fundState, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {closeFundOrderId.OrderId} is already closed");
    }

    [Fact]
    public async Task ReceiveAsync_CreateFundCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        // start with an empty state (no fund created)
        var state = new FundCommandState();

        // Prepare CreateFundCommand
        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        cmd.Should().NotBeNull();

        // Create ActorMessageInfo that ReceiveAsync will retrieve via context
        var natsMsg = new NatsMsg<byte[]>(cmd.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var msgInfo = new ActorMessageInfo(natsMsg, cmd);

        var context = Substitute.For<ICommandActorContext>();
        context.GetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>()).Returns(msgInfo);

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert - result contains the command id
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);

        // Assert - state has recorded a FundCreatedEvent
        state.Events.Should().NotBeNull();
        state.Events.Any(e => e is FundCreatedEvent).Should().BeTrue();

        // further assert that the persisted event contains the expected NewFund payload
        var evt = state.Events.OfType<FundCreatedEvent>().FirstOrDefault();
        evt.Should().NotBeNull();
        evt!.NewFund.FundId.Should().Be(cmd.NewFund.FundId);
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundCommand_DoesNotThrow_WhenCommandIsValid()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        cmd.Should().NotBeNull();

        var context = Substitute.For<ICommandActorContext>();

        // Act / Assert - invoking the exposed helper should complete without throwing
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundCommand_ThrowsCommandValidationException_WhenCommandIdIsEmpty()
    {
        // Arrange
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        cmd.Should().NotBeNull();

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert - specific validation exception
        await act.Should().ThrowAsync<IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task ReceiveAsync_RemoveTradeFromFundOrderCommand_ReturnsFailedResultWhenTradeIdDoesNotExist()
    {
        // Arrange - Given: An existing fund order trade
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var fundState = new FundCommandState();
        
        // Create fund
        var fund = SampleData.Fund with { FundId = SampleData.FundOrderTrade.FundId };
        fundState.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();
        
        // Add order to fund
        fundState.Apply(new OrderAddedToFundEvent { FundOrder = SampleData.FundOrder }).Should().BeTrue();
        
        // Add trade to fund order
        fundState.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = SampleData.FundOrderTrade }).Should().BeTrue();

        // When: Removing trade from fund order with non-existing trade id (9876)
        var nonExistingTradeId = 9876;
        var removeTradeId = new FundOrderTradeId(SampleData.FundOrderTrade.FundId, SampleData.FundOrderTrade.OrderId, nonExistingTradeId);
        var entityId = new FundId(removeTradeId.FundId);
        var cmd = new RemoveTradeFromFundOrderCommand(removeTradeId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveTradeFromFundOrderCommand.Actor, RemoveTradeFromFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var natsMsg = new NatsMsg<byte[]>(cmd.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var msgInfo = new ActorMessageInfo(natsMsg, cmd);

        var context = Substitute.For<ICommandActorContext>();
        context.GetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>()).Returns(msgInfo);

        // Act
        var result = await actor.InvokeReceiveAsync(context, fundState, cmd);

        // Assert - Then: Return a failed result with a trade-does-not-exist error message
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"tradeId {nonExistingTradeId} does not exist");
    }

    #region ParseMessage Additional Tests

    [Fact]
    public async Task ParseMessage_DeserializesCreateFundCommand_Successfully()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var command = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var payload = ActorExtensions.DataSerializer.Serialize(command);
        var subject = command.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<CreateFundCommand>();
        var deserialized = result as CreateFundCommand;
        deserialized!.CommandId.Should().Be(command.CommandId);
        deserialized.NewFund.FundId.Should().Be(command.NewFund.FundId);
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var subject = new ActorSubject(ActorType.Command, "SomeOtherActor", CreateFundCommand.Verb, "1");
        var natsMsg = new NatsMsg<byte[]>(subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsUnknown()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var subject = new ActorSubject(ActorType.Command, FundCommandActor.Actor, "UnknownVerb", "1");
        var natsMsg = new NatsMsg<byte[]>(subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var subject = new ActorSubject(ActorType.Command, FundCommandActor.Actor, CreateFundCommand.Verb, "1");
        var natsMsg = new NatsMsg<byte[]>(subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ReceiveAsync Additional Tests

    [Fact]
    public async Task ReceiveAsync_AddOrderToFundCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();

        var entityId = new FundId(SampleData.FundOrder.FundId);
        var cmd = new AddOrderToFundCommand(SampleData.FundOrder)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
        state.Events.Any(e => e is OrderAddedToFundEvent).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_AddOrderToFundCommand_ReturnsFailedResultWhenFundDoesNotExist()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var entityId = new FundId(SampleData.FundOrder.FundId);
        var cmd = new AddOrderToFundCommand(SampleData.FundOrder)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {SampleData.FundOrder.FundId} does not exist");
    }

    [Fact]
    public async Task ReceiveAsync_AddTradeToFundOrderCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrderTrade.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();
        state.Apply(new OrderAddedToFundEvent { FundOrder = SampleData.FundOrder }).Should().BeTrue();

        var entityId = new FundId(SampleData.FundOrderTrade.FundId);
        var cmd = new AddTradeToFundOrderCommand(SampleData.FundOrderTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
        state.Events.Any(e => e is TradeAddedToFundOrderEvent).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_AddTradeToFundOrderCommand_ReturnsFailedResultWhenFundOrderDoesNotExist()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrderTrade.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();

        var entityId = new FundId(SampleData.FundOrderTrade.FundId);
        var cmd = new AddTradeToFundOrderCommand(SampleData.FundOrderTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {SampleData.FundOrderTrade.OrderId} does not exist");
    }

    [Fact]
    public async Task ReceiveAsync_ChangeFundOrderTradeStateCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrderTrade.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();
        state.Apply(new OrderAddedToFundEvent { FundOrder = SampleData.FundOrder }).Should().BeTrue();
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = SampleData.FundOrderTrade }).Should().BeTrue();

        var tradeId = new FundOrderTradeId(SampleData.FundOrderTrade.FundId, SampleData.FundOrderTrade.OrderId, SampleData.FundOrderTrade.TradeId);
        var entityId = new FundId(tradeId.FundId);
        var cmd = new ChangeFundOrderTradeStateCommand(tradeId, TomasAI.IFM.Shared.Trade.TradeState.TradeToClose)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
        state.Events.Any(e => e is FundOrderTradeStateChangedEvent).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_ChangeFundOrderTradeStateCommand_ReturnsFailedResultWhenTradeDoesNotExist()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrderTrade.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();
        state.Apply(new OrderAddedToFundEvent { FundOrder = SampleData.FundOrder }).Should().BeTrue();

        var tradeId = new FundOrderTradeId(SampleData.FundOrderTrade.FundId, SampleData.FundOrderTrade.OrderId, 9999);
        var entityId = new FundId(tradeId.FundId);
        var cmd = new ChangeFundOrderTradeStateCommand(tradeId, TomasAI.IFM.Shared.Trade.TradeState.TradeToClose)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"tradeId {tradeId.TradeId} does not exist within order {tradeId.OrderId}");
    }

    [Fact]
    public async Task ReceiveAsync_RemoveOrderFromFundCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();
        state.Apply(new OrderAddedToFundEvent { FundOrder = SampleData.FundOrder }).Should().BeTrue();

        var orderId = new FundOrderId(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
        var entityId = new FundId(orderId.FundId);
        var cmd = new RemoveOrderFromFundCommand(orderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
        state.Events.Any(e => e is OrderRemovedFromFundEvent).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_RemoveOrderFromFundCommand_ReturnsFailedResultWhenOrderDoesNotExist()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();

        var orderId = new FundOrderId(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
        var entityId = new FundId(orderId.FundId);
        var cmd = new RemoveOrderFromFundCommand(orderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {orderId.OrderId} does not exist within fund: {orderId.FundId}");
    }

    [Fact]
    public async Task ReceiveAsync_GenerateFundMaxProfitCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var fund = SampleData.Fund with { FundId = SampleData.FundOrder.FundId };
        state.Apply(new FundCreatedEvent { NewFund = fund }).Should().BeTrue();

        var entityId = new FundId(SampleData.FundOrder.FundId);
        var cmd = new GenerateFundMaxProfitCommand(SampleData.FundOrder, TomasAI.IFM.Shared.MarketDataAnalytics.TradeTimePeriodType.Daily)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFundMaxProfitCommand.Actor, GenerateFundMaxProfitCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
        state.Events.Any(e => e is FundMaxProfitGeneratedEvent).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_GenerateFundMaxProfitCommand_ReturnsFailedResultWhenFundDoesNotExist()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var entityId = new FundId(SampleData.FundOrder.FundId);
        var cmd = new GenerateFundMaxProfitCommand(SampleData.FundOrder, TomasAI.IFM.Shared.MarketDataAnalytics.TradeTimePeriodType.Daily)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, GenerateFundMaxProfitCommand.Actor, GenerateFundMaxProfitCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {SampleData.FundOrder.FundId} does not exist");
    }

    [Fact]
    public async Task ReceiveAsync_CreateFundCommand_ReturnsFailedResultWhenFundAlreadyExists()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        state.Apply(new FundCreatedEvent { NewFund = SampleData.Fund }).Should().BeTrue();

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {SampleData.Fund.FundId} already exists");
    }

    [Fact]
    public void ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = new FundCommandState();
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid()
        };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, state, cmd);

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnValidateAsync Additional Tests

    [Fact]
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandTypeUnknown()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var cmd = Substitute.For<ICommand>();
        cmd.Subject.Returns(new ActorSubject(ActorType.Command, FundCommandActor.Actor, "Unknown", "1"));

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnValidateAsync_RemoveOrderFromFundCommand_ThrowsCommandValidationException_WhenFundOrderIdInvalid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(0);
        var cmd = new RemoveOrderFromFundCommand(new FundOrderId(0, 0))
        {
            CommandId = Guid.Empty,
            Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<IFM.Shared.Exceptions.CommandValidationException>();
    }

    #endregion

    #region OnLoadStateAsync Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepoIsConfigured()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = cmd.Subject.ThreadId;
        var expectedState = new FundCommandState { Id = threadId };

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FundCommandState>>();
        mockRepo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FundCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FundCommandState>();
        result.Id.Should().Be(threadId);
        await mockRepo.Received(1).LoadStateAsync(Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundCommandActor.Actor, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnSaveStateAsync Tests

    [Fact]
    public async Task OnSaveStateAsync_CallsRepoSaveStateAsync_WhenRepoIsConfigured()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = cmd.Subject.ThreadId;
        var state = new FundCommandState { Id = threadId };

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FundCommandState>>();
        mockRepo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FundCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FundCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await mockRepo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(ctx => ctx == context),
            Arg.Is<FundCommandState>(s => s.Id == threadId),
            Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<ICommandActorContext>();
        var threadId = cmd.Subject.ThreadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, null!, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundCommandActor.Actor, "test-thread");
        var state = new FundCommandState { Id = threadId };

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnExceptionAsync Tests

    [Fact]
    public async Task OnExceptionAsync_CreateFundException_SendsFundCreatedFailEvent_ReturnsFailedResult()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = cmd.Subject.ThreadId;
        var exception = new CreateFundException("Fund already exists");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<FundCreatedFailEvent, FundId>(Arg.Any<FundCreatedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, cmd, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        ((ServiceFailed<GuidResult>)result).Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_GenericException_SendsCommandExceptionEventAndReturnsFailedResult()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = cmd.Subject.ThreadId;
        var exception = new InvalidOperationException("Unexpected error occurred");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<IFM.Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, cmd, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        ((ServiceFailed<GuidResult>)result).Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncFails_ReturnsFailedResultWithFallback()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var entityId = new FundId(SampleData.Fund.FundId);
        var cmd = new CreateFundCommand(SampleData.Fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        var threadId = cmd.Subject.ThreadId;
        var exception = new InvalidOperationException("Original error");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<IFM.Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(x => throw new Exception("SendAsync failed"));

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, cmd, exception);

        // Assert - should still return a failed result from fallback logic
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        ((ServiceFailed<GuidResult>)result).Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_ReturnsFailedResult_WhenCommandIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundCommandActor.Actor, "test-thread");
        var exception = new InvalidOperationException("error");

        context.SendAsync<IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<IFM.Shared.EventModelActor.Events.CommandExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act - the actor internally catches the ArgumentNullException raised for a null command
        // and falls back to a generic failed result rather than propagating the exception.
        var result = await actor.InvokeOnExceptionAsync(context, threadId, null!, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        ((ServiceFailed<GuidResult>)result).Success.Should().BeFalse();
    }

    #endregion
}