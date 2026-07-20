using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Models
{
    public class MarketDataAnalyticsCommandModel : BaseModel<MarketDataAnalyticsCommandModel>
    {
        readonly IMarketDataAnalyticsCommandApi _commandApi;
        readonly IFuturesTradeSignalUIEventConsumer _futuresTradeSignalEventConsumer;
        readonly IFuturesRsiSignalUIEventConsumer _futuresRsiSignalEventConsumer;

        public MarketDataAnalyticsCommandModel(
            IMarketDataAnalyticsCommandApi commandApi,
            IFuturesTradeSignalUIEventConsumer futuresTradeSignalEventConsumer,
            IFuturesRsiSignalUIEventConsumer futuresRsiSignalEventConsumer)
        {
            _commandApi = commandApi;
            _futuresTradeSignalEventConsumer = futuresTradeSignalEventConsumer;
            _futuresRsiSignalEventConsumer = futuresRsiSignalEventConsumer;
        }

        /// <summary>
        /// start futures rsi signal service
        /// </summary>
        /// <param name="futuresRsiSignalId"></param>
        /// <returns></returns>
        public async Task StartFuturesRsiSignalServiceAsync(FuturesRsiSignalEntityId entityId)
            => await _commandApi.StartFuturesRsiSignalAsync(entityId);

        /// <summary>
        /// stop futures rsi signal service
        /// </summary>
        /// <param name="futuresRsiSignalId"></param>
        /// <returns></returns>
        public async Task StopFuturesRsiSignalServiceAsync(FuturesRsiSignalEntityId entityId)
            => await _commandApi.StopFuturesRsiSignalAsync(entityId);

        /// <summary>
        /// start futures trade signal LLM service
        /// </summary>
        /// <param name="futuresTradeSignalId"></param>
        /// <returns></returns>
        public async Task StartFuturesTradeSignalLLMServiceAsync(FuturesTradeSignalId futuresTradeSignalId)
            => await _commandApi.StartFuturesTradeSignalLLMAsync(futuresTradeSignalId);

        /// <summary>
        /// stop futures trade signal LLM service
        /// </summary>
        /// <param name="futuresTradeSignalId"></param>
        /// <returns></returns>
        public async Task StopFuturesTradeSignalLLMServiceAsync(FuturesTradeSignalId futuresTradeSignalId)
            => await _commandApi.StopFuturesTradeSignalLLMAsync(futuresTradeSignalId);

        /// <summary>
        /// start listening for futures trade signal updates
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesTradeSignalEventConsumerAsync(Guid siteId, Action<FuturesTradeSignalUpdatedCompleteEvent> listenerAction)
            => await _futuresTradeSignalEventConsumer.StartAsync(siteId, listenerAction);

        /// <summary>
        /// stop listening for futures trade signal updates
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopFuturesTradeSignalEventConsumerAsync(Guid siteId)
            => await _futuresTradeSignalEventConsumer.StopAsync(siteId);

        /// <summary>
        /// start listening for generated futures rsi signals
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesRsiSignalEventConsumerAsync(Guid siteId, Action<FuturesTdiSignalGeneratedCompleteEvent> listenerAction)
            => await _futuresRsiSignalEventConsumer.StartAsync(siteId, listenerAction);

        /// <summary>
        /// stop listening for generated futures rsi signals
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopFuturesRsiSignalEventConsumerAsync(Guid siteId)
            => await _futuresRsiSignalEventConsumer.StopAsync(siteId);
    }
}
