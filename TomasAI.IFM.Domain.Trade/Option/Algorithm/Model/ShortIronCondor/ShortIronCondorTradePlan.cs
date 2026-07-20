using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;

public class ShortIronCondorTradePlan : TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.TradePlan, ITradeAlgorithm
{
    LossProbabilityDataModel? _lossProbability;
    TradePriceReadModel _tradePrice = new();
    SignalProcessor<ShortIronCondorTradePlan>? _sp;

    public static TradePlan? Create(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal, IBlackboardService blackboardService)
    {
        var ironCondorTrades = new OptionTradeCollection(optionTrades.Where(e => e.TradeType == TradeType.ShortIronCondor || e.TradeType == TradeType.LongIronCondor));
        if (ironCondorTrades.Count > 0)
            return new ShortIronCondorAlgorithm(valueDate, ironCondorTrades, futuresEodData, futuresTradeSignal, blackboardService);
        return default;
    }

    public ShortIronCondorTradePlan(TradePlanReadModel e)
       : base(e)
    {
    }

    public ShortIronCondorTradePlan(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal, IBlackboardService blackboardService)
        : this(valueDate, optionTrades, optionTrades.PrimaryTrade ?? throw new ArgumentException("Primary trade is required.", nameof(optionTrades)), futuresEodData, futuresTradeSignal, blackboardService)
    {
    }

    private ShortIronCondorTradePlan(DateOnly valueDate, IOptionTradeCollection optionTrades, OptionTradeReadModel primaryTrade, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal, IBlackboardService blackboardService)
        : base(
            orderId: primaryTrade.OrderId,
            tradeId: primaryTrade.TradeId,
            tradeType: primaryTrade.TradeType,
            tradeDate: primaryTrade.TradeDate,
            valueDate: valueDate,
            maturityDate: primaryTrade.MaturityDate,
            actionDate: DateTime.Now,
            actionType: ActionType.HoldTradePosition,
            actionSubType: ActionSubType.TradeInProfitPosition,
            actionState: ActionState.Normal,
            actionReason: string.Empty,
            tradePnl: primaryTrade.TradePositions?.GetTradePnl() ?? 0m,
            forwardLossRatio: primaryTrade.TradePositions?.GetFowardLossRatio(primaryTrade.TradeType, TradeStatus.IntraDay, primaryTrade.TradeLimit?.MaxLossLimit ?? 0m) ?? 0.0,
            lossProbability: primaryTrade.TradePositions?.Get(primaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay)?.LossProbability ?? 0.0,
            mscore: 0,
            maxProfit: primaryTrade.TradeLimit?.MaxProfit ?? 0m,
            maxLoss: primaryTrade.TradeLimit?.MaxLoss ?? 0m,
            minProfitTarget: primaryTrade.TradeLimit?.MinProfitTarget ?? 0m,
            dailyProfitTarget: primaryTrade.TradeLimit?.DailyProfitTarget ?? 0m,
            assetPrice: Convert.ToDecimal(futuresEodData.ClosePrice),
            assetStdDev: futuresEodData.DailyStdDev,
            assetMean: futuresEodData.Mean,
            assetPriceChange: futuresEodData.DailyPercentChange,
            marketTrend: futuresEodData.MarketDirection,
            marketVolatility: futuresEodData.MarketVolatility,
            marketDirection: futuresEodData.PriceDirection,
            vixVolatility: futuresEodData.PriceVolatility,
            fiftyDayMA: 0,
            fiveDayXMA: 0,
            putOTMProbability: primaryTrade.TradePositions?.Get(primaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay)?.OTMProbability ?? 0,
            callOTMProbability: primaryTrade.TradePositions?.Get(primaryTrade.TradeType, OptionType.Call, TradeStatus.IntraDay)?.OTMProbability ?? 0.0,
            shortPutGamma: primaryTrade.TradePositions?.Get(primaryTrade.TradeType, OptionType.Put, TradeStatus.IntraDay)?.OptionLegData.Get(OptionLegAction.Short, OptionType.Put)?.Gamma ?? 0.0,
            shortCallGamma: primaryTrade.TradePositions?.Get(primaryTrade.TradeType, OptionType.Call, TradeStatus.IntraDay)?.OptionLegData.Get(OptionLegAction.Short, OptionType.Call)?.Gamma ?? 0.0,
            gammaRisk: GammaRiskType.None,
            netPrice: primaryTrade.TradePositions?.GetNetSpread(primaryTrade.TradeType, TradeStatus.IntraDay) ?? 0m,
            forwardPrice: primaryTrade.TradePositions?.GetForwardPrice(primaryTrade.TradeType, TradeStatus.IntraDay) ?? 0m,
            forwardDelta: 0.0,
            stopLossLimit: 0.0)
    {
        SetFuturesTradeSignal(futuresTradeSignal);
        SetFuturesEodData(futuresEodData);
        var openingNetSpread = primaryTrade.TradePositions?.GetNetSpread(primaryTrade.TradeType, TradeStatus.Open) ?? 0m;
        var ironCondorMDILimit = new IronCondorMDILimitDataModel(
            Id: primaryTrade.EntityId,
            ValueDate: valueDate,
            Value: ShortMDIWarningReached,
            WarningLimit: ShortMDIWarningReached * 2 * Convert.ToDouble(openingNetSpread),
            MaxLimit: ShortMDILimitReached * 2 * Convert.ToDouble(openingNetSpread));
        blackboardService.IronCondorMDILimit.Set(primaryTrade.EntityId, valueDate, ironCondorMDILimit);
        var callGamma = Convert.ToInt32(ShortCallGamma * 100000);
        var putGamma = Convert.ToInt32(ShortPutGamma * 100000);
        if (callGamma == 0 || putGamma == 0)
            GammaRisk = GammaRiskType.None;
        else
        {
            var callGammaDiff = Math.Abs(callGamma - putGamma);
            GammaRisk = callGammaDiff switch
            {
                >= 30 => GammaRiskType.HighShortCallGamma,
                >= 20 => GammaRiskType.LowShortCallGamma,
                _ => GammaRiskType.None
            };
        }
    }

