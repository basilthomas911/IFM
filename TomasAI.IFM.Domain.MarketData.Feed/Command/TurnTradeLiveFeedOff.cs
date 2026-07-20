using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class TurnTradeLiveFeedOff
{
    /// <summary>
    /// Executes the command to turn off the trade live feed if it is currently active.
    /// </summary>
    /// <remarks>This method checks the current state before attempting to turn off the trade live feed. If
    /// the feed is already off, an exception is thrown to prevent redundant operations.</remarks>
    /// <param name="e">The command that requests the trade live feed to be turned off.</param>
    /// <param name="state">The current state of the market data feed, indicating whether the trade live feed is active.</param>
    /// <returns>true if the trade live feed was successfully turned off; otherwise, false.</returns>
    /// <exception cref="TurnTradeLiveFeedOffException">Thrown if the trade live feed is already off for the specified order and trade identifiers.</exception>
    public static bool Execute(this TurnTradeLiveFeedOffCommand e, MarketDataFeedCommandState state)
        => e switch
        {
            _ when !state.IsTradeLiveFeedOn => throw new TurnTradeLiveFeedOffException($"Trade live feed is already off for: {e.OrderId}:{e.TradeId}"),
            _ => state.Update(e.CreateTradeLiveFeedTurnedOffEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="TradeLiveFeedTurnedOffEvent"/> from a <see cref="TurnTradeLiveFeedOffCommand"/>.
    /// </summary>
    /// <param name="e">The originating turn-off command.</param>
    /// <returns>A new <see cref="TradeLiveFeedTurnedOffEvent"/> populated with the command's order,
    /// trade identifiers and audit metadata.</returns>
    internal static TradeLiveFeedTurnedOffEvent CreateTradeLiveFeedTurnedOffEvent(this TurnTradeLiveFeedOffCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedTurnedOffEvent.Actor, TradeLiveFeedTurnedOffEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

}
