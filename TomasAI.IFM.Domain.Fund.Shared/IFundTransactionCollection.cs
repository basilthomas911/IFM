using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared;

public interface IFundTransactionCollection 
{
    bool Exists(int fundId, int orderId);
    void Add(IFundTransaction fundTransaction);
    IFundTransaction? Get(FundTransactionEntityId key, TradeStatus tradeStatus);
    IFundTransaction? Get(int fundId);
}
