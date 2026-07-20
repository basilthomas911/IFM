using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IMarketDataFeedResetUIEventConsumer
{
    ValueTask StartAsync(Action<MarketDataFeedResetStreamingEvent> eventAction);
    ValueTask StopAsync();
}


