using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

public static class CloseOptionTradePosition
{
    /// <summary>
    /// Executes the command to close an option trade position by validating the trade exists and is not already closed,
    /// then updating the state.
    /// </summary>
    /// <param name="e">The command containing the option trade position to close.</param>
    /// <param name="state">The current state of the option trade command.</param>
    /// <returns>The result of the state update operation.</returns>
    /// <exception cref="OpenOptionTradeException">The trade does not exist or is already closed.</exception>
    public static bool Execute(this CloseOptionTradePositionCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when !state.TradeExists(e.EntityId) => throw new OpenOptionTradeException(
                $"{e.CommandName}: option trade {e.EntityId.OrderId}/{e.EntityId.TradeId} does not exist"),
            _ when state.TradePositionState == TradePositionState.Closed => throw new OpenOptionTradeException(
                $"{e.CommandName}: option trade {e.EntityId.OrderId}/{e.EntityId.TradeId} already closed"),
            _ => state.Update(e.CreateOptionTradePositionClosedEvent(), e)
        };

    /// <summary>
    /// Creates an <see cref="OptionTradePositionClosedEvent"/> from the specified command.
    /// </summary>
    /// <param name="e">The command containing the data for closing the option trade position.</param>
    /// <returns>An event representing the closed option trade position.</returns>
    internal static OptionTradePositionClosedEvent CreateOptionTradePositionClosedEvent(this CloseOptionTradePositionCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradePositionClosedEvent.Actor, OptionTradePositionClosedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OptionTradeId = e.OptionTradeId,
            TradePositionState = TradePositionState.Closed,
            ClosedOn = e.OriginatedOn,
            ClosedBy = e.OriginatedBy
        };

}
