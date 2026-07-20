using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command;

internal static class PlaceOptionTradeOrder
{
    /// <summary>
    /// Executes the place option trade order command by validating trade existence, fill type, order action, and
    /// position status before updating the state.
    /// </summary>
    /// <param name="e">The command containing the option trade order to place.</param>
    /// <param name="state">The current state used for validation and update operations.</param>
    /// <returns><see langword="true"/> if the order was successfully placed and state updated; otherwise, <see
    /// langword="false"/>.</returns>
    /// <exception cref="PlaceOptionTradeOrderException">Thrown when the trade already exists, the trade state is invalid for the specified fill type, or position
    /// statuses do not match the order action type.</exception>
    public static bool Execute(this PlaceOptionTradeOrderCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeExists(e.EntityId) => throw new PlaceOptionTradeOrderException(
                $"{e.CommandName} tradeId: {e.OptionTrade.TradeId} already exists"),
            _ when e.TradeOrder.TradeFillType == TradeFillType.Broker && !state.CanPlaceBrokerTrade(e.OptionTrade) => throw new PlaceOptionTradeOrderException(
                $"{e.CommandName} tradeId: {e.OptionTrade.TradeId} trade state: {e.OptionTrade.TradeState} must be in OrderPlaced or OrderFilled to place broker trade"),
            _ when e.TradeOrder.TradeFillType == TradeFillType.Manual && !state.CanPlaceManualTrade(e.OptionTrade) => throw new PlaceOptionTradeOrderException(
                $"{e.CommandName} tradeId: {e.OptionTrade.TradeId} trade state: {e.OptionTrade.TradeState} must be in TradeToOpen or TradeToClose to place manual trade"),
            _ when e.TradeOrder.OrderActionType == OrderActionType.Open && !state.AllTradePositionsInOpenStatus(e.OptionTrade) => throw new PlaceOptionTradeOrderException(
                $"{e.CommandName} tradeId: {e.OptionTrade.TradeId} all trade position status MUST be Open to place open order"),
            _ when e.TradeOrder.OrderActionType == OrderActionType.Close && !state.AllTradePositionsInCloseStatus(e.OptionTrade) => throw new PlaceOptionTradeOrderException(
                $"{e.CommandName} tradeId: {e.OptionTrade.TradeId} all trade position status MUST be Close to place close order"),
            _ => state.Update(e.CreateOptionTradeOrderPlacedEvent(), e)
        };

    /// <summary>
    /// Creates an <see cref="OptionTradeOrderPlacedEvent"/> from a <see cref="PlaceOptionTradeOrderCommand"/>.
    /// Captures fund, pricing, order action/type, commission, value date, and trade P&amp;L from the trade order.
    /// </summary>
    /// <param name="e">The command containing the trade order details to place.</param>
    /// <returns>A fully populated <see cref="OptionTradeOrderPlacedEvent"/>.</returns>
    internal static OptionTradeOrderPlacedEvent CreateOptionTradeOrderPlacedEvent(this PlaceOptionTradeOrderCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeOrderPlacedEvent.Actor, OptionTradeOrderPlacedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FundId = e.TradeOrder.FundId,
            OrderPrice = e.TradeOrder.OrderPrice,
            OrderAction = e.TradeOrder.OrderActionType,
            OrderType = e.TradeOrder.OrderType,
            OptionTrade = e.OptionTrade,
            TradeCommission = e.TradeOrder.Commission,
            ValueDate = e.TradeOrder.ValueDate,
            TradePnl = e.TradeOrder.TradePnl,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

}
