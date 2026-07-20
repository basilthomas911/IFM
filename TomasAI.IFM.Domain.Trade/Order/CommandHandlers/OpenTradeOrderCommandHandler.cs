using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class OpenTradeOrderCommandHandler 
    : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
   IBoundedContextCommandHandler<OpenOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderDoesNotExistErrorMsg(OpenOrderCommand e)
          => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} does not exist";

    public static string NotInOrderPlacedErrorMsg(OpenOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} is not in order placed state";

    public static string BrokerFailedToOpenOrder(OpenOrderCommand e)
        => $"{e.CommandName}: broker failed to open trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} due to {e.ErrorMessage}";

    /// <summary>
    /// open trade order 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(OpenOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
            {
                // check if order exists...
                _ when state.TradeOrderDoesNotExist(e.EntityId) => throw new OpenTradeOrderException(TradeOrderDoesNotExistErrorMsg(e)),
                // check if order in placed state...
                _ when state.NotInOrderPlaced => throw new OpenTradeOrderException(NotInOrderPlacedErrorMsg(e)),
                // broker failed to place order...
                _ when !e.Executed => throw new OpenTradeOrderException(BrokerFailedToOpenOrder(e)),
                // broker opened order...
                _ => state.Update(new TradeOrderOpenedEvent
                {
                    TradeOrderId = e.TradeOrderId,
                    OpenedOn = e.OriginatedOn,
                    OpenedBy = e.OriginatedBy,
                }, e)
            };

}
