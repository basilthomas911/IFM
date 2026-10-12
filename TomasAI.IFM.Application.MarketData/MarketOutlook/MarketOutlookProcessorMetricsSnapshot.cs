using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public sealed record MarketOutlookProcessorMetricsSnapshot
{
    public required IReadOnlyDictionary<MarketOutlookUpdateKind, MarketOutlookUpdateMetricSnapshot> Updates { get; init; }
    public int PendingCount { get; init; }
    public DateTime? OldestPendingUtc { get; init; }
    public bool IsProcessorReady { get; init; }
}
