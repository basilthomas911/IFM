using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Model;

public class FundTransactionCollection : IFundTransactionCollection
{
    readonly List<IFundTransaction> _fundTransactions;

    public FundTransactionCollection() 
        => _fundTransactions = [];

    public void Add(IFundTransaction fundTransaction) 
        => _fundTransactions.Add(fundTransaction);

    public bool Exists(int fundId, int orderId) 
        => _fundTransactions.Any(e => e.FundId == fundId && e.OrderId == orderId);

    public IFundTransaction? Get(FundTransactionEntityId key, TradeStatus tradeStatus)
        => _fundTransactions
            .Where(e => e.FundId == key.FundId && e.OrderId == key.OrderId && e.TradeStatus == tradeStatus)
            .LastOrDefault();

    public IFundTransaction? Get(int fundId)
        => _fundTransactions
            .Where(e => e.FundId == fundId)
            .LastOrDefault();

    public IFundTransaction? Get(int fundId, DateOnly valueDate)
       => _fundTransactions
           .Where(e => e.FundId == fundId && e.ValueDate == valueDate)
           .LastOrDefault();
}
