using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Trade
{
    /// <summary>Provides the TradePlanActionEventService UI service boundary.</summary>
    public class TradePlanActionEventService : UiServiceBase<TradePlanActionEventService>
    {
        readonly ITradePlanActionUIEventConsumer _tradePlanActionEventConsumer;

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public TradePlanActionEventService(ITradePlanActionUIEventConsumer tradePlanSummaryEventConsumer)
        {
            _tradePlanActionEventConsumer = tradePlanSummaryEventConsumer ?? throw new ArgumentNullException(nameof(tradePlanSummaryEventConsumer));
        }

        /// <summary>
        /// start listening for trade plan action added complete events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartTradePlanActionListenerAsync( Action<TradePlanActionUpdatedEvent> listenerAction) => await _tradePlanActionEventConsumer.StartAsync(listenerAction);

        /// <summary>
        /// stop listening for trade plan action added complete events
        /// </summary>
        public async Task StopTradePlanActionListenerAsync() => await _tradePlanActionEventConsumer.StopAsync();
        
    }
}
