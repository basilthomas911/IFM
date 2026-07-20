using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.LongIronCondor;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;

public class AlgorithmBuilder(
    IBlackboardService blackboardService,
    IOptionPricerQueryApi optionPricerQueryApi,
    ITradeQueryApi tradeQueryApi,
    ITradePlanQueryApi tradePlanQueryApi,
    IFundQueryApi fundQueryApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataQueryApi marketDataQueryApi
    ) : IAlgorithmBuilder
{
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly IOptionPricerQueryApi _optionPricerQueryApi = IsArgumentNull.Set(optionPricerQueryApi);
    readonly ITradeQueryApi _tradeQueryApi = IsArgumentNull.Set(tradeQueryApi);
    readonly ITradePlanQueryApi _tradePlan_query_api = IsArgumentNull.Set(tradePlanQueryApi);
    readonly IFundQueryApi _fundQueryApi = IsArgumentNull.Set(fundQueryApi);
    readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi = IsArgumentNull.Set(marketDataFeedQueryApi);
    readonly IMarketDataQueryApi _marketDataQueryApi = IsArgumentNull.Set(marketDataQueryApi);

    /// <summary>
    /// build long iron condor option algorithm
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="optionTrades"></param>
    /// <param name="futuresEodData"></param>
    /// <param name="futuresTradeSignal"></param>
    /// <returns></returns>
    public LongIronCondorAlgorithm BuildLongIronCondorAlgorithm(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        var longIronCondorAlgo = new LongIronCondorAlgorithm(valueDate, optionTrades, futuresEodData, futuresTradeSignal, _blackboardService);
        var optionTradeId = new OptionTradeEntityId(longIronCondorAlgo.OrderId, longIronCondorAlgo.TradeId);
        longIronCondorAlgo
           .SetLossProbability(forwardLossRatio => GetLossProbability(longIronCondorAlgo, forwardLossRatio))
           .SetTradePrice(() => GetIronCondorTradePrice(longIronCondorAlgo.TradeId, longIronCondorAlgo.ValueDate))
           .SetStopLossLimit(() => GetStopLossLimit(optionTradeId))
           .SetSignalProcessor(() => GetSignalProcessor<LongIronCondorTradePlan>(optionTradeId))
           .SetFundBalance(() => GetFundBalanceByOrderId(longIronCondorAlgo.OrderId))
           .SetForwardDelta(GetForwardDelta)
           .SetForwardLossLimitType(GetForwardLossLimitType);
        return longIronCondorAlgo;
    }

    /// <summary>
    /// build short iron condor option algorithm
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="optionTrades"></param>
    /// <param name="futuresEodData"></param>
    /// <param name="futuresTradeSignal"></param>
    /// <returns></returns>
    public ShortIronCondorAlgorithm BuildShortIronCondorAlgorithm(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        var shortIronCondorAlgo = new ShortIronCondorAlgorithm(valueDate, optionTrades, futuresEodData, futuresTradeSignal, _blackboardService);
        var optionTradeId = new OptionTradeEntityId(shortIronCondorAlgo.OrderId, shortIronCondorAlgo.TradeId);
        shortIronCondorAlgo
            .SetLossProbability(forwardLossRatio => GetLossProbability(shortIronCondorAlgo, forwardLossRatio))
            .SetTradePrice(() => GetIronCondorTradePrice(shortIronCondorAlgo.TradeId, shortIronCondorAlgo.ValueDate))
            .SetStopLossLimit(() => GetStopLossLimit(optionTradeId))
            .SetSignalProcessor(() => GetSignalProcessor<ShortIronCondorTradePlan>(optionTradeId))
            .SetFundBalance(() => GetFundBalanceByOrderId(shortIronCondorAlgo.OrderId))
            .SetForwardDelta(GetForwardDelta)
           .SetForwardLossLimitType(GetForwardLossLimitType);
        return shortIronCondorAlgo;
    }

    /// <summary>
    /// return loss probability view model
    /// </summary>
    /// <param name="tradePlan"></param>
    /// <param name="forwardLossRatio"></param>
    /// <returns></returns>
    LossProbabilityDataModel GetLossProbability(TradePlan tradePlan, double forwardLossRatio)
    {
        var lossProbability = new LossProbabilityDataModel(Value: 0.01, Threshold: 0m, ThresholdCount: 0);
        try
        {
            var valueDate = tradePlan.ValueDate;
            var forwardLossRatioMap = GetForwardLossRatioMap(valueDate);
            if (!forwardLossRatioMap.TryGetValue(valueDate, out ICollection<TradePlanForwardLossRatioReadModel>? value))
            {
                // get trade plan forward loss ratios for last 60 days...
                var endDate = valueDate.AddDays(1);
                var startDate = endDate.AddDays(-60);
                var lossProbs = GetTradePlanForwardLossRatios(startDate, endDate);
                value = lossProbs;
                forwardLossRatioMap.Add(valueDate, value);
            }

            value.Add(new TradePlanForwardLossRatioReadModel(forwardLossRatio));
            var forwardLossRatios = value.Select(e => Math.Sqrt(e.ForwardLossRatio)).OrderByDescending(o => o).ToList();

            // calculate median score from forward loss ratios...
            var mscore = GetMScore(forwardLossRatios);
            tradePlan.SetMScore(mscore);

            // return m-score from current trade plan forward loss ratio...
            var daysToExpiry = Convert.ToInt32(tradePlan.MaturityDate.DayNumber - tradePlan.ValueDate.DayNumber);
            var tradeType = tradePlan.PutSpreadAtRisk ? TradeType.PutCreditSpread : TradeType.CallCreditSpread;
            var spreadDistribution = GetSpreadDistribution(tradePlan.TradeId, tradeType, TradeStatus.IntraDay, tradePlan.ValueDate, daysToExpiry);
            if (spreadDistribution is not  null)
            {
                lossProbability = new LossProbabilityDataModel(
                    Value: spreadDistribution.LossProbability,
                    Threshold: spreadDistribution.LossThreshold,
                    ThresholdCount: spreadDistribution.LossThresholdCount
                );
                tradePlan.SetLossProbability(lossProbability.Value);
            }

        }
        catch (Exception)
        {
            lossProbability = new LossProbabilityDataModel ( Value: 0.01, Threshold: 0m, ThresholdCount: 0 );
        }
        return lossProbability;

        List<TradePlanForwardLossRatioReadModel>  GetTradePlanForwardLossRatios(DateOnly startDate, DateOnly endDate)
        {
            var forwardLossRatios = new List<TradePlanForwardLossRatioReadModel>();
            var serviceResult = _tradePlan_query_api.GetIronCondorTradePlanForwardLossRatiosAsync(startDate, endDate).Result;
            if (serviceResult.Success && serviceResult.Value != null)
                forwardLossRatios.AddRange(serviceResult.Value);
            return forwardLossRatios;
        }

        SpreadDistributionReadModel? GetSpreadDistribution(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
        {
            SpreadDistributionReadModel? spreadDistribution = default;
            var serviceResult = _optionPricerQueryApi.GetSpreadDistributionAsync(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry).Result;
            if (serviceResult.Success && serviceResult.Value != null)
                spreadDistribution = serviceResult.Value;
            return spreadDistribution;
        }

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

        Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>> GetForwardLossRatioMap(DateOnly valueDate)
        {
            var forwardLossRatioMap = _blackboardService.ForwardLossRatioMap.Get(valueDate);
            if (forwardLossRatioMap is null)
                _blackboardService.ForwardLossRatioMap.Set(valueDate, new Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>());
            return _blackboardService.ForwardLossRatioMap.Get(valueDate) ?? new Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>();
        }
    }

    /// <summary>
    /// return trade price view model
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    TradePriceReadModel GetIronCondorTradePrice(int tradeId, DateOnly valueDate)
    {
        try
        {
            var serviceResult = _tradeQueryApi.GetIronCondorTradePriceAsync(tradeId, valueDate).Result;
            return (serviceResult.Success && serviceResult.Value is not null)
                ? serviceResult.Value
                : new TradePriceReadModel(tradeId, valueDate, 0.0m, 0.0m);
        }
        catch 
        {
            return new TradePriceReadModel(tradeId, valueDate, 0.0m, 0.0m);
        }
    }

    /// <summary>
    /// return stop loss limit
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    TradePlanStopLossLimitReadModel GetStopLossLimit(OptionTradeEntityId optionTradeId)
    {
        try
        {
            var stopLossLimit = _blackboardService.StopLossLimit.Get(optionTradeId);
            if (stopLossLimit is null)
            {
                _blackboardService.StopLossLimit.Set(optionTradeId, new TradePlanStopLossLimitReadModel(0.0));
                stopLossLimit = _blackboardService.StopLossLimit.Get(optionTradeId);
            }
            return stopLossLimit ?? new TradePlanStopLossLimitReadModel(0.0);
        }
        catch
        {
            return new TradePlanStopLossLimitReadModel(0.0);
        }

    }

    /// <summary>
    /// return signal processor
    /// </summary>
    /// <typeparam name="TSignal"></typeparam>
    /// <param name="optionTradeId"></param>
    /// <returns></returns>
    SignalProcessor<TSignal> GetSignalProcessor<TSignal>(OptionTradeEntityId optionTradeId)
    {
        try
        {
            var signalProcessor = _blackboardService.SignalProcessor.Get<TSignal>(optionTradeId);
            if (signalProcessor is null)
            {
                _blackboardService.SignalProcessor.Set<TSignal>(optionTradeId, new SignalProcessor<TSignal>());
                signalProcessor = _blackboardService.SignalProcessor.Get<TSignal>(optionTradeId);
            }
               return signalProcessor ?? new SignalProcessor<TSignal>();
        }
        catch
        {
            return new SignalProcessor<TSignal>();
        }
    }

    /// <summary>
    /// return fund balance
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    decimal GetFundBalanceByOrderId(int orderId)
    {
        var fundBalance = new FundBalanceReadModel(0m);
        try
        {
            fundBalance = _blackboardService.FundBalance.Get(orderId);
            if (fundBalance is null)
            {
                var fundId = GetFundIdFromOrderId(orderId);
                if (fundId > 0)
                {
                    var fundBalanceByFundId = GetFundBalance(fundId);
                    if (fundBalanceByFundId != 0m)
                    {
                        fundBalance = new FundBalanceReadModel(fundBalanceByFundId);
                        _blackboardService  .FundBalance.Set( orderId, fundBalance);
                    }
                }
            }
        }
        catch 
        {
            fundBalance = default;
        }
        return fundBalance?.Value ?? 0m;

        int GetFundIdFromOrderId(int orderId)
        {
            var serviceResult = _fundQueryApi.GetFundIdFromOrderIdAsync(orderId).Result;
            return serviceResult.Success
                ? (serviceResult.Value?.Value ?? 0) : 0;
        }

        decimal GetFundBalance(int fundId)
        {
            var serviceResult = _fundQueryApi.GetFundBalanceAsync(fundId).Result;
            return serviceResult.Success
                ? (serviceResult.Value?.Value ?? 0m) : 0m;
        }

    }

    /// <summary>
    /// return forward delta
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    double GetForwardDelta(DateOnly valueDate, TradeType tradeType)
    {
        double forwardDelta = 0.0;
        try
        {
            var riskPositionTypeVM =GetFuturesRiskPositionType(valueDate, tradeType);
            if (riskPositionTypeVM is not null)
            {
                var riskPositionType = riskPositionTypeVM.RiskPositionType;
                var forwardDeltaVM = GetIronCondorForwardDelta(valueDate, tradeType, riskPositionType);
                if (forwardDeltaVM is not null)
                    forwardDelta = forwardDeltaVM.ForwardDeltaValue;
            }
        }
        catch 
        {
            forwardDelta = 0.0;
        }
        return forwardDelta;

        RiskPositionTypeReadModel? GetFuturesRiskPositionType(DateOnly valueDate, TradeType tradeType)
        {
            var serviceResult = _marketDataFeedQueryApi.GetFuturesRiskPositionTypeAsync(valueDate, tradeType).Result;
            return serviceResult.Success && serviceResult.Value is not null
                ? serviceResult.Value
                : default;
        }

        IronCondorForwardDeltaDataModel? GetIronCondorForwardDelta(DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
        {
            FuturesContractV2ReadModel[] futuresContracts = default!;
            var serviceResult = _marketDataQueryApi.GetCurrentlyTradedFuturesContractsAsync("ES").Result;
            if (serviceResult.Success && serviceResult.Value is not null)
            {
                futuresContracts = serviceResult.Value;
                if (futuresContracts is not null)
                {
                    var vixContractId = futuresContracts?.FirstOrDefault(x => x.Symbol == "VX" && x.CurrentlyTraded)?.ContractId;
                    if (vixContractId is not null)
                    {
                        var serviceResult2 = _tradePlan_query_api.GetIronCondorForwardDeltaAsync(vixContractId, valueDate, tradeType, riskPositionType).Result;
                        if (serviceResult2.Success && serviceResult2.Value is not null)
                            return serviceResult2.Value;
                    }
                }
            }
            return default;    
        }

    }

    /// <summary>
    /// return forward loss limit type
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    ForwardLossLimitType GetForwardLossLimitType(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        var forwardLossLimitType = ForwardLossLimitType.LimitWarning;
        try
        {
            var serviceResult = _tradePlan_query_api.GetForwardLossLimitTypeAsync(orderId, tradeId, valueDate, tradeType).Result;
            if (serviceResult.Success)
                forwardLossLimitType = serviceResult.Value?.LimitType ?? forwardLossLimitType;
        }
        catch { }
        return forwardLossLimitType;
    }
}
