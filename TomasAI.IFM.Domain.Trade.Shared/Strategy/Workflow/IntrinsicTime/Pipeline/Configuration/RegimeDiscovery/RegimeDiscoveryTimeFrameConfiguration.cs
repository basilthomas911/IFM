using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures one observation timeframe used by a strategy horizon.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryTimeFrameConfiguration
{
    /// <summary>Gets the observation timeframe.</summary>
    [Key(0)] public TimeFrameType TimeFrame { get; init; }
    /// <summary>Gets its non-negative contribution weight.</summary>
    [Key(1)] public decimal Weight { get; init; }
    /// <summary>Gets whether evidence from this timeframe is required.</summary>
    [Key(2)] public bool IsRequired { get; init; }
    /// <summary>Gets the maximum accepted signal age in seconds.</summary>
    [Key(3)] public int MaximumAgeSeconds { get; init; }
}
