using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;

namespace TomasAI.IFM.Models
{
    public class MarketDataAnalyticsQueryModel : BaseModel<MarketDataAnalyticsQueryModel>
    {
        readonly IMarketDataAnalyticsQueryApi _queryApi;

        /// <summary>
        /// market data analytics query model constructor
        /// </summary>
        /// <param name="queryApi"></param>
        public MarketDataAnalyticsQueryModel(IMarketDataAnalyticsQueryApi queryApi)
        {
            _queryApi = queryApi;
        }

        /// <summary>
        /// load futures trade signal
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public async Task GetFuturesTradeSignalAsync(string contractId, DateTime valueDate, Action<FuturesTradeSignalViewModel> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFuturesTradeSignalAsync(contractId, valueDate), onCompleted);

        /// <summary>
        /// load futures iti trend direction changed signal
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public async Task GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateTime valueDate, Action<FuturesItiSignalViewModel[]> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFuturesItiTrendDirectionChangedSignalsAsync(contractId, valueDate), onCompleted);

    }
}
