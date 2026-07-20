using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePlanUIEventConsumer
{
    ValueTask StartAsync(Action<TradePlanUpdatedEvent> eventAction);
    ValueTask StopAsync();
}


