using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command;

public static class StopFuturesTickDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StopFuturesTickDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesTickDataStreamingStoppedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StopFuturesTickDataStreamingCommand e, FuturesTickDataCommandState state)
        => state.Update(e.CreateFuturesTickDataStreamingStoppedEvent(), e);

    internal static FuturesTickDataStreamingStoppedEvent CreateFuturesTickDataStreamingStoppedEvent(this StopFuturesTickDataStreamingCommand e)
       => new()
       {
           Subject = new ActorSubject(ActorType.Event, FuturesTickDataStreamingStoppedEvent.Actor, FuturesTickDataStreamingStoppedEvent.Verb, e.EntityId.Format()),
           EntityId = new(e.ValueDate),
           ContractId = e.ContractId,
           StoppedOn = e.OriginatedOn,
           StoppedBy = e.OriginatedBy
       };
}
