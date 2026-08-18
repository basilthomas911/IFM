using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed record G2FundFixture(
    string FundName,
    string FundDescription,
    decimal InitialBalance,
    decimal TransactionAmount,
    string DepositDescription,
    string WithdrawalDescription)
{
    public static G2FundFixture Create(G2Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new G2FundFixture(
            configuration.FundFixtureName,
            "Retained non-production fund used by reversible G2 UI system tests.",
            configuration.FundFixtureInitialBalance,
            configuration.FundTransactionAmount,
            $"{configuration.RunPrefix}-CashDeposit",
            $"{configuration.RunPrefix}-CashWithdrawal");
    }

    public FundTransactionReadModel Transaction(
        FundReadModel fund,
        DateOnly valueDate,
        FundTransactionType transactionType,
        string description)
        => new(
            transactionId: 0,
            transactionDate: DateTime.UtcNow,
            transactionType: transactionType,
            fundId: fund.FundId,
            orderId: 0,
            tradeId: 0,
            tradeType: TradeType.Unknown,
            valueDate: valueDate,
            tradeStatus: TradeStatus.Open,
            description: description,
            amount: TransactionAmount,
            balance: fund.Balance);
}
