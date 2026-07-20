using TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed ;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class HaltTradeLiveFeed
{
    /// <summary>
    /// Handle a <see cref="HaltTradeLiveFeedCommand"/> by building the corresponding
    /// <see cref="TradeLiveFeedHaltedEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The halt-trade-live-feed command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    /// <exception cref="HaltTradeLiveFeedException">Thrown if the trade live feed is not currently active for the specified order and trade identifiers.</exception>
    public static bool Execute(this HaltTradeLiveFeedCommand e, MarketDataFeedCommandState state)
        => e switch
        {
            _ when !state.IsTradeLiveFeedOn => throw new HaltTradeLiveFeedException($"Trade live feed is not on for: {e.OrderId}:{e.TradeId}"),
            _ => state.Update(e.CreateTradeLiveFeedHaltedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="TradeLiveFeedHaltedEvent"/> from a <see cref="HaltTradeLiveFeedCommand"/>.
    /// </summary>
    /// <param name="e">The originating halt-trade-live-feed command.</param>
    /// <returns>A new <see cref="TradeLiveFeedHaltedEvent"/> with the feed state set to
    /// <see cref="TradeLiveFeedStateType.Halted"/> and a <see cref="MarketDataFeedId"/> derived from
    /// the current UTC date.</returns>
    internal static TradeLiveFeedHaltedEvent CreateTradeLiveFeedHaltedEvent(this HaltTradeLiveFeedCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedHaltedEvent.Actor, TradeLiveFeedHaltedEvent.Verb, e.EntityId.Format()),
            EntityId = new MarketDataFeedId(DateOnly.FromDateTime(DateTime.UtcNow)),
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            TradeLiveFeedState = TradeLiveFeedStateType.Halted,
            RemovedOn = e.OriginatedOn,
            RemovedBy = e.OriginatedBy
        };

}
