using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IMarketDataFeedResetUIEventConsumer
{
    ValueTask StartAsync(Action<MarketDataFeedResetStreamingEvent> eventAction);
    ValueTask StopAsync();
}


