using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

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
