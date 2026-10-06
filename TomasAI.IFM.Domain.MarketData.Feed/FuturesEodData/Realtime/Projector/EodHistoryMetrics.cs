using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>Low-cardinality telemetry for history isolation; no per-tick informational logging.</summary>
internal static class EodHistoryMetrics
{
    internal static readonly Meter Meter = new("TomasAI.IFM.MarketData.Eod");
    internal static readonly UpDownCounter<long> Pending = Meter.CreateUpDownCounter<long>("ifm.eod.history.pending", "{record}");
    internal static readonly Counter<long> Failures = Meter.CreateCounter<long>("ifm.eod.history.failures", "{failure}");
    internal static readonly Histogram<double> BatchDuration = Meter.CreateHistogram<double>("ifm.eod.history.batch.duration", "ms");
    internal static readonly Histogram<double> OldestAge = Meter.CreateHistogram<double>("ifm.eod.history.batch.oldest_age", "s");
}
