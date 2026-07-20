using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class UpdateTradeOrderCommandHandler 
    : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
   IBoundedContextCommandHandler<UpdateOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderDoesNotExistErrorMsg(UpdateOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} does not exist";

    public static string NoWaitingTradeFillsErrorMsg(UpdateOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} is not waiting for trade fills";

    public static string BrokerFailedToUpdateTradeOrderErrorMsg(UpdateOrderCommand e)
        => $"{e.CommandName}: broker failed to update trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} due to {e.ErrorMessage}";
    /// <summary>
    /// update order 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(UpdateOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
            {
                // check if trade order exists...
                _ when state.TradeOrderDoesNotExist(e.EntityId) => throw new UpdateTradeOrderException(TradeOrderDoesNotExistErrorMsg(e)),
                // check if we are waiting for trade fills
                _ when state.NoWaitingTradeFills => throw new UpdateTradeOrderException(NoWaitingTradeFillsErrorMsg(e)),
                // broker failed to update trade order...
                _ when e.Executed => throw new UpdateTradeOrderException(BrokerFailedToUpdateTradeOrderErrorMsg(e)),
                // trade order was updated by broker...
                _ => state.Update(new TradeOrderUpdatedEvent
                {
                    TradeOrderId = e.TradeOrderId,
                    OrderPrice = e.OrderPrice,
                    UpdatedOn = e.OriginatedOn,
                    UpdatedBy = e.OriginatedBy
                }, e)
            };
}
