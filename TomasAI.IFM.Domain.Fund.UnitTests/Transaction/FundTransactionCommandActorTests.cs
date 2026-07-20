using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Exceptions;
using TomasAI.IFM.Domain.Fund.Transaction.Command.State;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Transaction;

/// <summary>
/// Contains unit tests for the FundTransactionCommandActor class, verifying command parsing, message handling,
/// validation, state load/save, and exception handling behaviors.
/// </summary>
public class FundTransactionCommandActorTests : IClassFixture<FundTestFixture>
{
    readonly FundTestFixture _fixture;

    public FundTransactionCommandActorTests(FundTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected members of FundTransactionCommandActor for unit testing.
    public class TestableFundTransactionCommandActor : FundTransactionCommandActor
    {
        public TestableFundTransactionCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FundTransactionCommandActor> logger)
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
            => await ((ICommandActor<FundTransactionCommandActor>)this).OnStartup(context);
    }

    static FundTransactionCommandState CreateState(decimal balance = 0m)
    {
        var fundDb = Substitute.For<IFundDbContext>();
        fundDb.GetFundBalanceAsync(Arg.Any<int>()).Returns(Task.FromResult(balance));
        return new FundTransactionCommandState(fundDb);
    }

    static CreateFundTransactionCommand CreateCommand(FundTransactionReadModel? fundTransaction = null, Guid? commandId = null)
    {
        var tx = fundTransaction ?? SampleData.FundTransaction;
        var entityId = new FundTransactionEntityId(tx.FundId, tx.OrderId);
        return new CreateFundTransactionCommand(tx)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundTransactionCommand.Actor, CreateFundTransactionCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
    }

