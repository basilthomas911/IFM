using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;

public static class StartFuturesOptionTickDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StartFuturesOptionTickDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesOptionTickDataStreamingStartedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StartFuturesOptionTickDataStreamingCommand e, FuturesOptionTickDataCommandState state)
        => state.Update(e.CreateFuturesOptionTickDataStreamingStartedEvent(), e);

    internal static FuturesOptionTickDataStreamingStartedEvent CreateFuturesOptionTickDataStreamingStartedEvent(this StartFuturesOptionTickDataStreamingCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataStreamingStartedEvent.Actor, FuturesOptionTickDataStreamingStartedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            Contract = e.Contract,
            BaseContract = e.BaseContract,
            ValueDate = e.ValueDate,
            MaturityDate = e.MaturityDate,
            RiskFreeRate = e.RiskFreeRate,
            StartedOn = e.OriginatedOn,
            StartedBy = e.OriginatedBy
        };
}
