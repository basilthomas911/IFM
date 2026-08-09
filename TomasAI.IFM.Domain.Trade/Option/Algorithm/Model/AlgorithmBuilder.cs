using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.LongIronCondor;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;

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
    public async ValueTask<LongIronCondorAlgorithm> BuildLongIronCondorAlgorithmAsync(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        var longIronCondorAlgo = new LongIronCondorAlgorithm(valueDate, optionTrades, futuresEodData, futuresTradeSignal, _blackboardService);
        var optionTradeId = new OptionTradeEntityId(longIronCondorAlgo.OrderId, longIronCondorAlgo.TradeId);
        var lossProbability = await GetLossProbabilityAsync(longIronCondorAlgo, longIronCondorAlgo.ForwardLossRatio).ConfigureAwait(false);
        var tradePrice = await GetIronCondorTradePriceAsync(longIronCondorAlgo.TradeId, longIronCondorAlgo.ValueDate).ConfigureAwait(false);
        var fundBalance = await GetFundBalanceByOrderIdAsync(longIronCondorAlgo.OrderId).ConfigureAwait(false);
        var forwardDelta = await GetForwardDeltaAsync(longIronCondorAlgo.ValueDate, longIronCondorAlgo.TradeType).ConfigureAwait(false);
        var forwardLossLimitType = await GetForwardLossLimitTypeAsync(longIronCondorAlgo.OrderId, longIronCondorAlgo.TradeId, longIronCondorAlgo.TradeType, longIronCondorAlgo.ValueDate).ConfigureAwait(false);
        longIronCondorAlgo
           .SetLossProbability(_ => lossProbability)
           .SetTradePrice(() => tradePrice)
           .SetStopLossLimit(() => GetStopLossLimit(optionTradeId))
           .SetSignalProcessor(() => GetSignalProcessor<LongIronCondorTradePlan>(optionTradeId))
           .SetFundBalance(() => fundBalance)
           .SetForwardDelta((_, _) => forwardDelta)
           .SetForwardLossLimitType((_, _, _, _) => forwardLossLimitType);
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
    public async ValueTask<ShortIronCondorAlgorithm> BuildShortIronCondorAlgorithmAsync(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        var shortIronCondorAlgo = new ShortIronCondorAlgorithm(valueDate, optionTrades, futuresEodData, futuresTradeSignal, _blackboardService);
        var optionTradeId = new OptionTradeEntityId(shortIronCondorAlgo.OrderId, shortIronCondorAlgo.TradeId);
        var lossProbability = await GetLossProbabilityAsync(shortIronCondorAlgo, shortIronCondorAlgo.ForwardLossRatio).ConfigureAwait(false);
        var tradePrice = await GetIronCondorTradePriceAsync(shortIronCondorAlgo.TradeId, shortIronCondorAlgo.ValueDate).ConfigureAwait(false);
        var fundBalance = await GetFundBalanceByOrderIdAsync(shortIronCondorAlgo.OrderId).ConfigureAwait(false);
        var forwardDelta = await GetForwardDeltaAsync(shortIronCondorAlgo.ValueDate, shortIronCondorAlgo.TradeType).ConfigureAwait(false);
        var forwardLossLimitType = await GetForwardLossLimitTypeAsync(shortIronCondorAlgo.OrderId, shortIronCondorAlgo.TradeId, shortIronCondorAlgo.TradeType, shortIronCondorAlgo.ValueDate).ConfigureAwait(false);
        shortIronCondorAlgo
            .SetLossProbability(_ => lossProbability)
            .SetTradePrice(() => tradePrice)
            .SetStopLossLimit(() => GetStopLossLimit(optionTradeId))
            .SetSignalProcessor(() => GetSignalProcessor<ShortIronCondorTradePlan>(optionTradeId))
            .SetFundBalance(() => fundBalance)
            .SetForwardDelta((_, _) => forwardDelta)
           .SetForwardLossLimitType((_, _, _, _) => forwardLossLimitType);
        return shortIronCondorAlgo;
    }

    /// <summary>
    /// return loss probability view model
    /// </summary>
    /// <param name="tradePlan"></param>
    /// <param name="forwardLossRatio"></param>
    /// <returns></returns>
    async Task<LossProbabilityDataModel> GetLossProbabilityAsync(TradePlan tradePlan, double forwardLossRatio)
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
                var lossProbs = await GetTradePlanForwardLossRatiosAsync(startDate, endDate).ConfigureAwait(false);
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
            var spreadDistribution = await GetSpreadDistributionAsync(tradePlan.TradeId, tradeType, TradeStatus.IntraDay, tradePlan.ValueDate, daysToExpiry).ConfigureAwait(false);
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

        async Task<List<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate)
        {
            var forwardLossRatios = new List<TradePlanForwardLossRatioReadModel>();
            var serviceResult = await _tradePlan_query_api.GetIronCondorTradePlanForwardLossRatiosAsync(startDate, endDate).ConfigureAwait(false);
            if (serviceResult.Success && serviceResult.Value != null)
                forwardLossRatios.AddRange(serviceResult.Value);
            return forwardLossRatios;
        }

        async Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
        {
            SpreadDistributionReadModel? spreadDistribution = default;
            var serviceResult = await _optionPricerQueryApi.GetSpreadDistributionAsync(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry).ConfigureAwait(false);
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
            var forwardLossRatioMap = _blackboardService.Trade.ForwardLossRatioMap.Get(valueDate);
            if (forwardLossRatioMap is null)
                _blackboardService.Trade.ForwardLossRatioMap.Set(valueDate, new Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>());
            return _blackboardService.Trade.ForwardLossRatioMap.Get(valueDate) ?? new Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>();
        }
    }

    /// <summary>
    /// return trade price view model
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    async Task<TradePriceReadModel> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate)
    {
        try
        {
            var serviceResult = await _tradeQueryApi.GetIronCondorTradePriceAsync(tradeId, valueDate).ConfigureAwait(false);
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
            var stopLossLimit = _blackboardService.Trade.StopLossLimit.Get(optionTradeId);
            if (stopLossLimit is null)
            {
                _blackboardService.Trade.StopLossLimit.Set(optionTradeId, new TradePlanStopLossLimitReadModel(0.0));
                stopLossLimit = _blackboardService.Trade.StopLossLimit.Get(optionTradeId);
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
            var signalProcessor = _blackboardService.Trade.SignalProcessor.Get<TSignal>(optionTradeId);
            if (signalProcessor is null)
            {
                _blackboardService.Trade.SignalProcessor.Set<TSignal>(optionTradeId, new SignalProcessor<TSignal>());
                signalProcessor = _blackboardService.Trade.SignalProcessor.Get<TSignal>(optionTradeId);
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
    async Task<decimal> GetFundBalanceByOrderIdAsync(int orderId)
    {
        var fundBalance = new FundBalanceReadModel(0m);
        try
        {
            fundBalance = _blackboardService.Fund.FundBalance.Get(orderId);
            if (fundBalance is null)
            {
                var fundId = await GetFundIdFromOrderIdAsync(orderId).ConfigureAwait(false);
                if (fundId > 0)
                {
                    var fundBalanceByFundId = await GetFundBalanceAsync(fundId).ConfigureAwait(false);
                    if (fundBalanceByFundId != 0m)
                    {
                        fundBalance = new FundBalanceReadModel(fundBalanceByFundId);
                        _blackboardService.Fund.FundBalance.Set(orderId, fundBalance);
                    }
                }
            }
        }
        catch 
        {
            fundBalance = default;
        }
        return fundBalance?.Value ?? 0m;

        async Task<int> GetFundIdFromOrderIdAsync(int orderId)
        {
            var serviceResult = await _fundQueryApi.GetFundIdFromOrderIdAsync(orderId).ConfigureAwait(false);
            return serviceResult.Success
                ? (serviceResult.Value?.Value ?? 0) : 0;
        }

        async Task<decimal> GetFundBalanceAsync(int fundId)
        {
            var serviceResult = await _fundQueryApi.GetFundBalanceAsync(fundId).ConfigureAwait(false);
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
    async Task<double> GetForwardDeltaAsync(DateOnly valueDate, TradeType tradeType)
    {
        double forwardDelta = 0.0;
        try
        {
            var riskPositionTypeVM = await GetFuturesRiskPositionTypeAsync(valueDate, tradeType).ConfigureAwait(false);
            if (riskPositionTypeVM is not null)
            {
                var riskPositionType = riskPositionTypeVM.RiskPositionType;
                var forwardDeltaVM = await GetIronCondorForwardDeltaAsync(valueDate, tradeType, riskPositionType).ConfigureAwait(false);
                if (forwardDeltaVM is not null)
                    forwardDelta = forwardDeltaVM.ForwardDeltaValue;
            }
        }
        catch 
        {
            forwardDelta = 0.0;
        }
        return forwardDelta;

        async Task<RiskPositionTypeReadModel?> GetFuturesRiskPositionTypeAsync(DateOnly valueDate, TradeType tradeType)
        {
            var serviceResult = await _marketDataFeedQueryApi.GetFuturesRiskPositionTypeAsync(valueDate, tradeType).ConfigureAwait(false);
            return serviceResult.Success && serviceResult.Value is not null
                ? serviceResult.Value
                : default;
        }

        async Task<IronCondorForwardDeltaDataModel?> GetIronCondorForwardDeltaAsync(DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
        {
            FuturesContractV2ReadModel[] futuresContracts = default!;
            var serviceResult = await _marketDataQueryApi.GetCurrentlyTradedFuturesContractsAsync("ES").ConfigureAwait(false);
            if (serviceResult.Success && serviceResult.Value is not null)
            {
                futuresContracts = serviceResult.Value;
                if (futuresContracts is not null)
                {
                    var vixContractId = futuresContracts?.FirstOrDefault(x => x.Symbol == "VX" && x.CurrentlyTraded)?.ContractId;
                    if (vixContractId is not null)
                    {
                        var serviceResult2 = await _tradePlan_query_api.GetIronCondorForwardDeltaAsync(vixContractId, valueDate, tradeType, riskPositionType).ConfigureAwait(false);
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
    async Task<ForwardLossLimitType> GetForwardLossLimitTypeAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        var forwardLossLimitType = ForwardLossLimitType.LimitWarning;
        try
        {
            var serviceResult = await _tradePlan_query_api.GetForwardLossLimitTypeAsync(orderId, tradeId, valueDate, tradeType).ConfigureAwait(false);
            if (serviceResult.Success)
                forwardLossLimitType = serviceResult.Value?.LimitType ?? forwardLossLimitType;
        }
        catch { }
        return forwardLossLimitType;
    }
}
