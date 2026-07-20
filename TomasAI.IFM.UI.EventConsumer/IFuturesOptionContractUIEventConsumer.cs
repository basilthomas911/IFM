using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IFuturesOptionContractUIEventConsumer
{
    ValueTask StartAsync(Func<IEvent,ValueTask> eventAction);
    ValueTask StopAsync();
}


