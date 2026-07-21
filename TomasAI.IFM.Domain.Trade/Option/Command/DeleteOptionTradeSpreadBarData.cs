using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

public static class DeleteOptionTradeSpreadBarData
{
    /// <summary>
    /// Executes the delete command for option trade spread bar data by creating and applying a deleted event to the
    /// command state.
    /// </summary>
    /// <param name="e">The delete command containing the spread bar data to remove.</param>
    /// <param name="state">The command state to update with the deleted event.</param>
    /// <returns>true if the state update succeeded; otherwise, false.</returns>
    public static bool Execute(this DeleteOptionTradeSpreadBarDataCommand e, OptionTradeCommandState state)
        => state.Update(e.CreateOptionTradeSpreadBarDataDeletedEvent(), e);

    /// <summary>
    /// Creates an <see cref="OptionTradeSpreadBarDataDeletedEvent"/> from a <see cref="DeleteOptionTradeSpreadBarDataCommand"/>.
    /// Derives the order and trade identifiers from the entity id and captures the trade type and value date for the bar being removed.
    /// </summary>
    /// <param name="e">The command identifying the spread bar data record to delete.</param>
    /// <returns>A fully populated <see cref="OptionTradeSpreadBarDataDeletedEvent"/>.</returns>
    internal static OptionTradeSpreadBarDataDeletedEvent CreateOptionTradeSpreadBarDataDeletedEvent(this DeleteOptionTradeSpreadBarDataCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeSpreadBarDataDeletedEvent.Actor, OptionTradeSpreadBarDataDeletedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.EntityId.OrderId,
            TradeId = e.EntityId.TradeId,
            TradeType = e.TradeType,
            ValueDate = e.ValueDate,
            DeletedOn = e.OriginatedOn,
            DeletedBy = e.OriginatedBy
        };

}
