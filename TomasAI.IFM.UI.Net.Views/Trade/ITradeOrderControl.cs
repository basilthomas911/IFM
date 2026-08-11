using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public interface ITradeOrderControl
{
    DateOnly MaturityDate { get; }
    void RemoveTrade(int fundid, int orderId, int tradeId);
    Task SubmitOrderAsync(
        DateOnly tradeDate,
        OrderActionType orderAction,
        ITradeOrderConfirmationService tradeOrderConfirmation,
        Action<Guid> setCommandId);
    void LiveFeed(bool enabled);
    void SetNearestStrikePrices();
    void OrderActionTypeChanged(OrderActionType orderActionType);
}
