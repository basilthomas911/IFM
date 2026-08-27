using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;

public static class InsertFuturesOptionTickPriceData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ServiceResult<GuidResult> Execute(this InsertFuturesOptionTickPriceDataCommand e, FuturesOptionTickDataCommandState state)
        => e.UpdateResult(() => state.Update(e.CreateFuturesOptionTickPriceDataInsertedEvent(), e));

    internal static FuturesOptionTickPriceDataInsertedEvent CreateFuturesOptionTickPriceDataInsertedEvent(this InsertFuturesOptionTickPriceDataCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickPriceDataInsertedEvent.Actor, FuturesOptionTickPriceDataInsertedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            Contract = e.Contract,
            TickData = e.OptionTickData,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
