using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command;

public static class DeleteOptionTrade
{
    /// <summary>
    /// Executes a delete option trade command by validating the trade exists and updating the state with a deletion
    /// event.
    /// </summary>
    /// <param name="e">The delete option trade command to execute.</param>
    /// <param name="state">The current state of option trade commands.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="DeleteOptionTradeException">The trade does not exist in the current state.</exception>
    public static bool Execute(this DeleteOptionTradeCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeDoesNotExist(e.EntityId) => throw new DeleteOptionTradeException(
                $"{e.CommandName}: {e.TradeId} orderId: {e.OrderId} invalid trade"),
            _ => state.Update(e.CreateOptionTradeDeletedEvent(), e)
        };

    /// <summary>
    /// Creates an <see cref="OptionTradeDeletedEvent"/> from a <see cref="DeleteOptionTradeCommand"/>.
    /// Captures the order and trade identifiers being removed along with audit information.
    /// </summary>
    /// <param name="e">The command identifying the option trade to delete.</param>
    /// <returns>A fully populated <see cref="OptionTradeDeletedEvent"/>.</returns>
    internal static OptionTradeDeletedEvent CreateOptionTradeDeletedEvent(this DeleteOptionTradeCommand e)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeDeletedEvent.Actor, OptionTradeDeletedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

}
