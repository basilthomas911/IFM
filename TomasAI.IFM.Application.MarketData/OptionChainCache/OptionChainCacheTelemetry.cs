using System.Diagnostics.Metrics;
namespace TomasAI.IFM.Application.MarketData.OptionChainCache;
/// <summary>Low-cardinality cache instruments. No contract, workflow or scope IDs become metric dimensions.</summary>
internal static class OptionChainCacheTelemetry
{
    static readonly Meter Meter = new("TomasAI.IFM.OptionChainCache");
    internal static readonly Histogram<double> ReadMilliseconds = Meter.CreateHistogram<double>("ifm.option_chain_cache.read.duration", "ms");
    internal static readonly Counter<long> ReadOutcomes = Meter.CreateCounter<long>("ifm.option_chain_cache.read.outcomes");
    internal static readonly Counter<long> Publications = Meter.CreateCounter<long>("ifm.option_chain_cache.publications");
    internal static readonly Histogram<double> PublicationAge = Meter.CreateHistogram<double>("ifm.option_chain_cache.publication.age", "ms");
}
