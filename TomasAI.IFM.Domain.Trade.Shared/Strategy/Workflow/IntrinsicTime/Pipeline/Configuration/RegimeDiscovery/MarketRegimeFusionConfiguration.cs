using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

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
