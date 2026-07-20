using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Shared.TradeOrder.Events;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class CancelTradeOrderCommandHandler 
    : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
    IBoundedContextCommandHandler<CancelOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderDoesNotExistErrorMsg(CancelOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.OrderId} does not exist";

    public static string NoWaitingTradeFillsErrorMsg(CancelOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.OrderId} is not waiting for trade fills";

    public static string BrokerFailedErrorMsg(CancelOrderCommand e)
        => $"{e.CommandName}: broker failed to cancel trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.OrderId} due to {e.ErrorMessage}";

    /// <summary>
    /// cancel trade order 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(CancelOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
        {
            // check if trade order exists...
            _ when state.TradeOrderDoesNotExist(e.EntityId) => throw new CancelTradeOrderException(TradeOrderDoesNotExistErrorMsg(e)),
            // check if we are waiting for trade fills
            _ when state.NoWaitingTradeFills => throw new CancelTradeOrderException(NoWaitingTradeFillsErrorMsg(e)),
            // broker failed to cancel trade order...
            _ when !e.Executed => throw new CancelTradeOrderException(BrokerFailedErrorMsg(e)),
            // trade order was cancelled by broker...
            _ => state.Update(new TradeOrderCancelledEvent
            {
                TradeOrderId = e.TradeOrderId,
                CancelledOn = e.OriginatedOn,
                CancelledBy = e.OriginatedBy
            }, e)
        };
}
