using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class PlaceTradeOrderCommandHandler : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
   IBoundedContextCommandHandler<PlaceTradeOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderExistsErrorMsg(PlaceTradeOrderCommand e)
           => $"{e.CommandName}: trade order {e.TradeOrder.OrderId}:{e.TradeOrder.TradeId} has already been placed";

    public static string OrderNotPlacedErrorMsg(PlaceTradeOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrder.OrderId}:{e.TradeOrder.TradeId} is not in order placed state";

    /// <summary>
    /// place trade order 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(PlaceTradeOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
            {
                // check if order exists...
                _ when state.TradeOrderExists(e.EntityId) => throw new PlaceTradeOrderException(TradeOrderExistsErrorMsg(e)),
                // check if order in placed state...
                _ when state.OrderNotPlaced(e.TradeOrder.TradeOrderState) => throw new PlaceTradeOrderException(OrderNotPlacedErrorMsg(e)),
                // check if in manual trade and required manual fills quantity == order quantity..
                // place trade order to broker...
                _ => state.Update(new TradeOrderPlacedEvent
                {
                    TradeOrder = e.TradeOrder,
                    SubmittedOn = e.OriginatedOn,
                    SubmittedBy = e.OriginatedBy
                }, e)
            };
}
