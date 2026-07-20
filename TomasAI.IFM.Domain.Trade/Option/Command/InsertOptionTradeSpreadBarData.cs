using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command;

public static class InsertOptionTradeSpreadBarData
{
    /// <summary>
    /// Executes the command to insert option trade spread bar data into the specified state.
    /// </summary>
    /// <param name="e">The command containing the spread bar data to insert.</param>
    /// <param name="state">The current state of the option trade command system.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InsertOptionTradeSpreadDataException">Thrown when the trade specified by <paramref name="e"/>.EntityId does not exist in the state.</exception>
    public static bool Execute(this InsertOptionTradeSpreadBarDataCommand e, OptionTradeCommandState state)
        => e switch
        {
            _ when state.TradeDoesNotExist(e.EntityId) => throw new InsertOptionTradeSpreadDataException(
                $"{e.CommandName}: {e.EntityId.TradeId}:{e.EntityId.OrderId} does not exist"),
            _ => state.Update(e.CreateOptionTradeSpreadBarDataInsertedEvent(), e)
        };

    /// <summary>
    /// Converts the command to an OptionTradeSpreadBarDataInsertedEvent.
    /// </summary>
    /// <param name="e">The command to convert.</param>
    /// <returns>The created event.</returns>
    internal static OptionTradeSpreadBarDataInsertedEvent CreateOptionTradeSpreadBarDataInsertedEvent(this InsertOptionTradeSpreadBarDataCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeSpreadBarDataInsertedEvent.Actor, OptionTradeSpreadBarDataInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OptionTradeSpreadBarData = e.OptionTradeSpreadBarData,
            InsertedOn = e.OriginatedOn,
            InsertedBy = e.OriginatedBy
        };

}
