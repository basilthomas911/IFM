using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePlanUIEventConsumer
{
    ValueTask StartAsync(Action<TradePlanUpdatedEvent> eventAction);
    ValueTask StopAsync();
}


