using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Immutable, versioned legacy calculation observations captured before evaluation; no provider access occurs in Compute.</summary>
[MessagePackObject]
public sealed record IronCondorTradePlanInputs
{
    /// <summary>Gets the short or long strategy used by recovered risk classification.</summary>
    [Key(0)] public TradeType TradeType { get; init; }
    /// <summary>Gets the put spread forward observation in price points.</summary>
    [Key(1)] public decimal PutForwardPrice { get; init; }
    /// <summary>Gets the call spread forward observation in price points.</summary>
    [Key(2)] public decimal CallForwardPrice { get; init; }
    /// <summary>Gets the positive configured legacy forward-loss denominator in price points.</summary>
    [Key(3)] public decimal ForwardLossPriceLimit { get; init; }
    /// <summary>Gets preceding forward-loss ratios excluding the current observation.</summary>
    [Key(4)] public double[] HistoricalForwardLossRatios { get; init; } = [];
    /// <summary>Gets the persisted baseline identity for reproducible MScore.</summary>
    [Key(5)] public string BaselineIdentity { get; init; } = string.Empty;
    /// <summary>Gets the versioned distribution identity.</summary>
    [Key(6)] public string DistributionIdentity { get; init; } = string.Empty;
    /// <summary>Gets when the distribution observations were captured.</summary>
    [Key(7)] public DateTime ObservedAtUtc { get; init; }
    /// <summary>The persisted opening exchange date. Null identifies an unavailable observation.</summary>
    [Key(8)] public DateOnly? TradeDate { get; init; }
    /// <summary>The exact option expiry date. Null identifies an unavailable observation.</summary>
    [Key(9)] public DateOnly? MaturityDate { get; init; }
    /// <summary>The captured configured business limits, in currency and price points. Null identifies an unavailable observation.</summary>
    [Key(10)] public TradeLimitReadModel? TradeLimits { get; init; }
    /// <summary>The coherent current-session underlying statistics. Null identifies an unavailable observation.</summary>
    [Key(11)] public FuturesEodDataV2ReadModel? UnderlyingStatistics { get; init; }
    /// <summary>The captured seeded analytics signal. Null identifies an unavailable observation.</summary>
    [Key(12)] public FuturesTradeSignalV2ReadModel? TradeSignal { get; init; }
    /// <summary>The qualified short put gamma. Null identifies an unavailable observation.</summary>
    [Key(13)] public double? ShortPutGamma { get; init; }
    /// <summary>The qualified short call gamma. Null identifies an unavailable observation.</summary>
    [Key(14)] public double? ShortCallGamma { get; init; }
    /// <summary>The put distribution probability, from zero to one. Null identifies an unavailable observation.</summary>
    [Key(15)] public double? PutOTMProbability { get; init; }
    /// <summary>The call distribution probability, from zero to one. Null identifies an unavailable observation.</summary>
    [Key(16)] public double? CallOTMProbability { get; init; }
    /// <summary>The versioned legacy MAD loss ratio, which can exceed one; this is not a calibrated probability. Null identifies an unavailable observation.</summary>
    [Key(17)] public double? LossProbability { get; init; }
    /// <summary>The versioned spread forward delta. Null identifies an unavailable observation.</summary>
    [Key(18)] public double? ForwardDelta { get; init; }
    /// <summary>The captured configured trailing stop ratio. Null identifies an unavailable observation.</summary>
    [Key(19)] public double? StopLossLimit { get; init; }
    /// <summary>The seeded daily fifty-session moving average. Null identifies an unavailable observation.</summary>
    [Key(20)] public double? FiftyDayMA { get; init; }
    /// <summary>The seeded daily five-session exponential moving average. Null identifies an unavailable observation.</summary>
    [Key(21)] public double? FiveDayXMA { get; init; }
    /// <summary>The observed fund cash used by legacy maximum-loss guards; null means unavailable.</summary>
    [Key(22)] public decimal? FundBalance { get; init; }
    /// <summary>The persisted forward-loss limit classification; null means unavailable.</summary>
    [Key(23)] public ForwardLossLimitType? ForwardLossLimit { get; init; }
    /// <summary>Current net position PnL in currency under CurrentPnlLatestStop/v1; the permanent key/name remains readable by older consumers.</summary>
    [Key(24)] public decimal? AverageTradePnl { get; init; }
    /// <summary>Gets the oldest qualified option risk valuation time across the four legs.</summary>
    [Key(25)] public DateTime? OptionRiskAsOfUtc { get; init; }
    /// <summary>Gets the earliest qualified risk expiry across the four scopes.</summary>
    [Key(26)] public DateTime? OptionRiskValidUntilUtc { get; init; }
    /// <summary>Gets the authoritative ledger revision used for Fund cash.</summary>
    [Key(27)] public long? FundFinancialRevision { get; init; }
    /// <summary>Gets when the authoritative Fund cash read was captured.</summary>
    [Key(28)] public DateTime? FundCashAsOfUtc { get; init; }
    /// <summary>Current four-leg theoretical spread pricing from qualified OptionCalculator inputs; null means unavailable.</summary>
    [Key(29)] public IronCondorCalculatedSpreadPrices? CalculatedSpreadPrices { get; init; }
    /// <summary>Common exact option multiplier used once when translating position point PnL into currency.</summary>
    [Key(30)] public decimal? ContractCashMultiplier { get; init; }
    /// <summary>Actual opening commissions from the established execution evidence, in currency.</summary>
    [Key(31)] public decimal? OpeningCommission { get; init; }
}
