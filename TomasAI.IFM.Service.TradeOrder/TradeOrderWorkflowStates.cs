using System;

namespace TomasAI.IFM.Service.TradeOrder
{
    public enum TradeOrderWorkflowStates
    {
        OrderSubmitted,
        OrderRejected,
        OrderPlaced,
        OrderCancelled,
        OrderCancelRejected,
        OrderUpdated,
        OrderUpdateRejected,
        OrderPartiallyFilled,
        OrderFilled
    }
}
