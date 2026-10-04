using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public interface ITradeOrderControl
{
    IReadOnlyList<TradeOrderDefinition> SubmittedTradeOrders => [];
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
    void SetExecutionSelection(BrokerOrderType orderType, BrokerAlgorithm algorithm, string timeInForce = "Day", string algorithmPace = "Normal");
}

public interface ITradeOrderPriceSelectionControl
{
    void SetOrderPrice(decimal signedNetDebitLimit);
}

/// <summary>Passes selected option contracts from Market Selection into an emulator order editor.</summary>
public interface ITradeOptionLegSelectionControl
{
    void SetOptionLegSelection(DateOnly expiry, (string ContractId, decimal Strike, bool IsCall)[] legs);
}

/// <summary>Accepts the strategy quantity selected in the unified Broker Trade tab.</summary>
public interface ITradeQuantitySelectionControl
{
    void SetQuantity(int quantity);
}
