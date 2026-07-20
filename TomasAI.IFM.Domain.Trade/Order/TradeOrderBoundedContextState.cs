using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order;

public class TradeOrderBoundedContextState 
    : BaseBoundedContextState<TradeOrderBoundedContextState>
{
    TradeOrderReadModel? _tradeOrder;

    /// <summary>
    /// Initialize the bounded context state with an empty trade order.
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                TradeOrderPlacedEvent e => On(e),
                TradeOrderOpenedEvent e => On(e),
                TradeOrderFilledEvent e => On(e),
                TradeOrderCancelledEvent e => On(e),
                TradeOrderUpdatedEvent e => On(e),
                TradeOrderClosedEvent e => On(e),
                _ => false
            }; ;
        }
        catch { }
        return false;
    }

    bool On(TradeOrderPlacedEvent e)
    {
        if (!TradeOrderExists(e.TradeOrder.EntityId))
            _tradeOrder = e.TradeOrder with { TradeOrderState = TradeOrderState.OrderPlaced }; ;
        return true;
    }

    bool On(TradeOrderOpenedEvent e)
    {
        if (TradeOrderExists(e.TradeOrderId))
            _tradeOrder = _tradeOrder! with { TradeOrderState = TradeOrderState.OrderOpened };
        return true;
    }

    bool On(TradeOrderFilledEvent e)
    {
        if (TradeOrderExists(e.TradeOrderId))
        {
            if (e.TradeFill is not null)
                _tradeOrder?.AddTradeFill(e.TradeFill);
            var tradeOrderState = _tradeOrder?.OrderQuantity == _tradeOrder?.FilledQuantity
                ? TradeOrderState.OrderFilled 
                : TradeOrderState.OrderPartiallyFilled;
            _tradeOrder = _tradeOrder! with { TradeOrderState = tradeOrderState };
            EventInitHelper.SetProperty(e, nameof(TradeOrderFilledEvent.TradeOrderState), tradeOrderState);
        }
        return true;
    }

    bool On(TradeOrderUpdatedEvent e)
    {
        if (TradeOrderExists(e.TradeOrderId))
            _tradeOrder = _tradeOrder! with { OrderPrice = e.OrderPrice };
        return true;
    }

    bool On(TradeOrderCancelledEvent e)
    {
        if (TradeOrderExists(e.TradeOrderId))
            _tradeOrder = _tradeOrder! with { TradeOrderState = TradeOrderState.OrderCancelled };
        return true;
    }

    bool On(TradeOrderClosedEvent e)
    {
        if (TradeOrderExists(e.TradeOrderId))
            _tradeOrder = _tradeOrder! with { TradeOrderState = TradeOrderState.OrderClosed };
        return true;
    }

    internal bool InOrderPlaced => _tradeOrder?.TradeOrderState == TradeOrderState.OrderPlaced;
    internal bool NotInOrderPlaced => !InOrderPlaced;
    internal bool InOrderFilledState => _tradeOrder?.TradeOrderState == TradeOrderState.OrderFilled;
    internal bool NotInOrderFilledState => !InOrderFilledState;
    internal bool WaitingForTradeFills => _tradeOrder?.TradeOrderState == TradeOrderState.OrderOpened || _tradeOrder?.TradeOrderState == TradeOrderState.OrderPartiallyFilled;
    internal bool NoWaitingTradeFills => !WaitingForTradeFills;

    internal bool TradeOrderExists(TradeOrderEntityId entityId) => _tradeOrder is not null && _tradeOrder.EntityId == entityId;
    internal bool TradeOrderDoesNotExist(TradeOrderEntityId entityId) => !TradeOrderExists(entityId);
    internal bool IsOrderPlaced(TradeOrderState tradeOrderState) => tradeOrderState == TradeOrderState.OrderPlaced;
    internal bool OrderNotPlaced(TradeOrderState tradeOrderState) =>!IsOrderPlaced(tradeOrderState);
    internal bool CanPlaceBrokerTrade(TradeOrderState tradeOrderState) => tradeOrderState == TradeOrderState.OrderOpened || tradeOrderState == TradeOrderState.OrderFilled;
    internal bool CanPlaceManualTrade(TradeState tradeState) => tradeState == TradeState.TradeToOpen || tradeState == TradeState.TradeToClose;
    internal bool IsOpenOrderAction(OrderActionType orderActionType) => orderActionType == OrderActionType.Open;
    internal bool IsCloseOrderAction(OrderActionType orderActionType) => orderActionType == OrderActionType.Close;
    internal bool IsBrokerTrade(TradeFillType tradeFillType) => tradeFillType == TradeFillType.Broker;
    internal bool IsManualTrade(TradeFillType tradeFillType) => tradeFillType == TradeFillType.Manual;

}
