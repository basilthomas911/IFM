using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Command;
using TomasAI.IFM.Domain.Fund.Transaction.Command.State;

namespace TomasAI.IFM.Domain.Fund.BDDTests.Transaction;

/// <summary>
/// BDD-style tests for the fund transaction command handlers (<see cref="CreateFundTransaction"/>,
/// <see cref="CreateFundTransactions"/>, <see cref="ProcessEndOfDayFundTransaction"/>), exercising the
/// Execute extension methods against <see cref="FundTransactionCommandState"/>, covering both happy
/// paths and edge cases for every registered fund transaction command.
/// </summary>
public class FundTransactionCommandTests
{
    static FundTransactionCommandState CreateState(decimal balance = 0m)
    {
        var fundDb = Substitute.For<IFundDbContext>();
        fundDb.GetFundBalanceAsync(Arg.Any<int>()).Returns(Task.FromResult(balance));
        return new FundTransactionCommandState(fundDb);
    }

    #region CreateFundTransactionCommand - happy paths

    [Theory]
    [InlineData(FundTransactionType.OpeningTrade)]
    [InlineData(FundTransactionType.OpeningTradeAdjustment)]
    [InlineData(FundTransactionType.TradeCommission)]
    [InlineData(FundTransactionType.TradeCommissionAdjustment)]
    [InlineData(FundTransactionType.UnrealizedTradePnl)]
    [InlineData(FundTransactionType.UnrealizedTradePnlAdjustment)]
    [InlineData(FundTransactionType.RealizedTradePnl)]
    [InlineData(FundTransactionType.RealizedTradePnlAdjustment)]
    [InlineData(FundTransactionType.CashDeposit)]
    [InlineData(FundTransactionType.CashDepositAdjustment)]
    [InlineData(FundTransactionType.CashWithdrawalAdjustment)]
    public void CreateFundTransactionCommand_GivenSupportedTransactionType_WhenExecuted_ThenReturnsFundTransactionEvent(FundTransactionType transactionType)
    {
        // Arrange - Given a state with a known balance and a create command of the given transaction type
        var state = CreateState(balance: 1000m);
        var fundTransaction = SampleData.FundTransaction with { TransactionType = transactionType };
        var command = new CreateFundTransactionCommand(fundTransaction);

        // Act - When executing CreateFundTransactionCommand
        var result = command.Execute(state);

        // Assert - Then a FundTransactionEvent is recorded with the expected balance update
        result.Success.Should().BeTrue();
        state.Events.Should().ContainSingle(e => e is FundTransactionEvent);
        var fundTxEvent = state.Events.OfType<FundTransactionEvent>().First();
        fundTxEvent.FundTransaction.FundId.Should().Be(fundTransaction.FundId);
        fundTxEvent.FundTransaction.TransactionType.Should().Be(transactionType);
        fundTxEvent.FundTransaction.Balance.Should().Be(1000m + fundTransaction.Amount);
    }

    [Fact]
    public void CreateFundTransactionCommand_GivenCashWithdrawal_WhenExecuted_ThenBalanceIsReducedByAmount()
    {
        // Arrange - Given a state with a known balance and a cash withdrawal command
        var state = CreateState(balance: 1000m);
        var fundTransaction = SampleData.FundTransaction with { TransactionType = FundTransactionType.CashWithdrawal, Amount = 200m };
        var command = new CreateFundTransactionCommand(fundTransaction);

        // Act - When executing CreateFundTransactionCommand
        var result = command.Execute(state);

        // Assert - Then the balance is reduced by the withdrawal amount
        result.Success.Should().BeTrue();
        var fundTxEvent = state.Events.OfType<FundTransactionEvent>().First();
        fundTxEvent.FundTransaction.Balance.Should().Be(800m);
    }

    #endregion

    #region CreateFundTransactionCommand - edge cases

