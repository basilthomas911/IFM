using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;

public static class StopFuturesBarDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StopFuturesBarDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesBarDataStreamingStoppedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StopFuturesBarDataStreamingCommand e, FuturesBarDataCommandState state)
        => state.Update(e.CreateFuturesBarDataStreamingStoppedEvent(), e);

    /// <summary>
    /// Creates a <see cref="FuturesBarDataStreamingStoppedEvent"/> from a
    /// <see cref="StopFuturesBarDataStreamingCommand"/>.
    /// </summary>
    /// <param name="e">The source stop-stream command containing entity identifiers and origin metadata.</param>
    /// <returns>A fully-populated streaming-stopped event ready to be applied to actor state.</returns>
    internal static FuturesBarDataStreamingStoppedEvent CreateFuturesBarDataStreamingStoppedEvent(this StopFuturesBarDataStreamingCommand e)
    => new()
    {
        CommandId = e.CommandId,
        Subject = new ActorSubject(ActorType.Event, FuturesBarDataStreamingStoppedEvent.Actor, FuturesBarDataStreamingStoppedEvent.Verb, e.EntityId.Format()),
        EntityId = e.EntityId,
        StoppedOn = e.OriginatedOn,
        StoppedBy = e.OriginatedBy
    };
}
