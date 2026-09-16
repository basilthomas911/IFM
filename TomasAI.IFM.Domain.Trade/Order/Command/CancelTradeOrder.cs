using TomasAI.IFM.Domain.Trade.Order.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command;

/// <summary>Handles <see cref="CancelTradeOrderCommand"/>.</summary>
public static class CancelTradeOrder
{
    /// <summary>Applies the order business guards and state transition.</summary>
    public static ServiceResult<GuidResult> Execute(this CancelTradeOrderCommand command, TradeOrderCommandState state)
        => TradeOrderTransition.Execute(command, state);
}
