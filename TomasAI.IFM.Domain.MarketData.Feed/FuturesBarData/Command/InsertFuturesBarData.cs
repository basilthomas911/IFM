using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;

public static class InsertFuturesBarData
{
    /// <summary>
    /// Handle an <see cref="InsertFuturesBarDataCommand"/> by building the corresponding
    /// <see cref="FuturesBarDataInsertedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this InsertFuturesBarDataCommand e, FuturesBarDataCommandState state)
            => state.Update(e.CreateFuturesBarDataInsertedEvent(), e);

    /// <summary>
    /// Creates a <see cref="FuturesBarDataInsertedEvent"/> from an <see cref="InsertFuturesBarDataCommand"/>.
    /// </summary>
    /// <param name="e">The source insert command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated inserted event ready to be applied to actor state.</returns>
    internal static FuturesBarDataInsertedEvent CreateFuturesBarDataInsertedEvent(this InsertFuturesBarDataCommand e)
    => new()
    {
        CommandId = e.CommandId,
        Subject = new ActorSubject(ActorType.Event, FuturesBarDataInsertedEvent.Actor, FuturesBarDataInsertedEvent.Verb, e.EntityId.Format()),
        EntityId = e.EntityId,
        FuturesBarData = e.FuturesBarData,
        CreatedOn = e.OriginatedOn,
        CreatedBy = e.OriginatedBy
    };

}
