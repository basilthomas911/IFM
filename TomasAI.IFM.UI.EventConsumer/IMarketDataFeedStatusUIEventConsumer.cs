using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IMarketDataFeedStatusUIEventConsumer
{
    ValueTask StartAsync(Func<IEvent, ValueTask> eventAction);
    ValueTask StopAsync();
}
