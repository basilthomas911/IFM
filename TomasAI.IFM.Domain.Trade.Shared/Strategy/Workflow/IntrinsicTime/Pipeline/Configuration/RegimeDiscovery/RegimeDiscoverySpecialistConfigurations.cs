using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures the deterministic Trend calculation.</summary>
[MessagePackObject]
public sealed record TrendRegimeConfiguration
{
    /// <summary>Gets the EMA alignment weight.</summary>
    [Key(0)] public decimal EmaAlignmentWeight { get; init; } = 0.25m;
    /// <summary>Gets the EMA slope weight.</summary>
    [Key(1)] public decimal EmaSlopeWeight { get; init; } = 0.15m;
    /// <summary>Gets the RSI weight.</summary>
    [Key(2)] public decimal RsiWeight { get; init; } = 0.15m;
    /// <summary>Gets the ADX weight.</summary>
    [Key(3)] public decimal AdxWeight { get; init; } = 0.20m;
    /// <summary>Gets the MACD weight.</summary>
    [Key(4)] public decimal MacdWeight { get; init; } = 0.15m;
    /// <summary>Gets the ITI weight.</summary>
    [Key(5)] public decimal ItiWeight { get; init; } = 0.10m;
    /// <summary>Gets the positive ATR-normalized EMA slope scale.</summary>
    [Key(6)] public decimal EmaSlopeScale { get; init; } = 0.10m;
    /// <summary>Gets the positive RSI slope scale.</summary>
    [Key(7)] public decimal RsiSlopeScale { get; init; } = 5m;
    /// <summary>Gets the positive MACD-to-ATR scale.</summary>
    [Key(8)] public decimal MacdAtrScale { get; init; } = 0.25m;
    /// <summary>Gets the absolute score at which direction becomes non-neutral.</summary>
    [Key(9)] public decimal DirectionThreshold { get; init; } = 0.20m;
    /// <summary>Gets the Moderate strength threshold.</summary>
    [Key(10)] public decimal ModerateThreshold { get; init; } = 0.40m;
    /// <summary>Gets the Strong strength threshold.</summary>
    [Key(11)] public decimal StrongThreshold { get; init; } = 0.65m;
    /// <summary>Gets the Extreme strength threshold.</summary>
    [Key(12)] public decimal ExtremeThreshold { get; init; } = 0.85m;
    /// <summary>Gets the ITI reversal threshold for Exhausting.</summary>
    [Key(13)] public decimal ExhaustingReversalThreshold { get; init; } = 0.25m;
    /// <summary>Gets the ITI reversal threshold for Reversing.</summary>
    [Key(14)] public decimal ReversingThreshold { get; init; } = 0.50m;
}

/// <summary>Configures the deterministic Volatility calculation.</summary>
[MessagePackObject]
public sealed record VolatilityRegimeConfiguration
{
    /// <summary>Gets the VIX-level weight.</summary>
    [Key(0)] public decimal VixWeight { get; init; } = 0.35m;
    /// <summary>Gets the ATR-ratio weight.</summary>
    [Key(1)] public decimal AtrRatioWeight { get; init; } = 0.35m;
    /// <summary>Gets the VX term-structure weight.</summary>
    [Key(2)] public decimal TermStructureWeight { get; init; } = 0.20m;
    /// <summary>Gets the optional realized-volatility weight.</summary>
    [Key(3)] public decimal RealizedVolatilityWeight { get; init; } = 0.10m;
    /// <summary>Gets the VIX Normal lower boundary.</summary>
    [Key(4)] public decimal VixNormalBoundary { get; init; } = 12m;
    /// <summary>Gets the VIX High lower boundary.</summary>
    [Key(5)] public decimal VixHighBoundary { get; init; } = 20m;
    /// <summary>Gets the VIX Extreme lower boundary.</summary>
    [Key(6)] public decimal VixExtremeBoundary { get; init; } = 30m;
    /// <summary>Gets the VIX value mapped to a maximum score.</summary>
    [Key(7)] public decimal VixMaximum { get; init; } = 50m;
    /// <summary>Gets the composite change required for expansion or contraction.</summary>
    [Key(8)] public decimal ExpansionThreshold { get; init; } = 0.10m;
    /// <summary>Gets the front/second ratio that creates severe-backwardation evidence.</summary>
    [Key(9)] public decimal SevereBackwardationRatio { get; init; } = 1.05m;
}

