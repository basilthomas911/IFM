using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Model;

public class FundTransactionCollection : IFundTransactionCollection
{
    readonly HashSet<(int FundId, int OrderId)> _fundOrders = [];
    readonly Dictionary<(int FundId, int OrderId, TradeStatus Status), IFundTransaction> _latestByOrderAndStatus = [];
    readonly Dictionary<int, IFundTransaction> _latestByFund = [];
    readonly Dictionary<(int FundId, DateOnly ValueDate), IFundTransaction> _latestByFundAndDate = [];

    public FundTransactionCollection() { }

    public void Add(IFundTransaction fundTransaction)
    {
        _fundOrders.Add((fundTransaction.FundId, fundTransaction.OrderId));
        _latestByOrderAndStatus[(fundTransaction.FundId, fundTransaction.OrderId, fundTransaction.TradeStatus)] = fundTransaction;
        _latestByFund[fundTransaction.FundId] = fundTransaction;
        _latestByFundAndDate[(fundTransaction.FundId, fundTransaction.ValueDate)] = fundTransaction;
    }

    public bool Exists(int fundId, int orderId) 
        => _fundOrders.Contains((fundId, orderId));

    public IFundTransaction? Get(FundTransactionEntityId key, TradeStatus tradeStatus)
        => _latestByOrderAndStatus.TryGetValue((key.FundId, key.OrderId, tradeStatus), out var transaction)
            ? transaction
            : null;

    public IFundTransaction? Get(int fundId)
        => _latestByFund.TryGetValue(fundId, out var transaction) ? transaction : null;

    public IFundTransaction? Get(int fundId, DateOnly valueDate)
       => _latestByFundAndDate.TryGetValue((fundId, valueDate), out var transaction) ? transaction : null;
}
