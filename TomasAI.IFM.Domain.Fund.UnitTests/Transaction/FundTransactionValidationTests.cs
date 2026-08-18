using FluentAssertions;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Model;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Transaction;

public sealed class FundTransactionValidationTests
{
    [Theory]
    [InlineData(FundTransactionType.CashDeposit)]
    [InlineData(FundTransactionType.CashWithdrawal)]
    [InlineData(FundTransactionType.CashDepositAdjustment)]
    [InlineData(FundTransactionType.CashWithdrawalAdjustment)]
    public void CashTransactionModel_DoesNotRequireTradeIdentifiers(FundTransactionType transactionType)
    {
        var transaction = SampleData.FundTransaction with
        {
            TransactionType = transactionType,
            OrderId = 0,
            TradeId = 0
        };

        Action act = () => _ = new FundTransaction(transaction);

        act.Should().NotThrow();
    }
}
