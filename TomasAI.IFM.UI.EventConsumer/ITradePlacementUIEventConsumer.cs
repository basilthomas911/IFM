using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePlacementUIEventConsumer
{
    ValueTask StartAsync(Action<IEvent> eventAction);
    ValueTask StopAsync();
}


