using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;

public static class InsertFuturesOptionTickData
{
    /// <summary>
    /// Handle an <see cref="InsertFuturesOptionTickDataCommand"/> by building the corresponding
    /// <see cref="FuturesOptionTickDataInsertedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this InsertFuturesOptionTickDataCommand e, FuturesOptionTickDataCommandState state)
        => state.Update(e.CreateFuturesOptionTickDataInsertedEvent(), e);

    internal static FuturesOptionTickDataInsertedEvent CreateFuturesOptionTickDataInsertedEvent(this InsertFuturesOptionTickDataCommand e)
       => new()
       {
           Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataInsertedEvent.Actor, FuturesOptionTickDataInsertedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           Contract = e.Contract,
           TickData = e.OptionTickData,
           CreatedOn = e.OriginatedOn,
           CreatedBy = e.OriginatedBy
       };
}
