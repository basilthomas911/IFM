using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.State;

/// <summary>
/// Represents the event-sourced state of market data feed commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for market data feed operations by applying domain events
/// such as <see cref="MarketDataFeedStartedEvent"/>, <see cref="MarketDataFeedStoppedEvent"/>,
/// <see cref="MarketDataFeedResetEvent"/>, <see cref="TradeLiveFeedAddedEvent"/>, and
/// <see cref="TradeLiveFeedRemovedEvent"/>.</remarks>
public class MarketDataFeedCommandState
    : BaseEventSourceActorState<MarketDataFeedCommandState>, IEventSourceActorState<MarketDataFeedCommandState>
{
    TradeLiveFeedStateType tradeLiveFeedState = TradeLiveFeedStateType.Unknown;
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// apply state change event
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                MarketDataFeedStartedEvent e => On(e),
                MarketDataFeedStoppedEvent e => On(e),
                MarketDataFeedResetEvent e => On(e),
                TradeLiveFeedAddedEvent e => On(e),
                TradeLiveFeedRemovedEvent e => On(e),
                TradeLiveFeedHaltedEvent e => On(e),
                TradeLiveFeedTurnedOnEvent e => On(e),
                TradeLiveFeedTurnedOffEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    internal  bool IsTradeLiveFeedOn 
        => tradeLiveFeedState == TradeLiveFeedStateType.On;
    /// <summary>
    /// market data feed started
    /// </summary>
    /// <param name="e"></param>
    bool On(MarketDataFeedStartedEvent e) => true;

    /// <summary>
    /// market data feed stopped
    /// </summary>
    /// <param name="e"></param>
    bool On(MarketDataFeedStoppedEvent e) => true;

    /// <summary>
    /// market data feed reset
    /// </summary>
    /// <param name="e"></param>
    bool On(MarketDataFeedResetEvent e) => true;

    /// <summary>
    /// trade live feed added
    /// </summary>
    /// <param name="e"></param>
    bool On(TradeLiveFeedAddedEvent e) => true;

    /// <summary>
    /// trade live feed removed
    /// </summary>
    /// <param name="e"></param>
    bool On(TradeLiveFeedRemovedEvent e) => true;

    /// <summary>
    /// trade live feed halted
    /// </summary>
    /// <param name="e"></param>
    bool On(TradeLiveFeedHaltedEvent e)
    {
        tradeLiveFeedState = TradeLiveFeedStateType.Halted;
        return true;
    }

    /// <summary>
    /// Handles the activation of the trade live feed in response to a corresponding event.
    /// </summary>
    /// <remarks>This method updates the internal state to reflect that the trade live feed is now
    /// active.</remarks>
    /// <param name="e">The event data indicating that the trade live feed has been turned on.</param>
    /// <returns>Always returns <see langword="true"/>, indicating that the operation was successful.</returns>
    bool On(TradeLiveFeedTurnedOnEvent e)
    {
        tradeLiveFeedState = TradeLiveFeedStateType.On;
        return true;
    }

    /// <summary>
    /// Handles a trade live feed turned off event by updating the internal state to reflect that the feed is no longer
    /// active.
    /// </summary>
    /// <remarks>This method should be called when the trade live feed is turned off to ensure the internal
    /// state remains consistent with the feed's status.</remarks>
    /// <param name="e">The event data associated with the trade live feed being turned off.</param>
    /// <returns>Always returns <see langword="true"/>, indicating that the event was handled successfully.</returns>
    bool On(TradeLiveFeedTurnedOffEvent e)
    {
        tradeLiveFeedState = TradeLiveFeedStateType.Off;
        return true;
    }
}
