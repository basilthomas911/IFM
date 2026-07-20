using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.ViewModels.Trade;

namespace TomasAI.IFM.Views.Trade;

public interface ITradeOrderControl
{
    DateOnly MaturityDate { get; }
    void RemoveTrade(int fundid, int orderId, int tradeId);
    void SubmitOrder(DateOnly  tradeDate, OrderActionType orderAction, TradeOrderConfirmationViewModel tradeOrderConfirmation, Action<Guid> setCommandId);
    void LiveFeed(bool enabled);
    void SetNearestStrikePrices();
    void OrderActionTypeChanged(OrderActionType orderActionType);
}
