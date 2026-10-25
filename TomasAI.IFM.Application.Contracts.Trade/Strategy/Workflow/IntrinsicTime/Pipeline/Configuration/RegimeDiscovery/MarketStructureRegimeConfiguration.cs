using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

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
