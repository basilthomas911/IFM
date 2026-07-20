using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class ResetMarketDataFeed
{
    /// <summary>
    /// Handle a <see cref="ResetMarketDataFeedCommand"/> by building the corresponding
    /// <see cref="MarketDataFeedResetEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The reset command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    public static bool Execute(this ResetMarketDataFeedCommand e, MarketDataFeedCommandState state)
        => state.Update(e.CreateMarketDataFeedResetEvent(), e);

    /// <summary>
    /// Creates a <see cref="MarketDataFeedResetEvent"/> from a <see cref="ResetMarketDataFeedCommand"/>.
    /// </summary>
    /// <param name="e">The originating reset command.</param>
    /// <returns>A new <see cref="MarketDataFeedResetEvent"/> populated with the command's futures
    /// contracts, value date and audit metadata.</returns>
    internal static MarketDataFeedResetEvent CreateMarketDataFeedResetEvent(this ResetMarketDataFeedCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedResetEvent.Actor, MarketDataFeedResetEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FuturesContracts = e.FuturesContracts,
            ValueDate = e.ValueDate,
            ResetOn = e.OriginatedOn,
            ResetBy = e.OriginatedBy
        };

}
