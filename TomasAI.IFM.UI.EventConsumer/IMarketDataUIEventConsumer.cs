using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IMarketDataUIEventConsumer
{
    ValueTask StartAsync(ICollection<IEvent> submittedEvents, Func<IEvent,ValueTask> eventAction);
    ValueTask StopAsync();
}


