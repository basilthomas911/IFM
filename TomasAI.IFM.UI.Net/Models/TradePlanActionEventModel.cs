using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models
{
    public class TradePlanActionEventModel : BaseModel<TradePlanActionEventModel>
    {
        readonly ITradePlanActionUIEventConsumer _tradePlanActionEventConsumer;

        public TradePlanActionEventModel(ITradePlanActionUIEventConsumer tradePlanSummaryEventConsumer)
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
