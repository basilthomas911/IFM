using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IMarketDataFeedStatusUIEventConsumer
{
    ValueTask StartAsync(Func<IEvent, ValueTask> eventAction);
    ValueTask StopAsync();
    /// <summary>Starts a feed lifecycle listener belonging to one trade view.</summary>
    ValueTask StartMonitoringAsync(Guid ownerId, Func<IEvent, ValueTask> eventAction);
    /// <summary>Stops only the specified trade view listener.</summary>
    ValueTask StopMonitoringAsync(Guid ownerId);
}
