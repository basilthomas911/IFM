using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared;

public interface IFundTransaction
{
    FundTransactionId TransactionId { get; }
    DateTime TransactionDate { get; }
    FundTransactionType TransactionType { get; }
    int FundId { get; }
    int OrderId { get; }
    int TradeId { get; }
    TradeType TradeType { get; }
    DateOnly ValueDate { get; }
    TradeStatus TradeStatus { get; }
    string Description { get; }
    decimal Amount { get; }
    decimal Balance { get; }
}
