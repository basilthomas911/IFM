using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePositionUIEventConsumer
{
    ValueTask StartAsync(Action<TradePositionUpdatedEvent> eventAction);
    ValueTask StopAsync();
}


