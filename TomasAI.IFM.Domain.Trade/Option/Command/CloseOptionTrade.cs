using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

public static class CloseOptionTrade
{
    /// <summary>
    /// Executes the close option trade command by updating the state with the close event.
    /// </summary>
    /// <param name="e">The close option trade command to execute.</param>
    /// <param name="state">The current option trade command state.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="CloseOptionTradeException">Thrown when a trade with the specified entity ID already exists.</exception>
    public static bool Execute(this CloseOptionTradeCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeExists(e.EntityId) => throw new CloseOptionTradeException(
                $"{e.CommandName}: trade: {e.TradeOrder.TradeId} exists already"),
            _ => state.Update(e.CreateOptionTradeToCloseEvent(), e)
        };

    /// <summary>
    /// Creates an OptionTradeToCloseEvent from the command.
    /// </summary>
    /// <param name="e">The command containing option trade closure information.</param>
    /// <returns>A new OptionTradeToCloseEvent with properties mapped from the command.</returns>
    internal static OptionTradeToCloseEvent CreateOptionTradeToCloseEvent(this CloseOptionTradeCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeToCloseEvent.Actor, OptionTradeToCloseEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            TradeOrder = e.TradeOrder,
            ClosedOn = e.OriginatedOn,
            ClosedBy = e.OriginatedBy
        };

}
