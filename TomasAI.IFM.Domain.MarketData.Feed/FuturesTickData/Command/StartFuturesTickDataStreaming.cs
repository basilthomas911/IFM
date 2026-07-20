using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command;

public static class StartFuturesTickDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StartFuturesTickDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesTickDataStreamingStartedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StartFuturesTickDataStreamingCommand e, FuturesTickDataCommandState state)
        => state.Update(e.CreateFuturesTickDataStreamingStartedEvent(), e);

    internal static FuturesTickDataStreamingStartedEvent CreateFuturesTickDataStreamingStartedEvent(this StartFuturesTickDataStreamingCommand e)
       => new()
       {
           Subject = new ActorSubject(ActorType.Event, FuturesTickDataStreamingStartedEvent.Actor, FuturesTickDataStreamingStartedEvent.Verb, e.EntityId.Format()),
           EntityId = new(e.ValueDate),
           Contract = e.Contract,
           ValueDate = e.ValueDate,
           ResetStream = e.ResetStream,
           StartedOn = e.OriginatedOn,
           StartedBy = e.OriginatedBy
       };
}
