using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class StartMarketDataFeed
{
    /// <summary>
    /// Handle a <see cref="StartMarketDataFeedCommand"/> by building the corresponding
    /// <see cref="MarketDataFeedStartedEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The start command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    public static bool Execute(this StartMarketDataFeedCommand e, MarketDataFeedCommandState state)
        => state.Update(e.CreateMarketDataFeedStartedEvent(), e);

    /// <summary>
    /// Creates a <see cref="MarketDataFeedStartedEvent"/> from a <see cref="StartMarketDataFeedCommand"/>.
    /// </summary>
    /// <param name="e">The originating start command.</param>
    /// <returns>A new <see cref="MarketDataFeedStartedEvent"/> populated with the command's futures
    /// contracts, value date, reset-stream flag and audit metadata.</returns>
    internal static MarketDataFeedStartedEvent CreateMarketDataFeedStartedEvent(this StartMarketDataFeedCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedStartedEvent.Actor, MarketDataFeedStartedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FuturesContracts = e.FuturesContracts,
            ValueDate = e.ValueDate,
            ResetStream = e.ResetStream,
            StartedOn = e.OriginatedOn,
            StartedBy = e.OriginatedBy
        };
}
