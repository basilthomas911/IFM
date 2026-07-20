using System;
using System.Collections.Generic;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.AlgoMath.Converters;

namespace TomasAI.IFM.Service.AlgoTrader.Model
{
    public abstract class TradePlan
    {
        int _daysToExpiry;
        int _tradingDays;
        List<ITradeDiaryEvent> _tradeDiaryEvents;
        decimal _fundBalance;
        ForwardLossLimitType _forwardLossLimitType;

        public TradePlan(
            int orderId,
            int tradeId,
            TradeType tradeType,
            DateTime tradeDate,
            DateTime valueDate,
            DateTime maturityDate,
            DateTime actionDate,
            ActionType actionType,
            ActionSubType actionSubType,
            ActionState actionState,
            string actionReason,
            decimal tradePnl,
            double forwardLossRatio,
            double lossProbability,
            double mscore,
            decimal maxProfit,
            decimal maxLoss,
            decimal minProfitTarget,
            decimal dailyProfitTarget,
            decimal assetPrice,
            double assetStdDev,
            double assetMean,
            double assetPriceChange,
            MarketDirectionType marketTrend,
            MarketVolatilityType marketVolatility,
            PriceDirectionType marketDirection,
            PriceVolatilityType vixVolatility,
            double fiftyDayMA,
            double fiveDayXMA,
            double putOTMProbability,
            double callOTMProbability,
            double shortPutGamma,
            double shortCallGamma,
            GammaRiskType gammaRisk,
            decimal netPrice,
            decimal forwardPrice,
            double forwardDelta,
            double stopLossLimit,
            DateTime createdOn,
            string createdBy)
        {
            TradePlanId = Guid.NewGuid();
            OrderId = orderId;
            TradeId = tradeId;
            TradeType = tradeType;
            TradeDate = tradeDate;
            ValueDate = valueDate;
            MaturityDate = maturityDate;
            ActionDate = actionDate;
            ActionType = actionType;
            ActionSubType = actionSubType;
            ActionState = actionState;
            ActionReason = actionReason;
            TradePnl = tradePnl;
            ForwardLossRatio = forwardLossRatio;
            LossProbability = lossProbability;
            MScore = mscore;
            MaxProfit = maxProfit;
            MaxLoss = maxLoss;
            MinProfitTarget = minProfitTarget;
            DailyProfitTarget = dailyProfitTarget;
            AssetPrice = assetPrice;
            AssetStdDev = assetStdDev;
            AssetMean = assetMean;
            AssetPriceChange = assetPriceChange;
            MarketTrend = marketTrend;
            MarketVolatility = marketVolatility;
            MarketDirection = marketDirection;
            VixVolatility = vixVolatility;
            TradeRisk = TradePlanReadModel.FromMScore(mscore, tradeType == TradeType.ShortIronCondor ? forwardPrice > netPrice : forwardPrice < netPrice, tradeType);
            FiftyDayMA = fiftyDayMA;
            FiveDayXMA = fiveDayXMA;
            PutOTMProbability = putOTMProbability;
            CallOTMProbability = callOTMProbability;
            ShortPutGamma = shortPutGamma;
            ShortCallGamma = shortCallGamma;
            GammaRisk = gammaRisk;
            NetPrice = netPrice;
            ForwardPrice = forwardPrice;
            ForwardDelta = forwardDelta;
            StopLossLimit = stopLossLimit;
            CreatedOn = createdOn;
            CreatedBy = createdBy;
            _daysToExpiry = (MaturityDate - ValueDate).Days;
            _tradingDays = (MaturityDate - TradeDate).Days;
            _tradeDiaryEvents = new List<ITradeDiaryEvent>();
            _forwardLossLimitType = ForwardLossLimitType.Unknown;
        }


