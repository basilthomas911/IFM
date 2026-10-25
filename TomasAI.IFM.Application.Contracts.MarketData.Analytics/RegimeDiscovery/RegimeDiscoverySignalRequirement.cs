using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Defines one exact signal metric required by a snapshot request.</summary>
[MessagePackObject]
public sealed record RegimeDiscoverySignalRequirement
{
    /// <summary>Gets the normalized metric.</summary>
    [Key(0)] public RegimeDiscoverySignalMetric Metric { get; init; }
    /// <summary>Gets the observation timeframe.</summary>
    [Key(1)] public TimeFrameType TimeFrame { get; init; }
    /// <summary>Gets whether the metric is required.</summary>
    [Key(2)] public bool IsRequired { get; init; }
    /// <summary>Gets the immutable upstream calculation configuration identity.</summary>
    [Key(3)] public string CalculationConfigurationId { get; init; } = string.Empty;
    /// <summary>Gets the maximum accepted age in seconds.</summary>
    [Key(4)] public int MaximumAgeSeconds { get; init; }
    /// <summary>Gets the configured evidence weight.</summary>
    [Key(5)] public decimal Weight { get; init; }
}
