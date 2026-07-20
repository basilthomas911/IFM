using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class ExecuteCancelOrderCommandHandler 
    : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
   IBoundedContextCommandHandler<ExecuteCancelOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderDoesNotExistErrorMsg(ExecuteCancelOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} does not exist";

    public static string NoWaitingTradeFillsErrorMsg(ExecuteCancelOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} is not waiting for order fills";

    /// <summary>
    /// execute cancel order for broker execution
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(ExecuteCancelOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
            {
                // check if trade order already exists...
                _ when state.TradeOrderDoesNotExist(e.EntityId) => throw new ExecuteCancelOrderException(TradeOrderDoesNotExistErrorMsg(e)),
                // check if waiting for trade fills...
                _ when state.NoWaitingTradeFills => throw new ExecuteCancelOrderException(NoWaitingTradeFillsErrorMsg(e)),
                // execute cancel order...
                _ => state.Update(new CancelOrderExecutedEvent
                {
                    TradeOrderId = e.TradeOrderId,
                    ExecutedOn = e.OriginatedOn,
                    ExecutedBy = e.OriginatedBy
                }, e)
            };
}
