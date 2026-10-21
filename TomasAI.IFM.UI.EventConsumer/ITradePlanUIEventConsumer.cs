
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.UI.EventConsumer;

public interface ITradePlanUIEventConsumer
{
    /// <summary>Listens to current Iron Condor monitoring events.</summary>
    ValueTask StartIronCondorAsync(Func<IronCondorTradePlanUpdatedEvent, ValueTask> eventAction);
    ValueTask StartIronCondorAsync(Guid ownerId, Func<IronCondorTradePlanUpdatedEvent, ValueTask> eventAction);
    ValueTask StopMonitoringAsync(Guid ownerId);
    ValueTask StopAsync();
}


