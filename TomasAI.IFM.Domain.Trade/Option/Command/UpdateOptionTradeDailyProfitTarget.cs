using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

internal static class UpdateOptionTradeDailyProfitTarget
{
    /// <summary>
    /// Executes the update option trade daily profit target command.
    /// </summary>
    /// <param name="e">The update option trade daily profit target command to execute.</param>
    /// <param name="state">The current state of the option trade command.</param>
    /// <returns>true if the update was successful; otherwise, false.</returns>
    /// <exception cref="UpdateOptionTradeDailyProfitTargetException">Thrown when the trade specified in the command does not exist.</exception>
    public static bool Execute(this UpdateOptionTradeDailyProfitTargetCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeDoesNotExist(e.EntityId) => throw new UpdateOptionTradeDailyProfitTargetException(
                $"{e.CommandName}: tradeId: {e.TradeId} orderId: {e.OrderId} does not exist"),
            _ => state.Update(e.CreateOptionTradeDailyProfitTargetUpdatedEvent(state.TradeType, state.GetDailyProfitTarget(e.TradingDays, e.MaxTradingDays)), e)
        };

    /// <summary>
    /// Creates an <see cref="OptionTradeDailyProfitTargetUpdatedEvent"/> from an
    /// <see cref="UpdateOptionTradeDailyProfitTargetCommand"/>, the resolved trade type, and the calculated daily profit target.
    /// </summary>
    /// <param name="e">The command carrying order, trade, and trading day information.</param>
    /// <param name="tradeType">The trade type (put or call spread) for which the profit target applies.</param>
    /// <param name="dailyProfitTarget">The calculated daily profit target value derived from trading days and max trading days.</param>
    /// <returns>A fully populated <see cref="OptionTradeDailyProfitTargetUpdatedEvent"/>.</returns>
    internal static OptionTradeDailyProfitTargetUpdatedEvent CreateOptionTradeDailyProfitTargetUpdatedEvent(this UpdateOptionTradeDailyProfitTargetCommand e,
        TradeType tradeType, decimal dailyProfitTarget)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeDailyProfitTargetUpdatedEvent.Actor, OptionTradeDailyProfitTargetUpdatedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            TradeType = tradeType,
            DailyProfitTarget = dailyProfitTarget,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };
}
