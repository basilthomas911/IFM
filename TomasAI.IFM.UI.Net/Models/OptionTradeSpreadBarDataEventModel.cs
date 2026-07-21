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
    public class OptionTradeSpreadBarDataEventModel : BaseModel<OptionTradeSpreadBarDataEventModel>
    {
        readonly IOptionTradeSpreadBarDataUIEventConsumer _spreadBarDataEventConsumer;

        public OptionTradeSpreadBarDataEventModel(IOptionTradeSpreadBarDataUIEventConsumer spreadBarDataEventConsumer)
        {
            _spreadBarDataEventConsumer = spreadBarDataEventConsumer ?? throw new ArgumentNullException(nameof(spreadBarDataEventConsumer));
        }

        /// <summary>
        /// start listening for option trade spread bar data inserted complete events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartOptionTradeSpreadBarDataListenerAsync( Action<OptionTradeSpreadBarDataInsertedCompleteEvent> listenerAction) => await _spreadBarDataEventConsumer.StartAsync(listenerAction);

        /// <summary>
        /// stop listening for  option trade spread bar data inserted complete events
        /// </summary>
        public async Task StopOptionTradeSpreadBarDataListenerAsync() => await _spreadBarDataEventConsumer.StopAsync();
        
    }
}
