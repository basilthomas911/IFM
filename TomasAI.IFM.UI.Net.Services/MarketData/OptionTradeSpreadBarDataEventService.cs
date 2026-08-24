using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.MarketData
{
    /// <summary>Provides the OptionTradeSpreadBarDataEventService UI service boundary.</summary>
    public class OptionTradeSpreadBarDataEventService : UiServiceBase<OptionTradeSpreadBarDataEventService>
    {
        readonly IOptionTradeSpreadBarDataUIEventConsumer _spreadBarDataEventConsumer;

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public OptionTradeSpreadBarDataEventService(IOptionTradeSpreadBarDataUIEventConsumer spreadBarDataEventConsumer)
        {
            _spreadBarDataEventConsumer = spreadBarDataEventConsumer ?? throw new ArgumentNullException(nameof(spreadBarDataEventConsumer));
        }

        /// <summary>
        /// start listening for option trade spread bar data inserted complete events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartOptionTradeSpreadBarDataListenerAsync(
            Func<OptionTradeSpreadBarDataInsertedCompleteEvent, ValueTask> listenerAction)
            => await _spreadBarDataEventConsumer.StartAsync(listenerAction);

        /// <summary>
        /// stop listening for  option trade spread bar data inserted complete events
        /// </summary>
        public async Task StopOptionTradeSpreadBarDataListenerAsync() => await _spreadBarDataEventConsumer.StopAsync();
        
    }
}
