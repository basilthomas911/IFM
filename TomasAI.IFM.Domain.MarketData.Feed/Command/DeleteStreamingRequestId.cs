using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class DeleteStreamingRequestId
{
    /// <summary>
    /// Handle a <see cref="DeleteStreamingRequestIdCommand"/> by building the corresponding
    /// <see cref="StreamingRequestIdDeletedEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The delete-streaming-request-id command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    public static bool Execute(this DeleteStreamingRequestIdCommand e, MarketDataFeedCommandState state)
        => state.Update(e.CreateStreamingRequestIdDeletedEvent(), e);

    /// <summary>
    /// Creates a <see cref="StreamingRequestIdDeletedEvent"/> from a <see cref="DeleteStreamingRequestIdCommand"/>.
    /// </summary>
    /// <param name="e">The originating delete-streaming-request-id command.</param>
    /// <returns>A new <see cref="StreamingRequestIdDeletedEvent"/> populated with the target feed
    /// identifier and audit metadata.</returns>
    internal static StreamingRequestIdDeletedEvent CreateStreamingRequestIdDeletedEvent(this DeleteStreamingRequestIdCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, StreamingRequestIdDeletedEvent.Actor, StreamingRequestIdDeletedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FeedId = e.FeedId,
            DeletedOn = e.OriginatedOn,
            DeletedBy = e.OriginatedBy
        };

}