        public TradePlan(TradePlanReadModel e) :
           this(
               orderId: e.OrderId,
               tradeId: e.TradeId,
               tradeType: e.TradeType,
               tradeDate: e.TradeDate,
               valueDate: e.ValueDate,
               maturityDate: e.MaturityDate,
               actionDate: e.ActionDate,
               actionType: e.ActionType,
               actionSubType: e.ActionSubType,
               actionState: e.ActionState,
               actionReason: e.ActionReason,
               tradePnl: e.TradePnl,
               forwardLossRatio: e.ForwardLossRatio,
               lossProbability: e.LossProbability,
               mscore: e.MScore,
               maxProfit: e.MaxProfit,
               maxLoss: e.MaxLoss,
               minProfitTarget: e.MinProfitTarget,
               dailyProfitTarget: e.DailyProfitTarget,
               assetPrice: e.AssetPrice,
               assetStdDev: e.AssetStdDev,
               assetMean: e.AssetMean,
               assetPriceChange: e.AssetPriceChange,
               marketTrend: e.MarketTrend,
               marketVolatility: e.MarketVolatility,
               marketDirection: e.MarketDirection,
               vixVolatility: e.VixVolatility,
               fiftyDayMA: e.FiftyDayMA,
               fiveDayXMA: e.FiveDayXMA,
               putOTMProbability: e.PutOTMProbability,
               callOTMProbability: e.CallOTMProbability,
               shortPutGamma: e.ShortPutGamma,
               shortCallGamma: e.ShortCallGamma,
               gammaRisk: e.GammaRisk,
               netPrice: e.NetPrice,
               forwardPrice: e.ForwardPrice,
               forwardDelta: e.ForwardDelta,
               stopLossLimit: e.StopLossLimit,
               createdOn: e.CreatedOn,
               createdBy: e.CreatedBy)
        {
        }



        public Guid TradePlanId { get; }
        public int OrderId { get; }
        public int TradeId { get; }
        public TradeType TradeType { get; }
        public DateTime TradeDate { get; }
        public DateTime ValueDate { get; }
        public DateTime MaturityDate { get; }
        public DateTime ActionDate { get; }
        public ActionType ActionType { get; private set; }
        public ActionSubType ActionSubType { get; private set; }
        public ActionState ActionState { get; private set; }
        public string ActionReason { get; private set; }
        public decimal TradePnl { get; }
        public double ForwardLossRatio { get; }
        public double LossProbability { get; private set; }
        public double MScore { get; private set; }
        public decimal MaxProfit { get; }
        public decimal MaxLoss { get; }
        public decimal MinProfitTarget { get; }
        public decimal DailyProfitTarget { get; }
        public decimal AssetPrice { get; }
        public double AssetStdDev { get; }
        public double AssetMean { get; }
        public double AssetPriceChange { get; }
        public MarketDirectionType MarketTrend { get; }
        public MarketVolatilityType MarketVolatility { get; }
        public PriceDirectionType MarketDirection { get; }
        public PriceVolatilityType VixVolatility { get; }
        public TradeRiskType TradeRisk { get; private set; }
        public double FiftyDayMA { get; }
        public double FiveDayXMA { get; }
        public double CallOTMProbability { get; }
        public double PutOTMProbability { get; }
        public double ShortPutGamma { get; }
        public double ShortCallGamma { get; }
        public GammaRiskType GammaRisk { get; set; }
        public decimal NetPrice { get; }
        public decimal ForwardPrice { get; }
        public double ForwardDelta { get; private set; }
        public double StopLossLimit { get; private set; }
        public DateTime CreatedOn { get; }
        public string CreatedBy { get; }
        public FuturesTradeSignalViewModel FuturesTradeSignal { get; private set; }
        public FuturesEodDataViewModel FuturesEodData { get; private set; }

        public virtual decimal AverageTradePnl => TradePnl;
        public virtual decimal FundBalance => _fundBalance;
        public virtual double UpTrendLimit => 0.0;
        public virtual double DownTrendLimit => 0.0;
        public virtual string ContractId => string.Empty;

        public bool TradeInProfitPosition => AverageTradePnl >= 0m;
        public bool TradeInLossPosition => AverageTradePnl < 0m;
        public bool MinProfitTargetExceeded => AverageTradePnl > MinProfitTarget || AverageTradePnl > DailyProfitTarget;
        public bool DailyProfitTargetExceeded => AverageTradePnl > DailyProfitTarget;
        public bool MaxLossLimitReached => FundBalance > 0m && AverageTradePnl < MaxLoss;
        public bool MaxLossWarningReached => FundBalance > 0m && AverageTradePnl < (MaxLoss/2);
        public bool CriticalLossLimitReached => FundBalance > 0m && Math.Abs(AverageTradePnl) > (FundBalance * 0.0080m);
        public bool MaxAssetPriceChangeExceeded => AssetPriceChangeUpExceeded || AssetPriceChangeDownExceeded;
        public bool AssetPriceChangeUpExceeded => AssetPriceChange > 0.0025;
        public bool AssetPriceChangeDownExceeded => AssetPriceChange < -0.005;
        public bool CallSpreadAtRisk => CallOTMProbability < PutOTMProbability;
        public bool PutSpreadAtRisk => PutOTMProbability < CallOTMProbability;

