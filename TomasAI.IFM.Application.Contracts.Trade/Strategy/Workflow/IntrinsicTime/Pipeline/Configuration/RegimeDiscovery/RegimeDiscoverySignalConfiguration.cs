using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Explicit versioned signal demand. Disabling monitoring never disables readiness checks.</summary>
[MessagePackObject]
public sealed record RegimeDiscoverySignalConfiguration
{
    [Key(0)] public Guid RequirementId { get; init; }
    [Key(1)] public RegimeDiscoverySignalMetric Metric { get; init; }
    [Key(2)] public TimeFrameType TimeFrame { get; init; }
    [Key(3)] public bool Enabled { get; init; } = true;
    [Key(4)] public bool IsRequired { get; init; }
    [Key(5)] public int MaximumAgeSeconds { get; init; }
    [Key(6)] public string CalculationConfigurationId { get; init; } = string.Empty;
    [Key(7)] public bool PrepareAtStartup { get; init; } = true;
    [Key(8)] public bool Monitor { get; init; } = true;
}
