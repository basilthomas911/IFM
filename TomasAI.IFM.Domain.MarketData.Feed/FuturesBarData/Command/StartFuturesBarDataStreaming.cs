using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;

public static class StartFuturesBarDataStreaming
{
    /// <summary>
    /// Handle a <see cref="StartFuturesBarDataStreamingCommand"/> by building the corresponding
    /// <see cref="FuturesBarDataStreamingStartedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StartFuturesBarDataStreamingCommand e, FuturesBarDataCommandState state)
        => state.Update(e.CreateFuturesBarDataStreamingStartedEvent(), e);

    /// <summary>
    /// Creates a <see cref="FuturesBarDataStreamingStartedEvent"/> from a
    /// <see cref="StartFuturesBarDataStreamingCommand"/>.
    /// </summary>
    /// <param name="e">The source start-stream command containing stream scope and origin metadata.</param>
    /// <returns>A fully-populated streaming-started event ready to be applied to actor state.</returns>
    internal static FuturesBarDataStreamingStartedEvent CreateFuturesBarDataStreamingStartedEvent(this StartFuturesBarDataStreamingCommand e)
    => new()
    {
        CommandId = e.CommandId,
        Subject = new ActorSubject(ActorType.Event, FuturesBarDataStreamingStartedEvent.Actor, FuturesBarDataStreamingStartedEvent.Verb, e.EntityId.Format()),
        EntityId = e.EntityId,
        Contracts = e.Contracts,
        ValueDate = e.ValueDate,
        StartedOn = e.OriginatedOn,
        StartedBy = e.OriginatedBy
    };

}
