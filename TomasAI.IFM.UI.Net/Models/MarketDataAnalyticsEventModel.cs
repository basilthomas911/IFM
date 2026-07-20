using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class MarketDataAnalyticsEventModel : BaseModel<MarketDataAnalyticsEventModel>
    {
        readonly IFuturesItiSignalUIEventConsumer _futuresItiSignalEventConsumer;
 
        public MarketDataAnalyticsEventModel(IFuturesItiSignalUIEventConsumer futuresItiSignalEventConsumer)
        {
            _futuresItiSignalEventConsumer = IsArgumentNull.Set(futuresItiSignalEventConsumer);
        }

        /// <summary>
        /// start listening for futures iti signal events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesItiSignalEventListenersAsync( 
            Action<FuturesItiSignalV2ReadModel> trendDirectionChangedAction,
            Action<FuturesItiSignalV2ReadModel> trendExtremeChangedAction,
            Action<FuturesTradeSignalV2ReadModel> futuresTradeSignalAction) 
            => await _futuresItiSignalEventConsumer.StartAsync(
                    trendDirectionChangedAction, 
                    trendExtremeChangedAction,
                    futuresTradeSignalAction);

        /// <summary>
        /// stop listening for  futures iti signal events
        /// </summary>
        public async Task StopFuturesItiSignalEventListenersAsync() 
            => await _futuresItiSignalEventConsumer.StopAsync();
        
    }
}