    public decimal LossThreshold => _lossProbability?.Threshold ?? 0m;
    public int LossThresholdCount => _lossProbability?.ThresholdCount ?? 0;
    public TradePriceReadModel TradePrice => _tradePrice;
    public override decimal AverageTradePnl => _sp == null ? TradePlan.TradePnl : Convert.ToDecimal(_sp.Average(TradePlan, 10, e => Convert.ToDouble(e.TradePnl)));
    public override string ContractId => FuturesEodData?.ContractId ?? string.Empty;
    public bool IsHighShortGammaRisk => GammaRisk == GammaRiskType.HighShortCallGamma;
    public bool IsLowShortGammaRisk => GammaRisk == GammaRiskType.LowShortCallGamma;

 
    public ShortIronCondorTradePlan TradePlan => this;

    public ShortIronCondorTradePlan SetLossProbability(Func<double, LossProbabilityDataModel> lossProbabilityEstimator)
    {
        _lossProbability = lossProbabilityEstimator(ForwardLossRatio);
        return this;
    }

    public ShortIronCondorTradePlan SetSignalProcessor(Func<SignalProcessor<ShortIronCondorTradePlan>> getSignalProcessor)
    {
        _sp = getSignalProcessor();
        return this;
    }

    public ShortIronCondorTradePlan SetTradePrice(Func<TradePriceReadModel> getTradePrice)
    {
        _tradePrice = getTradePrice();
        return this;
    }

    public ShortIronCondorTradePlan SetStopLossLimit(Func<TradePlanStopLossLimitReadModel> getStopLossLimit)
    {
        SetStopLossLimit(getStopLossLimit().StopLossLimit);
        return this;
    }

    public ShortIronCondorTradePlan SetFundBalance(Func<decimal> getFundBalance)
    {
        SetFundBalance(getFundBalance());
        return this;
    }

    public ShortIronCondorTradePlan SetForwardDelta(Func<DateOnly, TradeType, double> getForwardDelta)
    {
        SetForwardDelta(getForwardDelta(ValueDate, TradeType));
        return this;
    }

    public ShortIronCondorTradePlan SetForwardLossLimitType(Func<int, int, TradeType, DateOnly, ForwardLossLimitType> getForwardLossLimitType)
    {
        SetForwardLossLimitType(getForwardLossLimitType(OrderId, TradeId, TradeType, ValueDate));
        return this;
    }

    /// <summary>
    /// evaluate short iron condor trade strategy
    /// </summary>
    /// <returns></returns>
    public override TradePlanUpdatedEvent ExecuteAlgorithm() => throw new NotImplementedException();

}