        public bool RaiseTrailingStopLimit => (double)AverageTradePnl > ((StopLossLimit == 0.0 ? 0.20 : StopLossLimit + 0.05) * (double)MaxProfit);
        public bool InTrailingStopLimit => StopLossLimit != 0.0;
        public bool TrailingStopLimitReached => (double)AverageTradePnl < ((StopLossLimit - 0.05) * (double)MaxProfit);

        public bool MinStopLossLimitReached => (double)AverageTradePnl > (0.10 * (double)MaxProfit);
        public bool ForwardLossLimitReached => _forwardLossLimitType == ForwardLossLimitType.LimitReached;

        public double ShortMDIWarningReached => FuturesMDIMap.Get(TradeType, FuturesTradeSignal.MDI).ForwardLossRateLimit;
        public double ShortMDILimitReached => ShortMDIWarningReached + 0.03;

        public double LongMDIWarningReached => FuturesMDIMap.Get(TradeType, FuturesTradeSignal.MDI).ForwardLossRateLimit;
        public double LongMDILimitReached => LongMDIWarningReached - 0.03;

        public bool MarketIsRevertingToDownTrend => FuturesTradeSignal.TDI == FuturesTrendDirectionType.DownTrending;
        public bool MarketIsRevertingToUpTrend => FuturesTradeSignal.TDI == FuturesTrendDirectionType.UpTrending;

        public FuturesTrendType TrendDirection => FuturesTradeSignal.TrendType;
        public FuturesTrendStrengthType TrendStrength => FuturesTradeSignal.TrendStrength;
        public double RSI => FuturesTradeSignal.RSI;
        public double RSISlope => FuturesTradeSignal.RSISlope;
        public FuturesTrendDirectionType TDI => FuturesTradeSignal.TDI;
        public FuturesTrendDirectionStrengthType TDIStrength => FuturesTradeSignal.TDIStrength;

        public double GainPercentage => Math.Abs((double)(AverageTradePnl / MaxProfit));
        public double LossPercentage => Math.Abs((double)(AverageTradePnl / MaxLoss));
        public decimal TrailingStopLimit => (decimal)((StopLossLimit - 0.05) * (double)MaxProfit);
        public double MinTrailingStopLimit => StopLossLimit - 0.05;
        public double MarketTrendingFactor => 1.0 / Math.Sqrt(20) * AssetStdDev;
        public decimal MaxLossAmount => (FundBalance * (decimal)FuturesTradeSignal.FundRiskPercent);

        public bool InShortTrade
            =>  this.TradeType switch {
                TradeType.ShortIronCondor  => true,
                _ => false
            };

        public bool InLongTrade
         => this.TradeType switch {
             TradeType.LongIronCondor => true,
             _ => false
         };

        public abstract TradePlanUpdatedEvent ExecuteAlgorithm();

        public TPlan As<TPlan>(TPlan tradePlan) => tradePlan;

