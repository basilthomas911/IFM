using TomasAI.IFM.Domain.Trade.Order.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command;

/// <summary>Handles <see cref="CompleteTradeOrderCommand"/>.</summary>
public static class CompleteTradeOrder
{
    /// <summary>Applies the order business guards and state transition.</summary>
    public static ServiceResult<GuidResult> Execute(this CompleteTradeOrderCommand command, TradeOrderCommandState state)
        => TradeOrderTransition.Execute(command, state);
}
