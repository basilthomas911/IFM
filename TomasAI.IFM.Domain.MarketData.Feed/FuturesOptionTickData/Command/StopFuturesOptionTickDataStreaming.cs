using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;

public static class StopFuturesOptionTickDataStreaming
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ServiceResult<GuidResult> Execute(this StopFuturesOptionTickDataStreamingCommand e, FuturesOptionTickDataCommandState state)
        => e.UpdateResult(() => state.Update(e.CreateFuturesOptionTickDataStreamingStoppedEvent(), e));

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
