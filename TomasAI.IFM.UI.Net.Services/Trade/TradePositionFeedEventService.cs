using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>Provides the TradePositionFeedEventService UI service boundary.</summary>
public class TradePositionFeedEventService(ITradePositionUIEventConsumer tradePositionEventConsumer) 
    : UiServiceBase<TradePositionFeedEventService>
{
    /// <summary>
    /// start listening for trade position updates
    /// </summary>
    /// <param name="listenerAction"></param>
    public async Task StartTradePositionListenerAsync(Action<TradePositionUpdatedEvent> listenerAction) 
        => await ExecuteValueTaskAsync( () => tradePositionEventConsumer.StartAsync(listenerAction) );

    /// <summary>
    /// stop listening for trade position updates
    /// </summary>
    public async Task StopTradePositionListenerAsync() 
        => await ExecuteValueTaskAsync( tradePositionEventConsumer.StopAsync );

}
