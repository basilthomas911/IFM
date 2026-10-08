using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using System;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Trade
{
    /// <summary>Provides the TradePlanEventService UI service boundary.</summary>
    public class TradePlanEventService : UiServiceBase<TradePlanEventService>
    {
    /// <summary>Starts a plan or position listener owned by the selected trade view.</summary>
    public async Task StartIronCondorTradePlanListenerAsync(Guid ownerId, Func<IronCondorTradePlanUpdatedEvent, ValueTask> listenerAction)
        => await ExecuteValueTaskAsync(() => _tradePlanEventConsumer.StartIronCondorAsync(ownerId, listenerAction));

    /// <summary>Releases only the selected trade view's monitoring subscription.</summary>
    public async Task StopMonitoringAsync(Guid ownerId)
        => await ExecuteValueTaskAsync(() => _tradePlanEventConsumer.StopMonitoringAsync(ownerId));

        readonly ITradePlanUIEventConsumer _tradePlanEventConsumer;

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public TradePlanEventService(ITradePlanUIEventConsumer tradePlanEventConsumer)
        {
            _tradePlanEventConsumer = tradePlanEventConsumer ?? throw new ArgumentNullException(nameof(tradePlanEventConsumer));
        }

        /// <summary>
        /// start listening for trade plan updated events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartTradePlanListenerAsync(Func<TradePlanUpdatedEvent, ValueTask> listenerAction)
            => await ExecuteValueTaskAsync(() => _tradePlanEventConsumer.StartAsync(listenerAction));

        /// <summary>Starts the current Iron Condor plan monitoring listener.</summary>
        /// <param name="listenerAction">The read-only monitoring callback.</param>
        public async Task StartIronCondorTradePlanListenerAsync(Func<IronCondorTradePlanUpdatedEvent, ValueTask> listenerAction)
            => await ExecuteValueTaskAsync(() => _tradePlanEventConsumer.StartIronCondorAsync(listenerAction));

        /// <summary>
        /// stop listening for trade plan updated events
        /// </summary>
        public async Task StopTradePlanListenerAsync()
            => await ExecuteValueTaskAsync(_tradePlanEventConsumer.StopAsync);

    }
}
