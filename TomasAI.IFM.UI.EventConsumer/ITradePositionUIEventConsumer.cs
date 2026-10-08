using TomasAI.IFM.Domain.Trade.Shared.Events;

using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePositionUIEventConsumer
{
    ValueTask StartAsync(Action<TradePositionUpdatedEvent> eventAction);
    /// <summary>Listens to current Iron Condor monitoring events.</summary>
    ValueTask StartIronCondorAsync(Func<IronCondorPositionChangedEvent, ValueTask> eventAction);
    ValueTask StartIronCondorAsync(Guid ownerId, Func<IronCondorPositionChangedEvent, ValueTask> eventAction);
    ValueTask StopMonitoringAsync(Guid ownerId);
    ValueTask StopAsync();
}


