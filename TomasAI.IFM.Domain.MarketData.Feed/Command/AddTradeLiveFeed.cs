using TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class AddTradeLiveFeed
{
    /// <summary>
    /// Handle an <see cref="AddTradeLiveFeedCommand"/> by building the corresponding
    /// <see cref="TradeLiveFeedAddedEvent"/> and updating the actor state.
    /// </summary>
    /// <param name="e">The add-trade-live-feed command to execute.</param>
    /// <param name="state">The current actor command state to update.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise <see langword="false"/>.</returns>
    public static bool Execute(this AddTradeLiveFeedCommand e, MarketDataFeedCommandState state)
        => e switch
        {
            _ when state.IsTradeLiveFeedOn => throw new AddTradeLiveFeedException($"Trade live feed is already on for: {e.OrderId}:{e.TradeId}"),
            _ => state.Update(e.CreateTradeLiveFeedAddedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="TradeLiveFeedAddedEvent"/> from an <see cref="AddTradeLiveFeedCommand"/>.
    /// </summary>
    /// <param name="e">The originating add-trade-live-feed command.</param>
    /// <returns>A new <see cref="TradeLiveFeedAddedEvent"/> with the feed state set to
    /// <see cref="TradeLiveFeedStateType.On"/> and a <see cref="MarketDataFeedId"/> derived from
    /// the current UTC date.</returns>
    internal static TradeLiveFeedAddedEvent CreateTradeLiveFeedAddedEvent(this AddTradeLiveFeedCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedAddedEvent.Actor, TradeLiveFeedAddedEvent.Verb, e.EntityId.Format()),
            EntityId = new TradeLiveFeedId(e.OrderId, e.TradeId, e.ValueDate),
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            TradeLiveFeedState = TradeLiveFeedStateType.On,
            AddedOn = e.OriginatedOn,
            AddedBy = e.OriginatedBy
        };
}
