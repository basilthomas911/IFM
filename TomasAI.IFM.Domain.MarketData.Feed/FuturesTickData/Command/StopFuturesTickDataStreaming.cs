using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command;

public static class StopFuturesTickDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StopFuturesTickDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesTickDataStreamingStoppedEvent"/> and updating the actor state.
    /// </summary>
    public static ServiceResult<GuidResult> Execute(this StopFuturesTickDataStreamingCommand e, FuturesTickDataCommandState state)
        => e.UpdateResult(() => state.Update(e.CreateFuturesTickDataStreamingStoppedEvent(), e));

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
