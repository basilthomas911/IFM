using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command;

public static class InsertFuturesTickData
{
    /// <summary>
    /// Handle an <see cref="InsertFuturesTickDataCommand"/> by building the corresponding
    /// <see cref="FuturesTickDataInsertedEvent"/> and updating the actor state.
    /// </summary>
    public static ServiceResult<GuidResult> Execute(this InsertFuturesTickDataCommand e, FuturesTickDataCommandState state)
        => e.UpdateResult(() => state.Update(e.CreateFuturesTickDataInsertedEvent(), e));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    internal static FuturesTickDataInsertedEvent CreateFuturesTickDataInsertedEvent(this InsertFuturesTickDataCommand e)
         => new()
         {
             Subject = new ActorSubject(ActorType.Event, FuturesTickDataInsertedEvent.Actor, FuturesTickDataInsertedEvent.Verb, e.EntityId.Format()),
             EntityId = new(e.TickData.ContractId, e.TickData.ValueDate, e.TickData.TickId),
             Contract = e.Contract,
             TickData = e.TickData,
             CreatedOn = e.OriginatedOn,
             CreatedBy = e.OriginatedBy
         };
}
