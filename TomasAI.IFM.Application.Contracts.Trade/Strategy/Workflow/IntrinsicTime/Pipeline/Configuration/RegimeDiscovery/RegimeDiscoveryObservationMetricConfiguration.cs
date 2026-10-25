using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures one current market, contract, session, or intrinsic-time observation.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryObservationMetricConfiguration
{
    [Key(0)] public Guid RequirementId { get; init; }
    [Key(1)] public ObservationMetricsType Metric { get; init; }
    [Key(2)] public bool Enabled { get; init; } = true;
    [Key(3)] public int MaximumAgeSeconds { get; init; }
    [Key(4)] public bool PrepareAtStartup { get; init; } = true;
    [Key(5)] public bool Monitor { get; init; } = true;
}



