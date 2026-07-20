using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;

public static class DeleteFuturesBarData
{
    /// <summary>
    /// Handle a <see cref="DeleteFuturesBarDataCommand"/> by building the corresponding
    /// <see cref="FuturesBarDataDeletedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this DeleteFuturesBarDataCommand e, FuturesBarDataCommandState state)
        => state.Update(e.CreateFuturesBarDataDeletedEvent(), e);

    /// <summary>
    /// Creates a <see cref="FuturesBarDataDeletedEvent"/> from a <see cref="DeleteFuturesBarDataCommand"/>.
    /// </summary>
    /// <param name="e">The source delete command containing entity identifiers and origin metadata.</param>
    /// <returns>A fully-populated deleted event ready to be applied to actor state.</returns>
    internal static FuturesBarDataDeletedEvent CreateFuturesBarDataDeletedEvent(this DeleteFuturesBarDataCommand e)
    => new()
    {
        Subject = new ActorSubject(ActorType.Event, FuturesBarDataDeletedEvent.Actor, FuturesBarDataDeletedEvent.Verb, e.EntityId.Format()),
        EntityId = e.EntityId,
        BarDataId = e.Id,
        CreatedOn = e.OriginatedOn,
        CreatedBy = e.OriginatedBy
    };

}
