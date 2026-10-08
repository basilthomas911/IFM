using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>Provides the TradePositionFeedEventService UI service boundary.</summary>
public class TradePositionFeedEventService(ITradePositionUIEventConsumer tradePositionEventConsumer)
    : UiServiceBase<TradePositionFeedEventService>
{
    /// <summary>Starts a plan or position listener owned by the selected trade view.</summary>
    public async Task StartIronCondorTradePositionListenerAsync(Guid ownerId, Func<IronCondorPositionChangedEvent, ValueTask> listenerAction)
        => await ExecuteValueTaskAsync(() => tradePositionEventConsumer.StartIronCondorAsync(ownerId, listenerAction));

    /// <summary>Releases only the selected trade view's monitoring subscription.</summary>
    public async Task StopMonitoringAsync(Guid ownerId)
        => await ExecuteValueTaskAsync(() => tradePositionEventConsumer.StopMonitoringAsync(ownerId));

    /// <summary>
    /// start listening for trade position updates
    /// </summary>
    /// <param name="listenerAction"></param>
    public async Task StartTradePositionListenerAsync(Action<TradePositionUpdatedEvent> listenerAction)
        => await ExecuteValueTaskAsync(() => tradePositionEventConsumer.StartAsync(listenerAction));

    /// <summary>Starts the current Iron Condor position monitoring listener.</summary>
    /// <param name="listenerAction">The read-only monitoring callback.</param>
    public async Task StartIronCondorTradePositionListenerAsync(Func<IronCondorPositionChangedEvent, ValueTask> listenerAction)
        => await ExecuteValueTaskAsync(() => tradePositionEventConsumer.StartIronCondorAsync(listenerAction));

    /// <summary>
    /// stop listening for trade position updates
    /// </summary>
    public async Task StopTradePositionListenerAsync()
        => await ExecuteValueTaskAsync(tradePositionEventConsumer.StopAsync);

}
