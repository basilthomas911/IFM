using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing an option trade plan snapshot, including signals,
/// risk metrics, market context, and lifecycle metadata.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members
/// are excluded from MessagePack via IgnoreMember/JsonIgnore. A full constructor is provided to preserve call sites.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePlanReadModel
{
    [Key(0)] public long SequenceId { get; init; }
    [Key(1)] public int OrderId { get; init; }
    [Key(2)] public int TradeId { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public DateTime ActionDate { get; init; }
    [Key(5)] public DateOnly TradeDate { get; init; }
    [Key(6)] public DateOnly MaturityDate { get; init; }
    [Key(7)] public TradeType TradeType { get; init; }
    [Key(8)] public ActionType ActionType { get; init; }
    [Key(9)] public ActionSubType ActionSubType { get; init; }
    [Key(10)] public ActionState ActionState { get; init; }
    [Key(11)] public string ActionReason { get; init; } 
    [Key(12)] public decimal TradePnl { get; init; }
    [Key(13)] public double ForwardLossRatio { get; init; }
    [Key(14)] public double LossProbability { get; init; }
    [Key(15)] public double MScore { get; init; }
    [Key(16)] public decimal MaxProfit { get; init; }
    [Key(17)] public decimal MaxLoss { get; init; }
    [Key(18)] public decimal MinProfitTarget { get; init; }
    [Key(19)] public decimal DailyProfitTarget { get; init; }
    [Key(20)] public decimal AssetPrice { get; init; }
    [Key(21)] public double AssetStdDev { get; init; }
    [Key(22)] public double AssetMean { get; init; }
    [Key(23)] public double AssetPriceChange { get; init; }
    [Key(24)] public MarketDirectionType MarketTrend { get; init; }
    [Key(25)] public MarketVolatilityType MarketVolatility { get; init; }
    [Key(26)] public PriceDirectionType MarketDirection { get; init; }
    [Key(27)] public PriceVolatilityType VixVolatility { get; init; }
    [Key(28)] public TradeRiskType TradeRisk { get; init; }
    [Key(29)] public double FiftyDayMA { get; init; }
    [Key(30)] public double FiveDayXMA { get; init; }
    [Key(31)] public double PutOTMProbability { get; init; }
    [Key(32)] public double CallOTMProbability { get; init; }
    [Key(33)] public double ShortPutGamma { get; init; }
    [Key(34)] public double ShortCallGamma { get; init; }
    [Key(35)] public GammaRiskType GammaRisk { get; init; }
    [Key(36)] public decimal NetPrice { get; init; }
    [Key(37)] public decimal ForwardPrice { get; init; }
    [Key(38)] public double ForwardDelta { get; init; }
    [Key(39)] public double StopLossLimit { get; init; }
    [Key(40)] public FuturesTrendType TrendType { get; init; }
    [Key(41)] public FuturesTrendStrengthType TrendStrength { get; init; }
    [Key(42)] public double RSI { get; init; }
    [Key(43)] public double RSISlope { get; init; }
    [Key(44)] public FuturesTrendDirectionType TDI { get; init; }
    [Key(45)] public FuturesTrendDirectionStrengthType TDIStrength { get; init; }
    [Key(46)] public DateTime CreatedOn { get; init; }
    [Key(47)] public string CreatedBy { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public TradePlanReadModel() { }

    /// <summary>
    /// Full constructor to preserve existing call sites (positional-argument style).
    /// </summary>
    public TradePlanReadModel(
        long sequenceId,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        DateTime actionDate,
        DateOnly tradeDate,
        DateOnly maturityDate,
        TradeType tradeType,
        ActionType actionType,
        ActionSubType actionSubType,
        ActionState actionState,
        string actionReason,
        decimal tradePnl,
        double forwardLossRatio,
        double lossProbability,
        double mScore,
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
        TradeRiskType tradeRisk,
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
        FuturesTrendType trendType,
        FuturesTrendStrengthType trendStrength,
        double rsi,
        double rsiSlope,
        FuturesTrendDirectionType tdi,
        FuturesTrendDirectionStrengthType tdiStrength,
        DateTime createdOn,
        string createdBy)
    {
        SequenceId = sequenceId;
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        ActionDate = actionDate;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        TradeType = tradeType;
        ActionType = actionType;
        ActionSubType = actionSubType;
        ActionState = actionState;
        ActionReason = actionReason ?? string.Empty;
        TradePnl = tradePnl;
        ForwardLossRatio = forwardLossRatio;
        LossProbability = lossProbability;
        MScore = mScore;
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
        TradeRisk = tradeRisk;
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
        TrendType = trendType;
        TrendStrength = trendStrength;
        RSI = rsi;
        RSISlope = rsiSlope;
        TDI = tdi;
        TDIStrength = tdiStrength;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>Derived identifier for this trade plan (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public TradePlanEntityId EntityId => new(OrderId, TradeId, ValueDate);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;

    /// <summary>
    /// Computes a risk classification from M-Score and trade type.
    /// </summary>
    public static TradeRiskType FromMScore(double mScore, bool isCriticalRisk = false, TradeType tradeType = TradeType.ShortIronCondor)
    {
        if (tradeType == TradeType.ShortIronCondor)
            return mScore switch
            {
                >= 0.8 => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.High,
                >= 0.7 => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.Medium,
                _ => TradeRiskType.Low
            };
        else
            return mScore switch
            {
                >= 0.8 => TradeRiskType.Low,
                >= 0.7 => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.Medium,
                _ => isCriticalRisk ? TradeRiskType.Critical : TradeRiskType.High
            };
    }
}

/// <summary>
/// MessagePack-serializable projection view model for a trade plan row addressed by database identity.
/// </summary>
/// <remarks>
/// Follows the FundOrderReadModel pattern: explicit properties with sequential MessagePack keys.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record struct TradePlanByIdReadModel
{
    /// <summary>Unique identity of the trade plan row.</summary>
    [Key(0)] public long Id { get; init; }
    /// <summary>Order identifier.</summary>
    [Key(1)] public int OrderId { get; init; }
    /// <summary>Trade identifier.</summary>
    [Key(2)] public int TradeId { get; init; }
    /// <summary>Valuation date.</summary>
    [Key(3)] public DateOnly ValueDate { get; init; }
    /// <summary>Action timestamp.</summary>
    [Key(4)] public DateTime ActionDate { get; init; }
    /// <summary>Trade execution date.</summary>
    [Key(5)] public DateOnly TradeDate { get; init; }
    /// <summary>Maturity/expiry date.</summary>
    [Key(6)] public DateOnly MaturityDate { get; init; }
    /// <summary>Trade strategy/type.</summary>
    [Key(7)] public TradeType TradeType { get; init; }
    /// <summary>Action type category.</summary>
    [Key(8)] public ActionType ActionType { get; init; }
    /// <summary>Action sub-type classification.</summary>
    [Key(9)] public ActionSubType ActionSubType { get; init; }
    /// <summary>Action severity/state.</summary>
    [Key(10)] public ActionState ActionState { get; init; }
    /// <summary>Reason text for the action.</summary>
    [Key(11)] public string ActionReason { get; init; }
    /// <summary>Trade PnL at snapshot.</summary>
    [Key(12)] public decimal TradePnl { get; init; }
    /// <summary>Forward loss ratio.</summary>
    [Key(13)] public double ForwardLossRatio { get; init; }
    /// <summary>Loss probability.</summary>
    [Key(14)] public double LossProbability { get; init; }
    /// <summary>M-score metric.</summary>
    [Key(15)] public double MScore { get; init; }
    /// <summary>Maximum profit.</summary>
    [Key(16)] public decimal MaxProfit { get; init; }
    /// <summary>Maximum loss.</summary>
    [Key(17)] public decimal MaxLoss { get; init; }
    /// <summary>Minimum profit target.</summary>
    [Key(18)] public decimal MinProfitTarget { get; init; }
    /// <summary>Daily profit target.</summary>
    [Key(19)] public decimal DailyProfitTarget { get; init; }
    /// <summary>Underlying asset price.</summary>
    [Key(20)] public decimal AssetPrice { get; init; }
    /// <summary>Underlying asset standard deviation.</summary>
    [Key(21)] public double AssetStdDev { get; init; }
    /// <summary>Underlying asset mean.</summary>
    [Key(22)] public double AssetMean { get; init; }
    /// <summary>Underlying asset price change.</summary>
    [Key(23)] public double AssetPriceChange { get; init; }
    /// <summary>Market trend classification.</summary>
    [Key(24)] public MarketDirectionType MarketTrend { get; init; }
    /// <summary>Market volatility classification.</summary>
    [Key(25)] public MarketVolatilityType MarketVolatility { get; init; }
    /// <summary>Market direction classification.</summary>
    [Key(26)] public PriceDirectionType MarketDirection { get; init; }
    /// <summary>VIX volatility classification.</summary>
    [Key(27)] public PriceVolatilityType VixVolatility { get; init; }
    /// <summary>Trade risk classification.</summary>
    [Key(28)] public TradeRiskType TradeRisk { get; init; }
    /// <summary>50-day moving average.</summary>
    [Key(29)] public double FiftyDayMA { get; init; }
    /// <summary>5-day exponential moving average.</summary>
    [Key(30)] public double FiveDayXMA { get; init; }
    /// <summary>Put out-of-the-money probability.</summary>
    [Key(31)] public double PutOTMProbability { get; init; }
    /// <summary>Call out-of-the-money probability.</summary>
    [Key(32)] public double CallOTMProbability { get; init; }
    /// <summary>Short put gamma value.</summary>
    [Key(33)] public double ShortPutGamma { get; init; }
    /// <summary>Short call gamma value.</summary>
    [Key(34)] public double ShortCallGamma { get; init; }
    /// <summary>Gamma risk classification.</summary>
    [Key(35)] public GammaRiskType GammaRisk { get; init; }
    /// <summary>Net price at snapshot.</summary>
    [Key(36)] public decimal NetPrice { get; init; }
    /// <summary>Forward price at snapshot.</summary>
    [Key(37)] public decimal ForwardPrice { get; init; }
    /// <summary>Forward delta.</summary>
    [Key(38)] public double ForwardDelta { get; init; }
    /// <summary>Stop-loss limit value.</summary>
    [Key(39)] public double StopLossLimit { get; init; }
    /// <summary>Trend type classification.</summary>
    [Key(40)] public FuturesTrendType TrendType { get; init; }
    /// <summary>Trend strength classification.</summary>
    [Key(41)] public FuturesTrendStrengthType TrendStrength { get; init; }
    /// <summary>RSI value.</summary>
    [Key(42)] public double RSI { get; init; }
    /// <summary>RSI slope value.</summary>
    [Key(43)] public double RSISlope { get; init; }
    /// <summary>Trend direction indicator.</summary>
    [Key(44)] public FuturesTrendDirectionType TDI { get; init; }
    /// <summary>TDI strength classification.</summary>
    [Key(45)] public FuturesTrendDirectionStrengthType TDIStrength { get; init; }
    /// <summary>Creation timestamp.</summary>
    [Key(46)] public DateTime CreatedOn { get; init; }
    /// <summary>Record creator.</summary>
    [Key(47)] public string CreatedBy { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradePlanByIdReadModel()
    {
        ActionReason = string.Empty;
        CreatedBy = string.Empty;
    }

    /// <summary>Full constructor to preserve positional construction semantics.</summary>
    public TradePlanByIdReadModel(
        long id,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        DateTime actionDate,
        DateOnly tradeDate,
        DateOnly maturityDate,
        TradeType tradeType,
        ActionType actionType,
        ActionSubType actionSubType,
        ActionState actionState,
        string actionReason,
        decimal tradePnl,
        double forwardLossRatio,
        double lossProbability,
        double mScore,
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
        TradeRiskType tradeRisk,
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
        FuturesTrendType trendType,
        FuturesTrendStrengthType trendStrength,
        double rsi,
        double rsiSlope,
        FuturesTrendDirectionType tdi,
        FuturesTrendDirectionStrengthType tdiStrength,
        DateTime createdOn,
        string createdBy)
    {
        Id = id;
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        ActionDate = actionDate;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        TradeType = tradeType;
        ActionType = actionType;
        ActionSubType = actionSubType;
        ActionState = actionState;
        ActionReason = actionReason ?? string.Empty;
        TradePnl = tradePnl;
        ForwardLossRatio = forwardLossRatio;
        LossProbability = lossProbability;
        MScore = mScore;
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
        TradeRisk = tradeRisk;
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
        TrendType = trendType;
        TrendStrength = trendStrength;
        RSI = rsi;
        RSISlope = rsiSlope;
        TDI = tdi;
        TDIStrength = tdiStrength;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;
}