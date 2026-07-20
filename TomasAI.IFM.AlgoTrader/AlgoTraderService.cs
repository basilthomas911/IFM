using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Service.AlgoTrader.Model;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Service.AlgoTrader.Model.LongIronCondor;
using TomasAI.IFM.Service.AlgoTrader.Model.ShortIronCondor;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using RestSharp.Validation;
using System.Diagnostics.Contracts;

namespace TomasAI.IFM.Service.AlgoTrader
{
    public class AlgoTraderService : IAlgoTraderService
    {
        static ActionType _lastActionType;
        readonly IMarketDataQueryApi _marketDataQueryApi;
        readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi;
        readonly IMarketDataAnalyticsQueryApi _marketDataAnalyticsQueryApi;
        readonly IOptionPricerQueryApi _optionPricerQueryApi;
        readonly ITradeQueryApi _tradeQueryApi;
        readonly ITradePlanQueryApi _tradePlanQueryApi;
        readonly ITradePlanCommandApi _tradePlanCommandApi;
        readonly IFundQueryApi _fundQueryApi;
        readonly IDataCacheService _dataCacheService;
        readonly IExceptionEventProducer _exceptionEventProducer;
        readonly IBlackboardService _blackboardService;
        readonly ILogger<AlgoTraderService> _logger;

        public AlgoTraderService(
            IMarketDataQueryApi marketDataQueryApi, 
            IMarketDataFeedQueryApi marketDataFeedQueryApi,
            IMarketDataAnalyticsQueryApi marketDataAnalyticsQueryApi,
            IOptionPricerQueryApi optionPricerQueryApi,
            ITradeQueryApi tradeQueryApi, 
            ITradePlanQueryApi tradePlanQueryApi, 
            ITradePlanCommandApi tradePlanCommandApi,
            IFundQueryApi fundQueryApi,
            IDataCacheService dataCacheService,
            IExceptionEventProducer exceptionEventProducer,
            IBlackboardService blackboardService,
            ILogger<AlgoTraderService> logger)//:base(eventServiceHandlerResolver, logger)
        {
            _marketDataQueryApi = IsArgumentNull.Set(marketDataQueryApi);
            _marketDataFeedQueryApi = IsArgumentNull.Set(marketDataFeedQueryApi);
            _marketDataAnalyticsQueryApi = IsArgumentNull.Set(marketDataAnalyticsQueryApi);
            _optionPricerQueryApi = IsArgumentNull.Set(optionPricerQueryApi);
            _tradeQueryApi = IsArgumentNull.Set(tradeQueryApi);
            _tradePlanQueryApi = IsArgumentNull.Set(tradePlanQueryApi);
            _tradePlanCommandApi = IsArgumentNull.Set(tradePlanCommandApi);
            _fundQueryApi = IsArgumentNull.Set(fundQueryApi);
            _dataCacheService = IsArgumentNull.Set(dataCacheService);
            _exceptionEventProducer = IsArgumentNull.Set(exceptionEventProducer);
            _blackboardService= IsArgumentNull.Set(blackboardService);
            _logger = IsArgumentNull.Set(logger);
            _logger.LogInformationEvent("AlgoTraderService", "service started successfully");
        }

