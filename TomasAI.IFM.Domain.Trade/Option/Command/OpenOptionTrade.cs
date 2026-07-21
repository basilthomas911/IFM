using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

internal static class OpenOptionTrade
{
    /// <summary>
    /// Executes the open option trade command by creating and applying an option trade open event to the state.
    /// </summary>
    /// <param name="e">The command containing the option trade details to open.</param>
    /// <param name="state">The current state of option trade commands.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OpenOptionTradeException">Thrown when an option trade with the specified entity ID already exists.</exception>
    public static bool Execute(this OpenOptionTradeCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeExists(e.EntityId) => throw new OpenOptionTradeException(
                $"{e.CommandName} option trade: {e.TradeOrder.TradeId} exists already"),
            _ => state.Update(e.CreateOptionTradeToOpenEvent(), e)
        };

    /// <summary>
    /// Creates an <see cref="OptionTradeToOpenEvent"/> from an <see cref="OpenOptionTradeCommand"/>.
    /// Records the trade order and the originating user and timestamp for the open operation.
    /// </summary>
    /// <param name="e">The command containing the trade order and origination details.</param>
    /// <returns>A fully populated <see cref="OptionTradeToOpenEvent"/>.</returns>
    internal static OptionTradeToOpenEvent CreateOptionTradeToOpenEvent(this OpenOptionTradeCommand e)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeToOpenEvent.Actor, OptionTradeToOpenEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            TradeOrder = e.TradeOrder,
            OpenedOn = e.OriginatedOn,
            OpenedBy = e.OriginatedBy
        };

}
