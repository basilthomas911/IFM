using TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class RemoveTradeLiveFeed
{
    /// <summary>
    /// Handle a <see cref="RemoveTradeLiveFeedCommand"/> by building the corresponding
    /// <see cref="TradeLiveFeedRemovedEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The remove-trade-live-feed command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    public static bool Execute(this RemoveTradeLiveFeedCommand e, MarketDataFeedCommandState state)
        => e switch
        {
            _ when !state.IsTradeLiveFeedOn => throw new RemoveTradeLiveFeedException($"Trade live feed is already off for: {e.OrderId}:{e.TradeId}"),
            _ => state.Update(e.CreateTradeLiveFeedRemovedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="TradeLiveFeedRemovedEvent"/> from a <see cref="RemoveTradeLiveFeedCommand"/>.
    /// </summary>
    /// <param name="e">The originating remove-trade-live-feed command.</param>
    /// <returns>A new <see cref="TradeLiveFeedRemovedEvent"/> with the feed state set to
    /// <see cref="TradeLiveFeedStateType.Off"/> and a <see cref="MarketDataFeedId"/> derived from
    /// the current UTC date.</returns>
    internal static TradeLiveFeedRemovedEvent CreateTradeLiveFeedRemovedEvent(this RemoveTradeLiveFeedCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedRemovedEvent.Actor, TradeLiveFeedRemovedEvent.Verb, e.EntityId.Format()),
            EntityId = new TradeLiveFeedId(e.OrderId, e.TradeId, e.ValueDate),
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            TradeLiveFeedState = TradeLiveFeedStateType.Off,
            RemovedOn = e.OriginatedOn,
            RemovedBy = e.OriginatedBy
        };

}