        /// <summary>
        /// update trade plan by evaluating trade plan algorithm
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task UpdateTradePlanAsync(OptionTradeDistributionStatisticsUpdatedEvent e)
        {
            try
            {
                // get selected trade plan from trade type...
                var tradePlan = await CreateTradePlanAsync(e);
                if (tradePlan is not null)
                {
                    // build selected trade algorithm...
                    var tradeAlgo = BuildAlgorithm(tradePlan);
                    if (tradeAlgo is not null)
                    {
                        // execute trade algorithm...
                        var tradePlanUpdatedEvent = tradeAlgo.ExecuteAlgorithm();
                        SetStopLossLimit(tradePlan.OrderId, tradePlan.TradeId, tradePlanUpdatedEvent.TradePlan.StopLossLimit);
                        SetLastActionType(tradePlanUpdatedEvent.TradePlan.ActionType);

                        // send out trade plan updated event...
                        await _tradePlanCommandApi.UpdateTradePlanAsync(tradePlanUpdatedEvent.TradePlan);
                        return;
                     
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, "UpdateTradePlan failed");
                await _exceptionEventProducer.PostEventAsync(new EventServiceExceptionEvent
                {
                    CommandId = e.CommandId,
                    ErrorData = $"{ex}",
                    ErrorType = ErrorType.EventService,
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// create iron condor trade plan
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        async Task<TradePlan> CreateTradePlanAsync(OptionTradeDistributionStatisticsUpdatedEvent e)
        {
            var tradePlan = default(TradePlan);
            try
            {
                var srFuturesContract = await _marketDataQueryApi.GetCurrentTradedFuturesContractAsync();
                if (!srFuturesContract.Success)
                    return tradePlan;
                var futuresContract = srFuturesContract.Value;
                var srFuturesEodData = await _marketDataFeedQueryApi.GetFuturesEodDataAsync(futuresContract.ContractId, e.ValueDate);
                if (!srFuturesEodData.Success)
                    return tradePlan;
                var futuresEodData = srFuturesEodData.Value;
                var srOptionTrade = await _tradeQueryApi.GetOptionTradeAsync(e.OrderId, e.TradeId);
                if (!srOptionTrade.Success)
                    return tradePlan;
                var optionTrade = srOptionTrade.Value;
                var srTrades = await _tradeQueryApi.GetOptionTradesAsync(e.OrderId);
                if (!srTrades.Success)
                    return tradePlan;
                var optionTrades = new OptionTradeCollection(srTrades.Value);
                var srFuturesTradeSignal = await _marketDataAnalyticsQueryApi.GetFuturesTradeSignalAsync(futuresContract.ContractId, e.ValueDate);
                if (!srFuturesTradeSignal.Success)
                    return tradePlan;
                var futuresTradeSignal = srFuturesTradeSignal.Value;
                tradePlan = optionTrade.TradeType switch
                {
                    TradeType.ShortIronCondor => ShortIronCondorTradePlan.Create(e.ValueDate, optionTrades, futuresEodData, futuresTradeSignal, _blackboardService, DateTime.Now, e.UserName),
                    TradeType.LongIronCondor => LongIronCondorTradePlan.Create(e.ValueDate, optionTrades, futuresEodData, futuresTradeSignal, _blackboardService, DateTime.Now, e.UserName),
                    _ => throw new NotImplementedException($"CreateTradePlanAsync: Trade Plan for {optionTrade.TradeType} not implemented")
                };
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, "CreateTradePlanAsync Failed");
            }
            return await Task.FromResult(tradePlan);
        }

        ITradeAlgorithm BuildAlgorithm(TradePlan tradePlan)
            => tradePlan switch {
                ShortIronCondorAlgorithm shortIronCondorAlgo => shortIronCondorAlgo
                    .SetLossProbability(forwardLossRatio => GetLossProbabilityAsync(shortIronCondorAlgo, forwardLossRatio).Result)
                    .SetTradePrice(() => GetIronCondorTradePrice(shortIronCondorAlgo.TradeId, shortIronCondorAlgo.ValueDate))
                    .SetStopLossLimit(() => GetStopLossLimit(shortIronCondorAlgo.OrderId, shortIronCondorAlgo.TradeId))
                    .SetSignalProcessor(() => GetSignalProcessor<ShortIronCondorTradePlan>(shortIronCondorAlgo.OrderId, shortIronCondorAlgo.TradeId))
                    .SetFundBalance(() => GetFundBalance(shortIronCondorAlgo.OrderId))
                    .SetForwardDelta((valueDate, tradeType) => GetForwardDeltaAsync(valueDate, tradeType).Result)
                    .SetForwardLossLimitType( id => GetForwardLossLimitTypeAsync(id).Result),
                LongIronCondorAlgorithm longIronCondorAlgo => longIronCondorAlgo
                    .SetLossProbability(forwardLossRatio => GetLossProbabilityAsync(longIronCondorAlgo, forwardLossRatio).Result)
                    .SetTradePrice(() => GetIronCondorTradePrice(longIronCondorAlgo.TradeId, longIronCondorAlgo.ValueDate))
                    .SetStopLossLimit(() => GetStopLossLimit(longIronCondorAlgo.OrderId, longIronCondorAlgo.TradeId))
                    .SetSignalProcessor(() => GetSignalProcessor<LongIronCondorTradePlan>(longIronCondorAlgo.OrderId, longIronCondorAlgo.TradeId))
                    .SetFundBalance(() => GetFundBalance(longIronCondorAlgo.OrderId))
                    .SetForwardDelta((valueDate, tradeType) => GetForwardDeltaAsync(valueDate, tradeType).Result)
                    .SetForwardLossLimitType( id =>  GetForwardLossLimitTypeAsync(id).Result),
                _ => default
            };
       
        /// <summary>
        /// return loss probability view model
        /// </summary>
        /// <param name="forwardLossRatio"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        async Task<LossProbabilityViewModel> GetLossProbabilityAsync(TradePlan tradePlan, double forwardLossRatio)
        {
            var lossProbability = default(LossProbabilityViewModel);
            try
            {
                var valueDate = tradePlan.ValueDate;
                var forwardLossRatioMap = GetForwardLossRatioMap(valueDate);
                if (!forwardLossRatioMap.ContainsKey(valueDate))
                {
                    // get trade plan forward loss ratios for last 60 days...
                    var endDate = valueDate.AddDays(1).Date;
                    var startDate = endDate.AddDays(-60).Date;
                    var lossProbs = new List<TradePlanForwardLossRatioReadModel>((await _tradePlanQueryApi.GetTradePlanForwardLossRatiosAsync(startDate, endDate)).Value );
                    forwardLossRatioMap.Add(valueDate, lossProbs);
                }

                // create de-skewed distribution from trade plan forward loss ratios...
                forwardLossRatioMap[valueDate].Add(new TradePlanForwardLossRatioReadModel(forwardLossRatio));
                var forwardLossRatios = forwardLossRatioMap[valueDate].Select(e => Math.Sqrt(e.ForwardLossRatio)).OrderByDescending(o => o).ToList();

                // calculate median score from forward loss ratios...
                var mscore = GetMScore(forwardLossRatios);
                tradePlan.SetMScore(mscore);

                // return m-score from current trade plan forward loss ratio...
                var daysToExpiry = Convert.ToInt32((tradePlan.MaturityDate - tradePlan.ValueDate).TotalDays);
                var tradeType = tradePlan.PutSpreadAtRisk ? TradeType.PutCreditSpread : TradeType.CallCreditSpread;
                var spreadDistResult = await _optionPricerQueryApi.GetSpreadDistributionAsync(tradePlan.TradeId, tradeType, TradeStatus.IntraDay, tradePlan.ValueDate, daysToExpiry);
                if (spreadDistResult.Success && spreadDistResult.Value != null)
                {
                    lossProbability = new LossProbabilityViewModel
                    {
                        Value = spreadDistResult.Value.LossProbability,
                        Threshold = spreadDistResult.Value.LossThreshold,
                        ThresholdCount = spreadDistResult.Value.LossThresholdCount
                    };
                    tradePlan.SetLossProbability(lossProbability.Value);
                }

            }
            catch (Exception ex)
            {
                lossProbability = new LossProbabilityViewModel { Value = 0.01 };
                _logger.LogErrorEvent("AlgoTraderService", ex, "GetLossProbabilityAsync Failed");
            }
            return lossProbability;

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

        /// <summary>
        /// return trade price view model
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        TradePriceReadModel GetIronCondorTradePrice(int tradeId, DateTime valueDate)
        {
            try
            {
                var serviceResult = _tradeQueryApi.GetIronCondorTradePriceAsync(tradeId, valueDate).Result;
                return (serviceResult.Success) ? serviceResult.Value : new TradePriceReadModel(tradeId, valueDate, 0.0m, 0.0m);
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, $"GetIronCondorTradePrice Failed");
                return new TradePriceReadModel(tradeId, valueDate, 0.0m, 0.0m);
            }
        }

        TradePlanStopLossLimitReadModel GetStopLossLimit(int orderId, int tradeId)
        {
            try
            {
                var tradeKey = OptionTradeId.Create(orderId, tradeId);
                if (!_dataCacheService.Exists(DataCacheName.StopLossLimit, tradeKey))
                    _dataCacheService.Add(DataCacheName.StopLossLimit, tradeKey, new TradePlanStopLossLimitReadModel(0.0));
                return _dataCacheService.Get<OptionTradeId, TradePlanStopLossLimitReadModel>(DataCacheName.StopLossLimit, tradeKey);
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, "GetIronCondorTradePrice Failed");
                return new TradePlanStopLossLimitReadModel(0.0);
            }

        }

        decimal GetFundBalance(int orderId)
        {
            var fundBalance = new FundBalanceReadModel (Value: 0m);
            try
            {
                if (!_dataCacheService.Exists(DataCacheName.FundBalanceByOrderId, orderId))
                {
                    var serviceResult = _fundQueryApi.GetFundIdFromOrderIdAsync(orderId).Result;
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
            }
            catch(Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, "GetFundBalance Failed");
            }
            return fundBalance?.Value ?? 0m;
        }

        async Task<double> GetForwardDeltaAsync(DateTime valueDate, TradeType tradeType)
        {
            double forwardDelta = 0.0;
            try
            {
                var srRiskPositionType = await _marketDataFeedQueryApi.GetFuturesRiskPositionTypeAsync(valueDate, tradeType);
                if (srRiskPositionType.Success && srRiskPositionType.Value is not null)
                {
                    var riskPositionType = srRiskPositionType.Value.RiskPositionType;
                    var srForwardDelta = await _tradePlanQueryApi.GetIronCondorForwardDeltaAsync(valueDate, tradeType, riskPositionType);
                    if (srForwardDelta.Success && srForwardDelta.Value is not null)
                        forwardDelta = srForwardDelta.Value.Value;
                }
            }
            catch(Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, "GetForwardDeltaAsync Failed");
            }
            return forwardDelta;
        }

        /// <summary>
        /// return forward loss limit type
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        async Task<ForwardLossLimitType> GetForwardLossLimitTypeAsync(TradePlanForwardLossLimitId id)
        {
            var forwardLossLimitType = ForwardLossLimitType.LimitWarning;
            try
            {
                var sr = await _tradePlanQueryApi.GetTradePlanForwardLossLimitAsync(id);
                if(sr.Success && sr.Value is not null)
                    forwardLossLimitType = sr.Value?.LimitType ?? forwardLossLimitType;
            }
            catch(Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, "GetForwardLossLimitTypeAsync Failed");
            }
            return forwardLossLimitType;
        }

        /// <summary>
        /// return lates futures trade signal
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        async Task<FuturesTradeSignalViewModel> GetFuturesTradeSignalAsync(string contractId, DateTime valueDate)
        {
            var futuresTradeSignal = default(FuturesTradeSignalViewModel);
            try
            {
                var serviceResult = await _marketDataAnalyticsQueryApi.GetFuturesTradeSignalAsync(contractId, valueDate);
                if (serviceResult.Success && serviceResult.Value is not null)
                    futuresTradeSignal = serviceResult.Value;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent("AlgoTraderService", ex, $"GetFuturesTradeSignalAsync Failed");
            }
            return futuresTradeSignal;
        }

        SignalProcessor<TSignal> GetSignalProcessor<TSignal>(int orderId, int tradeId)
        {
            try
            {
                var tradeKey = OptionTradeId.Create(orderId, tradeId);
                if (!_dataCacheService.Exists(DataCacheName.SignalProcessor, tradeKey))
                    _dataCacheService.Add(DataCacheName.SignalProcessor, tradeKey, new SignalProcessor<TSignal>());
                return _dataCacheService.Get<OptionTradeId, SignalProcessor<TSignal>>(DataCacheName.SignalProcessor, tradeKey);
            }
            catch
            {
                return new SignalProcessor<TSignal>();
            }

        }

        Dictionary<DateTime, ICollection<TradePlanForwardLossRatioReadModel>> GetForwardLossRatioMap(DateTime valueDate)
        {
            if (!_dataCacheService.Exists(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}"))
                _dataCacheService.Add(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}", new Dictionary<DateTime, ICollection<TradePlanForwardLossRatioReadModel>>());
            return _dataCacheService.Get<string, Dictionary<DateTime, ICollection<TradePlanForwardLossRatioReadModel>>>(DataCacheName.ForwardLossRatioMap, $"{valueDate:yyyyMMdd}");
        }


        void SetStopLossLimit(int orderId, int tradeId, double stopLossLimit)
        {
            try
            {
                var tradeKey = OptionTradeId.Create(orderId, tradeId);
                if (_dataCacheService.Exists(DataCacheName.StopLossLimit, tradeKey))
                    _dataCacheService.Remove(DataCacheName.StopLossLimit, tradeKey);
                _dataCacheService.Add(DataCacheName.StopLossLimit, tradeKey, new TradePlanStopLossLimitReadModel(stopLossLimit));
            }
            catch { }
        }

        void SetLastActionType(ActionType lastActionType) => _lastActionType = lastActionType;

       

        
    }
}
