using TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class TurnTradeLiveFeedOn
{
    /// <summary>
    /// Executes the command to enable the trade live feed if it is not already active.
    /// </summary>
    /// <remarks>This method checks the current state before enabling the trade live feed to prevent duplicate
    /// activation attempts.</remarks>
    /// <param name="e">The command instance that specifies the details for turning on the trade live feed.</param>
    /// <param name="state">The current state of the market data feed, used to determine whether the trade live feed is already active.</param>
    /// <returns>true if the trade live feed was successfully enabled; otherwise, false.</returns>
    /// <exception cref="TurnTradeLiveFeedOnException">Thrown if the trade live feed is already active for the specified order and trade identifiers.</exception>
    public static bool Execute(this TurnTradeLiveFeedOnCommand e, MarketDataFeedCommandState state)
        => e switch
        {
            _ when state.IsTradeLiveFeedOn => throw new TurnTradeLiveFeedOnException($"Trade live feed is already on for: {e.OrderId}:{e.TradeId}"),
            _ => state.Update(e.CreateTradeLiveFeedTurnedOnEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="TradeLiveFeedTurnedOnEvent"/> from a <see cref="TurnTradeLiveFeedOnCommand"/>.
    /// </summary>
    /// <param name="e">The originating turn-on command.</param>
    /// <returns>A new <see cref="TradeLiveFeedTurnedOnEvent"/> populated with the command's order,
    /// trade identifiers and audit metadata.</returns>
    internal static TradeLiveFeedTurnedOnEvent CreateTradeLiveFeedTurnedOnEvent(this TurnTradeLiveFeedOnCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedTurnedOnEvent.Actor, TradeLiveFeedTurnedOnEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };
}
