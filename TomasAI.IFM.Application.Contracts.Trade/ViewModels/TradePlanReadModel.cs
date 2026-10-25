using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the related validation rules together so the full contract is checked in the established order.")]
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
