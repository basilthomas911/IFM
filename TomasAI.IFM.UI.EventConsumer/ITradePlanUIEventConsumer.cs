using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePlanUIEventConsumer
{
    ValueTask StartAsync(Func<TradePlanUpdatedEvent, ValueTask> eventAction);
    ValueTask StopAsync();
}


