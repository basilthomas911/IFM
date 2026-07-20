using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Domain.Trade.Order.Exceptions;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Domain.Trade.Order.CommandHandlers;

public class FillTradeOrderCommandHandler : BaseBoundedContextCommandHandler<TradeOrderBoundedContextState>,
   IBoundedContextCommandHandler<FillOrderCommand, TradeOrderBoundedContextState>
{
    public static string TradeOrderDoesNotExistErrorMsg(FillOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} does not exist";

    public static string NoWaitingTradeFillsErrorMsg(FillOrderCommand e)
        => $"{e.CommandName}: trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} is not waiting for order fills";

    public static string BrokerFailedErrorMsg(FillOrderCommand e)
        => $"{e.CommandName}: broker failed to fill trade order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} due to {e.ErrorMessage}";

    /// <summary>
    /// fill trade order 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"> current trade order bounded context state</param>
    public bool Execute(FillOrderCommand e, TradeOrderBoundedContextState state)
        => e switch
            {
                _ when state.TradeOrderDoesNotExist(e.EntityId) => throw new FillTradeOrderException(TradeOrderDoesNotExistErrorMsg(e)),
                _ when state.NoWaitingTradeFills => throw new FillTradeOrderException(NoWaitingTradeFillsErrorMsg(e)),
                _ when !e.Executed => throw new FillTradeOrderException(BrokerFailedErrorMsg(e)),
                _ => state.Update(new TradeOrderFilledEvent
                {
                    TradeOrderId = e.EntityId,
                    TradeFill = e.TradeFill,
                    FilledOn = e.OriginatedOn,
                    FilledBy = e.OriginatedBy
                }, e),
            };
}