    [Fact]
    public void CreateFundTransactionCommand_GivenUnsupportedTransactionType_WhenExecuted_ThenReturnsFailedResultWithDoesNotExistMessage()
    {
        // Arrange - Given an unsupported (Unknown) transaction type
        var state = CreateState();
        var fundTransaction = SampleData.FundTransaction with { TransactionType = FundTransactionType.Unknown };
        var command = new CreateFundTransactionCommand(fundTransaction);

        // Act - When executing CreateFundTransactionCommand
        var result = command.Execute(state);

        // Assert - Then return a failed result because no event can be built for the transaction type, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public void CreateFundTransactionCommand_GivenNullFundTransaction_WhenExecuted_ThenDoesNotAddEvent()
    {
        // Arrange - Given a command whose FundTransaction payload is null
        var state = CreateState();
        var command = new CreateFundTransactionCommand
        {
            EntityId = new FundTransactionEntityId(SampleData.FundTransaction.FundId, SampleData.FundTransaction.OrderId),
            FundTransaction = null!
        };

        // Act - When executing CreateFundTransactionCommand
        var result = command.Execute(state);

        // Assert - Then no fund transaction event is recorded and the state update reflects the null event
        result.Success.Should().BeFalse();
        state.Events.Should().BeEmpty();
    }

    #endregion

    #region CreateFundTransactionsCommand - happy paths

    [Fact]
    public void CreateFundTransactionsCommand_GivenBatchOfTransactions_WhenExecuted_ThenReturnsFundTransactionsEventWithAccumulatedBalances()
    {
        // Arrange - Given a state with a starting balance and a batch of transactions
        var state = CreateState(balance: 1000m);
        var fundTransactions = new[]
        {
            SampleData.FundTransaction with { TransactionType = FundTransactionType.OpeningTrade, Amount = 100m },
            SampleData.FundTransaction with { TransactionType = FundTransactionType.TradeCommission, Amount = 50m },
        };
        var command = new CreateFundTransactionsCommand(
            new FundTransactionEntityId(fundTransactions[0].FundId, fundTransactions[0].OrderId), fundTransactions);

        // Act - When executing CreateFundTransactionsCommand
        var result = command.Execute(state);

        // Assert - Then a FundTransactionsEvent is recorded with accumulated balances
        result.Success.Should().BeTrue();
        state.Events.Should().ContainSingle(e => e is FundTransactionsEvent);
        var evt = state.Events.OfType<FundTransactionsEvent>().First();
        evt.FundTransactions.Should().HaveCount(2);
        evt.FundTransactions![0].Balance.Should().Be(1100m);
        evt.FundTransactions![1].Balance.Should().Be(1150m);
    }

    [Fact]
    public void CreateFundTransactionsCommand_GivenBatchWithCashWithdrawal_WhenExecuted_ThenBalanceIsReducedForWithdrawal()
    {
        // Arrange - Given a batch containing a cash withdrawal transaction
        var state = CreateState(balance: 500m);
        var fundTransactions = new[]
        {
            SampleData.FundTransaction with { TransactionType = FundTransactionType.CashWithdrawal, Amount = 100m },
        };
        var command = new CreateFundTransactionsCommand(
            new FundTransactionEntityId(fundTransactions[0].FundId, fundTransactions[0].OrderId), fundTransactions);

        // Act - When executing CreateFundTransactionsCommand
        var result = command.Execute(state);

        // Assert - Then the balance is reduced by the withdrawal amount
        result.Success.Should().BeTrue();
        var evt = state.Events.OfType<FundTransactionsEvent>().First();
        evt.FundTransactions![0].Balance.Should().Be(400m);
    }

    #endregion

    #region CreateFundTransactionsCommand - edge cases

    [Fact]
    public void CreateFundTransactionsCommand_GivenUnsupportedTransactionType_WhenExecuted_ThenReturnsFailedResultWithUnsupportedTypeMessage()
    {
        // Arrange - Given a batch containing an unsupported transaction type
        var state = CreateState();
        var fundTransactions = new[]
        {
            SampleData.FundTransaction with { TransactionType = FundTransactionType.Unknown },
        };
        var command = new CreateFundTransactionsCommand(
            new FundTransactionEntityId(fundTransactions[0].FundId, fundTransactions[0].OrderId), fundTransactions);

        // Act - When executing CreateFundTransactionsCommand
        var result = command.Execute(state);

        // Assert - Then return a failed result with an unsupported-transaction-type error message, and no exception thrown
        // (the CreateFundTransactionException raised internally while building the batch is caught and converted to a failed result)
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unsupported fund transaction type");
    }

    [Fact]
    public void CreateFundTransactionsCommand_GivenEmptyTransactionArray_WhenConstructed_ThenThrowsArgumentOutOfRangeException()
    {
        // Arrange - Given an empty array of fund transactions
        var fundTransactions = Array.Empty<FundTransactionReadModel>();

        // Act - When constructing CreateFundTransactionsCommand and executing it (accesses index 0)
        Action act = () =>
        {
            var cmd = new CreateFundTransactionsCommand(new FundTransactionEntityId(0, 0), fundTransactions);
            cmd.Execute(CreateState());
        };

        // Assert - Then throws because there is no first transaction to derive current balance from
        act.Should().Throw<IndexOutOfRangeException>();
    }

    #endregion

    #region ProcessEndOfDayFundTransactionCommand - happy paths

    [Fact]
    public void ProcessEndOfDayFundTransactionCommand_GivenExistingUnrealizedTradePnlTransaction_WhenExecuted_ThenReturnsEndOfDayFundTransactionProcessedEvent()
    {
        // Arrange - Given a state with an existing fund transaction and an unrealized trade pnl EOD command
        var state = CreateState(balance: 1000m);
        var existingTransaction = SampleData.FundTransaction with { TransactionType = FundTransactionType.OpeningTrade };
        new CreateFundTransactionCommand(existingTransaction).Execute(state);

        var eodTransaction = existingTransaction with { TransactionType = FundTransactionType.UnrealizedTradePnl, Amount = 25m };
        var command = new ProcessEndOfDayFundTransactionCommand(eodTransaction);

        // Act - When executing ProcessEndOfDayFundTransactionCommand
        var result = command.Execute(state);

        // Assert - Then an EndOfDayFundTransactionProcessedEvent is recorded
        result.Success.Should().BeTrue();
        state.Events.Should().Contain(e => e is EndOfDayFundTransactionProcessedEvent);
        var evt = state.Events.OfType<EndOfDayFundTransactionProcessedEvent>().First();
        evt.FundTransaction.TransactionType.Should().Be(FundTransactionType.UnrealizedTradePnl);
    }

    #endregion

    #region ProcessEndOfDayFundTransactionCommand - edge cases

    [Fact]
    public void ProcessEndOfDayFundTransactionCommand_GivenNonExistingFundTransaction_WhenExecuted_ThenReturnsFailedResultWithDoesNotExistMessage()
    {
        // Arrange - Given a state without any existing fund transaction
        var state = CreateState();
        var fundTransaction = SampleData.FundTransaction with { TransactionType = FundTransactionType.UnrealizedTradePnl };
        var command = new ProcessEndOfDayFundTransactionCommand(fundTransaction);

        // Act - When executing ProcessEndOfDayFundTransactionCommand
        var result = command.Execute(state);

        // Assert - Then return a failed result with a does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public void ProcessEndOfDayFundTransactionCommand_GivenTransactionTypeNotUnrealizedTradePnl_WhenExecuted_ThenReturnsFailedResultWithInvalidTransactionTypeMessage()
    {
        // Arrange - Given an existing fund transaction but the EOD command uses a non-UnrealizedTradePnl type
        var state = CreateState(balance: 1000m);
        var existingTransaction = SampleData.FundTransaction with { TransactionType = FundTransactionType.OpeningTrade };
        new CreateFundTransactionCommand(existingTransaction).Execute(state);

        var invalidEodTransaction = existingTransaction with { TransactionType = FundTransactionType.RealizedTradePnl };
        var command = new ProcessEndOfDayFundTransactionCommand(invalidEodTransaction);

        // Act - When executing ProcessEndOfDayFundTransactionCommand
        var result = command.Execute(state);

        // Assert - Then return a failed result with an invalid-transaction-type error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("transaction type must be UnrealizedTradePnl");
    }

    #endregion
}
