using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models;

public class TradePositionFeedEventModel(ITradePositionUIEventConsumer tradePositionEventConsumer) 
    : BaseModel<TradePositionFeedEventModel>
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
