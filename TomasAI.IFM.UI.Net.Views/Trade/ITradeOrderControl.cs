using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public interface ITradeOrderControl
{
    DateOnly MaturityDate { get; }
    Task RemoveTradeAsync(int fundid, int orderId, int tradeId);
    Task<Guid> SubmitOrderAsync(
        DateOnly tradeDate,
        OrderActionType orderAction,
        ITradeOrderConfirmationService tradeOrderConfirmation);
    Task SetLiveFeedAsync(bool enabled);
    void SetNearestStrikePrices();
    Task OrderActionTypeChangedAsync(OrderActionType orderActionType);
}

/// <summary>Accepts the broker execution choices owned by the unified blotter shell.</summary>
public interface ITradeExecutionSelectionControl
{
    void SetExecutionSelection(BrokerOrderType orderType, BrokerAlgorithm algorithm);
}
