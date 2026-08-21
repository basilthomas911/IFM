using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IFuturesItiSignalUIEventConsumer
{
    ValueTask StartAsync(
        Guid siteId,
        Action<FuturesItiSignalUpdatedNotifyEvent> eventAction);

    ValueTask StopAsync(Guid siteId);
}