/// <summary>Configures the deterministic Market Structure calculation.</summary>
[MessagePackObject]
public sealed record MarketStructureRegimeConfiguration
{
    /// <summary>Gets the Bollinger evidence weight.</summary>
    [Key(0)] public decimal BollingerWeight { get; init; } = 0.25m;
    /// <summary>Gets the EMA20 interaction weight.</summary>
    [Key(1)] public decimal EmaInteractionWeight { get; init; } = 0.20m;
    /// <summary>Gets the ATR/range weight.</summary>
    [Key(2)] public decimal AtrRangeWeight { get; init; } = 0.20m;
    /// <summary>Gets the rolling high/low and breakout weight.</summary>
    [Key(3)] public decimal BreakoutWeight { get; init; } = 0.20m;
    /// <summary>Gets the ITI persistence/reversal weight.</summary>
    [Key(4)] public decimal ItiWeight { get; init; } = 0.15m;
    /// <summary>Gets the ATR-normalized breakout threshold.</summary>
    [Key(5)] public decimal BreakoutAtrThreshold { get; init; } = 0.50m;
    /// <summary>Gets the compressed Bollinger-width ratio boundary.</summary>
    [Key(6)] public decimal CompressionWidthRatio { get; init; } = 0.75m;
    /// <summary>Gets the compressed ATR-ratio boundary.</summary>
    [Key(7)] public decimal CompressionAtrRatio { get; init; } = 0.85m;
    /// <summary>Gets the expanding Bollinger-width ratio boundary.</summary>
    [Key(8)] public decimal ExpansionWidthRatio { get; init; } = 1.25m;
    /// <summary>Gets the expanding ATR-ratio boundary.</summary>
    [Key(9)] public decimal ExpansionAtrRatio { get; init; } = 1.25m;
    /// <summary>Gets the organization score required for Trending.</summary>
    [Key(10)] public decimal TrendingOrganizationThreshold { get; init; } = 0.50m;
    /// <summary>Gets the ITI persistence required for Trending.</summary>
    [Key(11)] public decimal TrendingPersistenceThreshold { get; init; } = 0.50m;
    /// <summary>Gets the maximum absolute organization score for Ranging.</summary>
    [Key(12)] public decimal RangingOrganizationThreshold { get; init; } = 0.25m;
}

/// <summary>Configures deterministic specialist Fusion.</summary>
[MessagePackObject]
public sealed record MarketRegimeFusionConfiguration
{
    /// <summary>Gets the Trend contribution to direction.</summary>
    [Key(0)] public decimal TrendDirectionalWeight { get; init; } = 0.65m;
    /// <summary>Gets the Market Structure contribution to direction.</summary>
    [Key(1)] public decimal MarketStructureDirectionalWeight { get; init; } = 0.35m;
    /// <summary>Gets the Trend contribution to base confidence.</summary>
    [Key(2)] public decimal TrendConfidenceWeight { get; init; } = 0.40m;
    /// <summary>Gets the Volatility contribution to base confidence.</summary>
    [Key(3)] public decimal VolatilityConfidenceWeight { get; init; } = 0.30m;
    /// <summary>Gets the Market Structure contribution to base confidence.</summary>
    [Key(4)] public decimal MarketStructureConfidenceWeight { get; init; } = 0.30m;
    /// <summary>Gets the maximum Volatility penalty applied to conviction.</summary>
    [Key(5)] public decimal VolatilityConvictionPenalty { get; init; } = 0.50m;
    /// <summary>Gets the absolute score at which fused direction becomes non-neutral.</summary>
    [Key(6)] public decimal DirectionThreshold { get; init; } = 0.20m;
    /// <summary>Gets the confidence threshold for LowConfidence restriction.</summary>
    [Key(7)] public decimal LowConfidenceRestrictionThreshold { get; init; } = 0.55m;
    /// <summary>Gets the minimum confidence for Acceptable quality.</summary>
    [Key(8)] public decimal AcceptableQualityThreshold { get; init; } = 0.60m;
    /// <summary>Gets the minimum confidence for High quality.</summary>
    [Key(9)] public decimal HighQualityThreshold { get; init; } = 0.80m;
}
