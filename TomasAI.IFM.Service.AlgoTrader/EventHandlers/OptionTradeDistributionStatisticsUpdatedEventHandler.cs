using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Service.AlgoTrader.Model;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Service.AlgoTrader.EventHandlers
{
    /// <summary>
    /// option trade distribution statistics updated event handler
    /// </summary>
    public class OptionTradeDistributionStatisticsUpdatedEventHandler : BaseEventServiceHandler,
        IAsyncEventHandler<OptionTradeDistributionStatisticsUpdatedEvent, AlgoTraderService2>
    {
        readonly IMarketDataQueryApi _marketDataQueryApi;
        readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi;
        readonly IOptionPricerQueryApi _optionPricerQueryApi;
        readonly ITradeQueryApi _tradeQueryApi;
        readonly ITradePlanQueryApi _tradePlanQueryApi;
        readonly ITradePlanCommandApi _tradePlanCommandApi;
        readonly IFundQueryApi _fundQueryApi;
        readonly IDataCacheService _dataCacheService;
        readonly IExceptionEventProducer _exceptionEventProducer;
        readonly ILogger<OptionTradeDistributionStatisticsUpdatedEventHandler> _logger;

        /// <summary>
        /// OptionTradeDistributionStatisticsUpdatedEventHandler constructor
        /// </summary>
        /// <param name="marketDataQueryAp"></param>
        /// <param name="marketDataFeedQueryApi"></param>
        /// <param name="optionPricerQueryApi"></param>
        /// <param name="tradeQueryApi"></param>
        /// <param name="tradePlanQueryApi"></param>
        /// <param name="tradePlanCommandApi"></param>
        /// <param name="fundQueryApi"></param>
        /// <param name="dataCacheService"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public OptionTradeDistributionStatisticsUpdatedEventHandler(
            IMarketDataQueryApi marketDataQueryAp,
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            IOptionPricerQueryApi optionPricerQueryApi,
            ITradeQueryApi tradeQueryApi,
            ITradePlanQueryApi tradePlanQueryApi,
            ITradePlanCommandApi tradePlanCommandApi,
            IFundQueryApi fundQueryApi,
            IDataCacheService dataCacheService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<OptionTradeDistributionStatisticsUpdatedEventHandler> logger) : base(statusConsoleWriter)
        {
            _marketDataQueryApi = marketDataQueryAp;
            _marketDataFeedQueryApi = marketDataFeedQueryApi;
            _optionPricerQueryApi = optionPricerQueryApi;
            _tradeQueryApi = tradeQueryApi;
            _tradePlanQueryApi = tradePlanQueryApi;
            _tradePlanCommandApi = tradePlanCommandApi;
            _fundQueryApi = fundQueryApi;
            _dataCacheService = dataCacheService;
            _logger = logger;
        }

        public async Task ExecuteAsync(OptionTradeDistributionStatisticsUpdatedEvent e)
        {
            try
            {
                var srOptionTrades = await _tradeQueryApi.GetOptionTradesAsync(e.OrderId);
                if (!srOptionTrades.Success)
                    throw new InvalidOperationException($"No Option Trades Found for: {e.OrderId}");
                var optionTrades = new OptionTradeCollection(srOptionTrades.Value);
                var srFuturesEodData = await _marketDataFeedQueryApi.GetFuturesEodDataAsync(optionTrades.PrimaryTrade.UnderlyingContractId, e.ValueDate);
                if (!srFuturesEodData.Success)
                    throw new InvalidOperationException($"No Futures EOD Data Found for: {optionTrades.PrimaryTrade.UnderlyingContractId} - {e.ValueDate}");
                var futuresEodData = srFuturesEodData.Value;
                var forwardLossRatio = optionTrades.PrimaryTrade.TradePositions?.Get(optionTrades.PrimaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay).ForwardLossRatio ?? 0.0;
                switch (optionTrades.PrimaryTrade.TradeType)
                {
                    case TradeType.ShortIronCondor:
                    case TradeType.LongIronCondor:
                        await _tradePlanCommandApi.UpdateIronCondorTradePlanAsync(
                            valueDate: e.ValueDate,
                            optionTrades: optionTrades,
                            futuresEodData: futuresEodData,
                            mScore: await GetMScoreAsync(e.ValueDate, optionTrades.PrimaryTrade.TradeType, forwardLossRatio),
                            fundBalance: await GetFundBalanceAsync(e.OrderId));
                        break;
                    default:
                        throw new NotImplementedException($"No Trade Plan implemented for {optionTrades.PrimaryTrade.TradeType}");
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"AlgoTraderService.OptionTradeDistributionStatisticsUpdatedEvent: {ex.Message}");
            }
        }

        /// <summary>
        /// return mscore
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="tradeType"></param>
        /// <param name="forwardLossRatio"></param>
        /// <returns></returns>
        private async Task<double> GetMScoreAsync(DateTime valueDate, TradeType tradeType, double forwardLossRatio)
        {
            var mscore = default(double);
            try
            {
                var forwardLossRatioMap = GetForwardLossRatioMap(valueDate);
                if (!forwardLossRatioMap.ContainsKey(valueDate))
                {
                    // get trade plan forward loss ratios for last 60 days...
                    var endDate = valueDate.AddDays(1).Date;
                    var startDate = endDate.AddDays(-60).Date;
                    var lossProbs = new List<TradePlanForwardLossRatioReadModel>((await _tradePlanQueryApi.GetTradePlanForwardLossRatiosAsync(startDate, endDate)).Value);
                    forwardLossRatioMap.Add(valueDate, lossProbs);
                }

                // create de-skewed distribution from trade plan forward loss ratios...
                forwardLossRatioMap[valueDate].Add(new TradePlanForwardLossRatioReadModel(forwardLossRatio));
                var forwardLossRatios = forwardLossRatioMap[valueDate].Select(e => Math.Sqrt(e.ForwardLossRatio)).OrderByDescending(o => o).ToList();

                // calculate median score from forward loss ratios...
                mscore = GetMScore(forwardLossRatios);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"AlgoTraderService.GetMScoreAsync Failed");
            }
            return mscore;

            double GetMScore(ICollection<double> forwardLossRatios)
            {
                // get median...
                var median = forwardLossRatios.OrderByDescending(e => e).ToArray()[(int)(forwardLossRatios.Count / 2)];

                // get the absolute deviations from the median...
                var absDevsFromMedian = forwardLossRatios.Select(x => Math.Abs(x - median)).ToList();

                // get the median of the absolute values...
                var medianAbsDev = absDevsFromMedian.OrderByDescending(e => e).ToArray()[(int)(absDevsFromMedian.Count / 2)];

                // return median score...
                return Math.Sqrt(forwardLossRatio) / (median + (3.5 * medianAbsDev));
            }

        }

        private Dictionary<DateTime, ICollection<TradePlanForwardLossRatioReadModel>> GetForwardLossRatioMap(DateTime valueDate)
        {
            if (!_dataCacheService.Exists(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}"))
                _dataCacheService.Add(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}", new Dictionary<DateTime, ICollection<TradePlanForwardLossRatioReadModel>>());
            return _dataCacheService.Get<string, Dictionary<DateTime, ICollection<TradePlanForwardLossRatioReadModel>>>(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}");
        }

        private async Task<decimal> GetFundBalanceAsync(int orderId)
        {
            var fundBalance = new FundBalanceReadModel(Value: 0m);
            if (!_dataCacheService.Exists(DataCacheName.FundBalanceByOrderId, orderId))
            {
                var serviceResult = await _fundQueryApi.GetFundIdFromOrderIdAsync(orderId);
                if (serviceResult.Success)
                {
                    var fundId = serviceResult.Value.Value;
                    var serviceResult2 = _fundQueryApi.GetFundBalanceAsync(fundId).Result;
                    if (serviceResult2.Success)
                    {
                        fundBalance = serviceResult2.Value;
                        _dataCacheService.Add(DataCacheName.FundBalanceByOrderId, orderId, fundBalance);
                    }
                }
            }
            fundBalance = _dataCacheService.Get<int, FundBalanceReadModel>(DataCacheName.FundBalanceByOrderId, orderId);
            return fundBalance?.Value ?? 0m;
        }


    }


}
