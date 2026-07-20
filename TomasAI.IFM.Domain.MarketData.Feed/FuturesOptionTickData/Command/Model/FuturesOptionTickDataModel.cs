using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Model;

internal static class FuturesOptionTickDataModel
{
   

    

    

    internal static FuturesOptionTickDataStreamingStoppedEvent CreateFuturesOptionTickDataStreamingStoppedEvent(this StopFuturesOptionTickDataStreamingCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataStreamingStoppedEvent.Actor, FuturesOptionTickDataStreamingStoppedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            ContractId = e.ContractId,
            StoppedOn = e.OriginatedOn,
            StoppedBy = e.OriginatedBy
        };
}
