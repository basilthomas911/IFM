using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

public static class StopMarketDataFeed
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool Execute(this StopMarketDataFeedCommand e, MarketDataFeedCommandState state)
        => state.Update(e.CreateMarketDataFeedStoppedEvent(), e);

    /// <summary>
    /// Creates a <see cref="MarketDataFeedStoppedEvent"/> from a <see cref="StopMarketDataFeedCommand"/>.
    /// </summary>
    /// <param name="e">The originating stop command.</param>
    /// <returns>A new <see cref="MarketDataFeedStoppedEvent"/> populated with the command's value
    /// date and audit metadata.</returns>
    internal static MarketDataFeedStoppedEvent CreateMarketDataFeedStoppedEvent(this StopMarketDataFeedCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedStoppedEvent.Actor, MarketDataFeedStoppedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            ValueDate = e.ValueDate,
            StoppedOn = e.OriginatedOn,
            StoppedBy = e.OriginatedBy
        };
}
