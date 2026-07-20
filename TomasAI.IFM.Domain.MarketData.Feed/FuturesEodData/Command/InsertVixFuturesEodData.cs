using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command;

public static class InsertVixFuturesEodData
{
    /// <summary>
    /// Handle an <see cref="InsertVixFuturesEodDataCommand"/> by building the corresponding
    /// <see cref="VixFuturesEodDataInsertedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this InsertVixFuturesEodDataCommand e, FuturesEodDataCommandState state)
        => state.Update(e.CreateVixFuturesEodDataInsertedEvent(), e);

    /// <summary>
    /// Creates a <see cref="VixFuturesEodDataInsertedEvent"/> from an <see cref="InsertVixFuturesEodDataCommand"/>.
    /// </summary>
    /// <param name="e">The source insert command containing entity identifiers, VIX tick data, and origin metadata.</param>
    /// <returns>A fully-populated inserted event ready to be applied to actor state.</returns>
    internal static VixFuturesEodDataInsertedEvent CreateVixFuturesEodDataInsertedEvent(this InsertVixFuturesEodDataCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, VixFuturesEodDataInsertedEvent.Actor, VixFuturesEodDataInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            VixFuturesTickData = e.VixFuturesTickData,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
