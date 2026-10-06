using System.Diagnostics.Metrics;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>Aggregated cache lookup telemetry without instrument or trade identity labels.</summary>
internal static class CurrentEodCacheMetrics
{
    internal static readonly Meter Meter = new("TomasAI.IFM.MarketData.Eod");
    internal static readonly Counter<long> Hits = Meter.CreateCounter<long>("ifm.eod.cache.hits", "{lookup}");
    internal static readonly Counter<long> Misses = Meter.CreateCounter<long>("ifm.eod.cache.misses", "{lookup}");
}
