using ServiceStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Models
{
    public class MarketDataFeedQueryModel : BaseModel<MarketDataFeedQueryModel>
    {
        readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi;

        public MarketDataFeedQueryModel(IMarketDataFeedQueryApi marketDataFeedQueryApi)
        {
            _marketDataFeedQueryApi = marketDataFeedQueryApi ?? throw new ArgumentNullException(nameof(marketDataFeedQueryApi));
        }

        /// <summary>
        /// return futures eod data by date range
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFuturesEodDataAsync(string contractId, DateTime startDate, DateTime endDate, Action<FuturesEodDataViewModel[]> onCompleted)
            => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, startDate, endDate), onCompleted);

        /// <summary>
        /// return futures risk position type
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="tradeType"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFuturesRiskPositionTypeAsync(DateTime valueDate, TradeType tradeType, Action<RiskPositionTypeReadModel> onCompleted)
            => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesRiskPositionTypeAsync(valueDate, tradeType), onCompleted);

        /// <summary>
        /// return futures eod data by value date
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFuturesEodDataAsync(string contractId, DateTime valueDate, Action<FuturesEodDataViewModel> onCompleted)
            => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, valueDate), onCompleted);

        /// <summary>
        /// return futures bar data by date range
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="symbol"></param>
        /// <param name="valueDate"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFuturesBarDataAsync(string contractId, string symbol, DateTime valueDate, DateTime startDate, DateTime endDate, Action<FuturesBarDataReadModel[]> onCompleted)
            => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesBarDataAsync(contractId, symbol, valueDate, startDate, endDate), onCompleted);

        /// <summary>
        /// return futures option spread data
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="maturityDate"></param>
        /// <param name="assetPrice"></param>
        /// <param name="riskFreeRate"></param>
        /// <param name="timeValue"></param>
        /// <param name="shortOptionContract"></param>
        /// <param name="longOptionContract"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFuturesOptionSpreadDataAsync(
            DateTime valueDate,
            DateTime maturityDate,
            double assetPrice,
            double riskFreeRate,
            double timeValue,
            FuturesOptionContractReadModel shortOptionContract,
            FuturesOptionContractReadModel longOptionContract,
            Action<FuturesOptionSpreadDataReadModel> onCompleted)
                => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesOptionSpreadDataAsync(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, shortOptionContract, longOptionContract), onCompleted);

        /// <summary>
        /// return streaming request id by stream id
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        public async Task<int> GetStreamingRequestIdAsync(Guid streamId)
        {
            var serviceResult = await _marketDataFeedQueryApi.GetStreamingRequestIdAsync(streamId);
            return serviceResult.Success && serviceResult.Value is not null
                ? serviceResult.Value.AsInteger
                : -1;
        }
    }
}
