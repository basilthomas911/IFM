using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class CloseTradeOrderCommandHandler 
    : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
   IBoundedContextCommandHandler<CloseOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderDoesNotExistErrorMsg(CloseOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} does not exist";

    public static string NotInOrderFilledStateErrorMsg(CloseOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} is not in order filled state";

    public static string BrokerFailedErrorMsg(CloseOrderCommand e)
        => $"{e.CommandName}: broker failed to close trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} due to {e.ErrorMessage}";

    /// <summary>
    /// close trade order 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(CloseOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
            {
                _ when state.TradeOrderDoesNotExist(e.EntityId) => throw new CloseTradeOrderException(TradeOrderDoesNotExistErrorMsg(e)),
                _ when state.NotInOrderFilledState => throw new CloseTradeOrderException(NotInOrderFilledStateErrorMsg(e)),
                _ when !e.Executed => throw new CloseTradeOrderException(BrokerFailedErrorMsg(e)),
                _ => state.Update(new TradeOrderClosedEvent
                {
                    TradeOrderId = e.EntityId,
                    ClosedOn = e.OriginatedOn,
                    ClosedBy = e.OriginatedBy
                }, e)
            };
}
