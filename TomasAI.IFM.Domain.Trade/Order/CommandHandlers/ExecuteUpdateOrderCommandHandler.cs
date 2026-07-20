using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class ExecuteUpdateOrderCommandHandler 
    : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
   IBoundedContextCommandHandler<ExecuteUpdateOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderDoesNotExistErrorMsg(ExecuteUpdateOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} does not exist";

    public static string TradeOrderNotWaitingForTradeFillsErrorMsg(ExecuteUpdateOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} is not waiting for trade fills";

    /// <summary>
    /// execute update order 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(ExecuteUpdateOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
            {
                // check if order has already been placed...
                _ when state.TradeOrderDoesNotExist(e.EntityId) => throw new ExecuteUpdateOrderException(TradeOrderDoesNotExistErrorMsg(e)),
                // check if order is waiting for fills...
                _ when state.NoWaitingTradeFills => throw new ExecuteUpdateOrderException(TradeOrderNotWaitingForTradeFillsErrorMsg(e)),
                // execute update order...
                _ => state.Update(new UpdateOrderExecutedEvent
                {
                    TradeOrderId = e.TradeOrderId,
                    OrderPrice = e.OrderPrice,
                    SubmittedOn = e.OriginatedOn,
                    SubmittedBy = e.OriginatedBy
                }, e)
            };
}
