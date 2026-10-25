using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
/// <summary>Configures one timeframe-and-period market-data signal.</summary>
[MessagePackObject]
public sealed record RegimeDiscoverySignalMetricConfiguration
{
    [Key(0)] public Guid RequirementId { get; init; }
    [Key(1)] public SignalMetricsType Metric { get; init; }
    [Key(2)] public TimeFrameType TimeFrame { get; init; }
    [Key(3)] public int PeriodLength { get; init; }
    [Key(4)] public bool Enabled { get; init; } = true;
    [Key(5)] public int MaximumAgeSeconds { get; init; }
    [Key(6)] public bool PrepareAtStartup { get; init; } = true;
    [Key(7)] public bool Monitor { get; init; } = true;
}



