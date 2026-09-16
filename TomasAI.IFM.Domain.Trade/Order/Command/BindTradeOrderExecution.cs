using TomasAI.IFM.Domain.Trade.Order.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command;

/// <summary>Handles <see cref="BindTradeOrderExecutionCommand"/>.</summary>
public static class BindTradeOrderExecution
{
    /// <summary>Applies the order business guards and state transition.</summary>
    public static ServiceResult<GuidResult> Execute(this BindTradeOrderExecutionCommand command, TradeOrderCommandState state)
        => TradeOrderTransition.Execute(command, state);
}