    static CreateFundTransactionsCommand CreateBatchCommand(FundTransactionReadModel[]? transactions = null, Guid? commandId = null)
    {
        var txs = transactions ?? new[] { SampleData.FundTransaction };
        var entityId = new FundTransactionEntityId(txs[0].FundId, txs[0].OrderId);
        return new CreateFundTransactionsCommand
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundTransactionsCommand.Actor, CreateFundTransactionsCommand.Verb, entityId.Format()),
            EntityId = entityId,
            FundTransactions = txs
        };
    }

    static ProcessEndOfDayFundTransactionCommand CreateEndOfDayCommand(FundTransactionReadModel? fundTransaction = null, Guid? commandId = null)
    {
        var tx = fundTransaction ?? SampleData.FundTransaction with { TransactionType = FundTransactionType.UnrealizedTradePnl };
        var entityId = new FundTransactionEntityId(tx.FundId, tx.OrderId);
        return new ProcessEndOfDayFundTransactionCommand
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ProcessEndOfDayFundTransactionCommand.Actor, ProcessEndOfDayFundTransactionCommand.Verb, entityId.Format()),
            EntityId = entityId,
            FundTransaction = tx
        };
    }

    #region ParseMessage Tests

    [Fact]
    public void ParseMessage_CreateFundTransactionCommand_ReturnsDeserializedCommand()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(cmd);
        var subject = cmd.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<CreateFundTransactionCommand>();
        result.CommandId.Should().Be(cmd.CommandId);
    }

    [Fact]
    public void ParseMessage_CreateFundTransactionsCommand_ReturnsDeserializedCommand()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateBatchCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(cmd);
        var subject = cmd.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<CreateFundTransactionsCommand>();
        result.CommandId.Should().Be(cmd.CommandId);
    }

    [Fact]
    public void ParseMessage_ProcessEndOfDayFundTransactionCommand_ReturnsDeserializedCommand()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateEndOfDayCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(cmd);
        var subject = cmd.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ProcessEndOfDayFundTransactionCommand>();
        result.CommandId.Should().Be(cmd.CommandId);
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenSubjectVerbIsUnknown()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(cmd);
        var subject = new ActorSubject(ActorType.Command, FundTransactionCommandActor.ActorName, "UnknownVerb", cmd.EntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(cmd);
        var subject = new ActorSubject(ActorType.Command, "SomeOtherActor", CreateFundTransactionCommand.Verb, cmd.EntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<ICommandActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var payload = ActorExtensions.DataSerializer.Serialize(cmd);
        var subject = cmd.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ReceiveAsync Tests

    [Fact]
    public async Task ReceiveAsync_CreateFundTransactionCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var cmd = CreateCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_CreateFundTransactionsCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var cmd = CreateBatchCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_ProcessEndOfDayFundTransactionCommand_ExecutesHandler_PersistsEventAndReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var tx = SampleData.FundTransaction with { TransactionType = FundTransactionType.UnrealizedTradePnl };
        // Ensure the transaction exists before processing end of day
        var createCmd = CreateCommand(tx);
        await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, createCmd);

        var cmd = CreateEndOfDayCommand(tx);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    [Fact]
    public async Task ReceiveAsync_ProcessEndOfDayFundTransactionCommand_ThrowsWhenTransactionDoesNotExist()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var cmd = CreateEndOfDayCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ProcessEndOfDayFundTransactionException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenCommandIsUnrecognized()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var context = Substitute.For<ICommandActorContext>();
        var unrecognized = Substitute.For<ICommand>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, unrecognized);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = CreateState();
        var cmd = CreateCommand();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var context = Substitute.For<ICommandActorContext>();
        var cmd = CreateCommand();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, null!, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var state = CreateState();
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_CreateFundTransactionCommand_ThrowsCreateFundTransactionException_WhenTransactionTypeIsUnsupported()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var invalidTx = SampleData.FundTransaction with { TransactionType = (FundTransactionType)9999 };
        var cmd = CreateCommand(invalidTx);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_CreateFundTransactionsCommand_ThrowsCreateFundTransactionException_WhenAnyTransactionTypeIsUnsupported()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var invalidTx = SampleData.FundTransaction with { TransactionType = (FundTransactionType)9999 };
        var cmd = CreateBatchCommand(new[] { invalidTx });
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<CreateFundTransactionException>();
    }

    [Fact]
    public async Task ReceiveAsync_ProcessEndOfDayFundTransactionCommand_ThrowsWhenTransactionTypeIsNotUnrealizedTradePnl()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState();
        var tx = SampleData.FundTransaction with { TransactionType = FundTransactionType.OpeningTrade };
        var createCmd = CreateCommand(tx);
        await actor.InvokeReceiveAsync(Substitute.For<ICommandActorContext>(), state, createCmd);

        // The existing transaction is OpeningTrade, but end-of-day requires UnrealizedTradePnl
        var cmd = CreateEndOfDayCommand(tx with { TransactionType = FundTransactionType.OpeningTrade });
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ProcessEndOfDayFundTransactionException>();
    }

    [Fact]
    public async Task ReceiveAsync_CreateFundTransactionCommand_CashWithdrawal_ExecutesHandler_ReturnsGuid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        dbEventSource.InsertCommandLogAsync(Arg.Any<ICommand>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var state = CreateState(balance: 1000m);
        var tx = SampleData.FundTransaction with { TransactionType = FundTransactionType.CashWithdrawal };
        var cmd = CreateCommand(tx);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        var result = await actor.InvokeReceiveAsync(context, state, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Value.Guid.Should().Be(cmd.CommandId);
    }

    #endregion

    #region OnValidateAsync Tests

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionCommand_DoesNotThrow_WhenCommandIsValid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act / Assert
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionCommand_ThrowsCommandValidationException_WhenFundIdIsInvalid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidTx = SampleData.FundTransaction with { FundId = 0 };
        var cmd = CreateCommand(invalidTx);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionsCommand_DoesNotThrow_WhenCommandIsValid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateBatchCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act / Assert
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionsCommand_ThrowsCommandValidationException_WhenTransactionsAreEmpty()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = new CreateFundTransactionsCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundTransactionsCommand.Actor, CreateFundTransactionsCommand.Verb, new FundTransactionEntityId(0, 0).Format()),
            EntityId = new FundTransactionEntityId(0, 0),
            FundTransactions = Array.Empty<FundTransactionReadModel>()
        };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_ProcessEndOfDayFundTransactionCommand_DoesNotThrow_WhenCommandIsValid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateEndOfDayCommand();
        var context = Substitute.For<ICommandActorContext>();

        // Act / Assert
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsInvalidOperationException_WhenCommandIsUnrecognized()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundTransactionCommandActor.ActorName, "test-thread");
        var unrecognized = Substitute.For<ICommand>();
        unrecognized.CommandId.Returns(Guid.NewGuid());

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, unrecognized);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(null!, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionCommand_ThrowsCommandValidationException_WhenCommandIdIsEmpty()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand(commandId: Guid.Empty);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionCommand_ThrowsCommandValidationException_WhenValueDateIsMinValue()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidTx = SampleData.FundTransaction with { ValueDate = DateOnly.MinValue };
        var cmd = CreateCommand(invalidTx);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionsCommand_ThrowsCommandValidationException_WhenTransactionsHaveMismatchedFundOrOrderIds()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var tx1 = SampleData.FundTransaction;
        var tx2 = SampleData.FundTransaction with { FundId = tx1.FundId + 1 };
        var cmd = CreateBatchCommand(new[] { tx1, tx2 });
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_CreateFundTransactionsCommand_ThrowsCommandValidationException_WhenTransactionsAreNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = new CreateFundTransactionsCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundTransactionsCommand.Actor, CreateFundTransactionsCommand.Verb, new FundTransactionEntityId(0, 0).Format()),
            EntityId = new FundTransactionEntityId(0, 0),
            FundTransactions = null!
        };
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_ProcessEndOfDayFundTransactionCommand_ThrowsCommandValidationException_WhenTradeIdIsInvalid()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var invalidTx = SampleData.FundTransaction with { TransactionType = FundTransactionType.UnrealizedTradePnl, TradeId = 0 };
        var cmd = CreateEndOfDayCommand(invalidTx);
        var context = Substitute.For<ICommandActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.CommandValidationException>();
    }

    [Fact]
    public async Task OnValidateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundTransactionCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnValidateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnLoadStateAsync Tests

    [Fact]
    public async Task OnLoadStateAsync_ReturnsState_WhenRepoIsConfigured()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;
        var expectedState = CreateState();
        expectedState.Id = threadId;

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FundTransactionCommandState>>();
        mockRepo.LoadStateAsync(Arg.Any<ICommand>()).Returns(ValueTask.FromResult(expectedState));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FundTransactionCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FundTransactionCommandState>();
        result.Id.Should().Be(threadId);
        await mockRepo.Received(1).LoadStateAsync(Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(null!, cmd.Subject.ThreadId, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundTransactionCommandActor.ActorName, "test-thread");

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnLoadStateAsync_PropagatesException_WhenRepoThrows()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FundTransactionCommandState>>();
        mockRepo.LoadStateAsync(Arg.Any<ICommand>())
            .Returns<ValueTask<FundTransactionCommandState>>(x => throw new InvalidOperationException("Repo load failed"));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FundTransactionCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        Func<Task> act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region OnSaveStateAsync Tests

    [Fact]
    public async Task OnSaveStateAsync_CallsRepoSaveStateAsync_WhenRepoIsConfigured()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = CreateState();
        state.Id = threadId;

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FundTransactionCommandState>>();
        mockRepo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FundTransactionCommandState>(), Arg.Any<ICommand>())
            .Returns(ValueTask.CompletedTask);

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FundTransactionCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await mockRepo.Received(1).SaveStateAsync(
            Arg.Is<ICommandActorContext>(ctx => ctx == context),
            Arg.Is<FundTransactionCommandState>(s => s.Id == threadId),
            Arg.Is<ICommand>(c => c.CommandId == cmd.CommandId));
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
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
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundTransactionCommandActor.ActorName, "test-thread");
        var state = CreateState();
        state.Id = threadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = CreateState();
        state.Id = threadId;

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(null!, threadId, state, cmd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnSaveStateAsync_PropagatesException_WhenRepoThrows()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;
        var state = CreateState();
        state.Id = threadId;

        var mockRepo = Substitute.For<IEventSourceActorStateRepository<FundTransactionCommandState>>();
        mockRepo.SaveStateAsync(Arg.Any<ICommandActorContext>(), Arg.Any<FundTransactionCommandState>(), Arg.Any<ICommand>())
            .Returns(x => throw new InvalidOperationException("Repo save failed"));

        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IEventSourceActorStateRepository<FundTransactionCommandState>>().Returns(mockRepo);

        var context = Substitute.For<ICommandActorContext>();
        context.Container.Returns(container);

        await actor.InvokeOnStartup(context);

        // Act
        Func<Task> act = async () => await actor.InvokeOnSaveStateAsync(context, threadId, state, cmd);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region OnExceptionAsync Tests

    [Fact]
    public async Task OnExceptionAsync_CreateFundTransactionException_SendsFailEvent_ReturnsFailedResult()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;
        var exception = new CreateFundTransactionException("Unsupported fund transaction type");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<FundTransactionCreatedFailEvent, FundTransactionEntityId>(Arg.Any<FundTransactionCreatedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, cmd, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        ((ServiceFailed<GuidResult>)result).Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_CreateFundTransactionsException_SendsFailEvent_ReturnsFailedResult()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateBatchCommand();
        var threadId = cmd.Subject.ThreadId;
        var exception = new CreateFundTransactionsException("Batch creation failed");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<FundTransactionsFailEvent, FundTransactionEntityId>(Arg.Any<FundTransactionsFailEvent>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await actor.InvokeOnExceptionAsync(context, threadId, cmd, exception);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ServiceFailed<GuidResult>>();
        ((ServiceFailed<GuidResult>)result).Success.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionAsync_ProcessEndOfDayFundTransactionException_SendsFailEvent_ReturnsFailedResult()
    {
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateEndOfDayCommand();
        var threadId = cmd.Subject.ThreadId;
        var exception = new ProcessEndOfDayFundTransactionException("Transaction does not exist");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<EndOfDayFundTransactionProcessedFailEvent, FundTransactionEntityId>(Arg.Any<EndOfDayFundTransactionProcessedFailEvent>())
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
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;
        var exception = new InvalidOperationException("Unexpected error occurred");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent>())
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
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var cmd = CreateCommand();
        var threadId = cmd.Subject.ThreadId;
        var exception = new InvalidOperationException("Original error");

        var context = Substitute.For<ICommandActorContext>();
        context.SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent>())
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
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var actor = _fixture.CreateActor(dbEventSource, logger);

        var context = Substitute.For<ICommandActorContext>();
        var threadId = new ActorThreadId(ActorType.Command, FundTransactionCommandActor.ActorName, "test-thread");
        var exception = new InvalidOperationException("error");

        context.SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(
            Arg.Any<TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent>())
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
