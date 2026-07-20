using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IOptionTradeSpreadBarDataUIEventConsumer
{
    ValueTask StartAsync(Action<OptionTradeSpreadBarDataInsertedCompleteEvent> eventAction);
    ValueTask StopAsync();
}