        public TradePlanReadModel ToViewModel()
            => new TradePlanReadModel (
                TradePlanId: this.TradePlanId,
                OrderId: this.OrderId,
                TradeId: this.TradeId,
                TradeType: this.TradeType,
                TradeDate: this.TradeDate,
                ValueDate: this.ValueDate,
                MaturityDate: this.MaturityDate,
                ActionDate: this.ActionDate,
                ActionType: this.ActionType,
                ActionSubType: this.ActionSubType,
                ActionState: this.ActionState,
                ActionReason: this.ActionReason,
                TradePnl: this.TradePnl,
                ForwardLossRatio: this.ForwardLossRatio,
                LossProbability: this.LossProbability,
                MScore: this.MScore,
                MaxProfit: this.MaxProfit,
                MaxLoss: this.MaxLoss,
                MinProfitTarget: this.MinProfitTarget,
                DailyProfitTarget: this.DailyProfitTarget,
                AssetPrice: this.AssetPrice,
                AssetStdDev: this.AssetStdDev,
                AssetMean: this.AssetMean,
                AssetPriceChange: this.AssetPriceChange,
                MarketTrend: this.MarketTrend,
                MarketVolatility: this.MarketVolatility,
                MarketDirection: this.MarketDirection,
                VixVolatility: this.VixVolatility,
                TradeRisk: this.TradeRisk,
                FiftyDayMA: this.FiftyDayMA,
                FiveDayXMA: this.FiveDayXMA,
                PutOTMProbability: this.PutOTMProbability,
                CallOTMProbability: this.CallOTMProbability,
                ShortPutGamma: this.ShortPutGamma,
                ShortCallGamma: this.ShortCallGamma,
                GammaRisk: this.GammaRisk,
                NetPrice: this.NetPrice,
                ForwardPrice: this.ForwardPrice,
                ForwardDelta: this.ForwardDelta,    
                StopLossLimit: this.StopLossLimit,
                TrendType: this.TrendDirection,
                TrendStrength: this.TrendStrength,
                RSI: this.RSI,
                RSISlope: this.RSISlope,
                TDI: this.TDI,
                TDIStrength: this.TDIStrength,  
                CreatedOn: this.CreatedOn,
                CreatedBy: this.CreatedBy
            );

        public TradePlan SetLossProbability(double lossProbability)
        {
            LossProbability = lossProbability;
            return this;
        }

        public TradePlan SetStopLossLimit(double stopLossLimit)
        {
            StopLossLimit = stopLossLimit;
            return this;
        }

        public TradePlan SetMScore(double mscore)
        {
            MScore = mscore;
            TradeRisk = TradePlanReadModel.FromMScore(mscore, TradeType == TradeType.ShortIronCondor ? ForwardPrice > NetPrice : ForwardPrice < NetPrice, TradeType);
            return this;
        }

        public TradePlan HoldTradePosition(ActionSubType actionSubType)
        {
            ActionType = ActionType.HoldTradePosition;
            ActionSubType = actionSubType;
            return this;
        }

        public TradePlan ExitTradePosition(ActionSubType actionSubType)
        {
            ActionType = ActionType.ExitTradePosition;
            ActionSubType = actionSubType;
            return this;
        }

        public TradePlan HedgeTradePosition(ActionSubType actionSubType)
        {
            ActionType = ActionType.HedgeTradePosition;
            ActionSubType = actionSubType;
            return this;
        }

        public TradePlan WarnTradePosition(ActionSubType actionSubType)
        {
            ActionType = ActionType.WarnTradePosition;
            ActionSubType = actionSubType;
            return this;
        }

        public TradePlan SetNormal(string actionReason)
        {
            ActionState = ActionState.Normal;
            ActionReason = actionReason;
            return this;
        }

        public TradePlan SetWarning(string actionReason)
        {
            ActionState = ActionState.Warning;
            ActionReason = actionReason;
            return this;
        }

        public TradePlan SetCritical(string actionReason)
        {
            ActionState = ActionState.Critical;
            ActionReason = actionReason;
            return this;
        }

        public TradePlan SetRedAlert(string actionReason)
        {
            ActionState = ActionState.RedAlert;
            ActionReason = actionReason;
            return this;
        }

        public TradePlan IncrementStopLossLimit()
        {
            StopLossLimit = StopLossLimit == 0.0 ? 0.20 : StopLossLimit += 0.05;
            return this;
        }

        public TradePlan ClearStopLossLimit()
        {
            StopLossLimit = 0.0;
            return this;
        }

        public TSource As<TSource>() where TSource : TradePlan => this as TSource;

        protected TradePlan SetFundBalance(decimal fundBalance)
        {
            _fundBalance = fundBalance;
            return this;
        }

        protected TradePlan SetForwardDelta(double forwardDelta)
        {
            ForwardDelta = forwardDelta;
            return this;
        }

        protected TradePlan SetForwardLossLimitType(ForwardLossLimitType forwardLossLimitType)
        {
            _forwardLossLimitType = forwardLossLimitType;
            return this;
        }

        protected TradePlan SetFuturesTradeSignal(FuturesTradeSignalViewModel futuresTradeSignal)
        {
            FuturesTradeSignal = futuresTradeSignal;
            return this;
        }

        protected TradePlan SetFuturesEodData(FuturesEodDataViewModel futuresEodData)
        {
            FuturesEodData = futuresEodData;
            return this;
        }
    }
           
}
