using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ICommandResponseUIEventConsumer
{
    ValueTask StartAsync(ICollection<IEvent> commandResponseEvents, Action<IEvent> eventAction);
    ValueTask StopAsync();
}


