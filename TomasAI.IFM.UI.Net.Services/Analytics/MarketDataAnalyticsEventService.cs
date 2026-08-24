using System;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Analytics
{
    /// <summary>Provides the MarketDataAnalyticsEventService UI service boundary.</summary>
    public class MarketDataAnalyticsEventService : UiServiceBase<MarketDataAnalyticsEventService>
    {
        readonly IFuturesItiSignalUIEventConsumer _futuresItiSignalEventConsumer;
        readonly IFuturesTradeSignalUIEventConsumer _futuresTradeSignalEventConsumer;
 
        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public MarketDataAnalyticsEventService(
            IFuturesItiSignalUIEventConsumer futuresItiSignalEventConsumer,
            IFuturesTradeSignalUIEventConsumer futuresTradeSignalEventConsumer)
        {
            _futuresItiSignalEventConsumer = IsArgumentNull.Set(futuresItiSignalEventConsumer);
            _futuresTradeSignalEventConsumer = IsArgumentNull.Set(futuresTradeSignalEventConsumer);
        }

        /// <summary>
        /// start listening for futures iti signal events
        /// </summary>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesItiSignalEventListenersAsync(
            Guid siteId,
            Action<FuturesItiSignalUpdatedNotifyEvent> futuresItiSignalAction,
            Action<FuturesTradeSignalUpdatedNotifyEvent> futuresTradeSignalAction)
        {
            await _futuresItiSignalEventConsumer.StartAsync(siteId, futuresItiSignalAction);
            try
            {
                await _futuresTradeSignalEventConsumer.StartAsync(siteId, futuresTradeSignalAction);
            }
            catch
            {
                await _futuresItiSignalEventConsumer.StopAsync(siteId);
                throw;
            }
        }

        /// <summary>
        /// stop listening for  futures iti signal events
        /// </summary>
        public async Task StopFuturesItiSignalEventListenersAsync(Guid siteId)
        {
            try
            {
                await _futuresTradeSignalEventConsumer.StopAsync(siteId);
            }
            finally
            {
                await _futuresItiSignalEventConsumer.StopAsync(siteId);
            }
        }
        
    }
}
