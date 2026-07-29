using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePlanActionUIEventConsumer
{
    ValueTask StartAsync(Action<TradePlanActionUpdatedEvent> eventAction);
    ValueTask StopAsync();
}


