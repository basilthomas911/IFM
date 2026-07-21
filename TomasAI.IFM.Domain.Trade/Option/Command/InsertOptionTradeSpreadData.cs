using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

internal static class InsertOptionTradeSpreadData
{
    /// <summary>
    /// Executes the command to insert option trade spread data.
    /// </summary>
    /// <param name="e">The command containing the spread data to insert.</param>
    /// <param name="state">The current command state.</param>
    /// <returns>The result of updating the state with the spread data inserted event.</returns>
    /// <exception cref="InsertOptionTradeSpreadDataException">Thrown when the trade does not exist.</exception>
    public static bool Execute(this InsertOptionTradeSpreadDataCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeDoesNotExist(e.EntityId) => throw new InsertOptionTradeSpreadDataException(
                $"{e.CommandName}: {e.EntityId.TradeId}:{e.EntityId.OrderId} does not exist"),
            _ => state.Update(e.CreateOptionTradeSpreadDataInsertedEvent(), e)
        };

    /// <summary>
    /// Creates an <see cref="OptionTradeSpreadDataInsertedEvent"/> from an <see cref="InsertOptionTradeSpreadDataCommand"/>.
    /// Embeds the full spread data payload from the command into the event.
    /// </summary>
    /// <param name="e">The command containing the spread data to insert.</param>
    /// <returns>A fully populated <see cref="OptionTradeSpreadDataInsertedEvent"/>.</returns>
    internal static OptionTradeSpreadDataInsertedEvent CreateOptionTradeSpreadDataInsertedEvent(this InsertOptionTradeSpreadDataCommand e)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeSpreadDataInsertedEvent.Actor, OptionTradeSpreadDataInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OptionTradeSpreadData = e.OptionTradeSpreadData,
            InsertedOn = e.OriginatedOn,
            InsertedBy = e.OriginatedBy
        };

}
