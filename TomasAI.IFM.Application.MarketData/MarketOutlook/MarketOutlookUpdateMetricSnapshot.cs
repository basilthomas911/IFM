using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public sealed record MarketOutlookUpdateMetricSnapshot
{
    public required MarketOutlookUpdateKind Kind { get; init; }
    public long Received { get; init; }
    public long Enqueued { get; init; }
    public long Applied { get; init; }
    public long Changed { get; init; }
    public long Composed { get; init; }
    public long Published { get; init; }
    public long Failed { get; init; }
    public long Coalesced { get; init; }
    public Guid LastUpdateId { get; init; }
    public DateTime? LastReceivedUtc { get; init; }
    public DateTime? LastAppliedUtc { get; init; }
    public DateTime? LastPublishedUtc { get; init; }
    public DateTime? LastMarketDataAsOfUtc { get; init; }
    public TimeSpan AverageQueueLatency { get; init; }
    public TimeSpan MaximumQueueLatency { get; init; }
    public TimeSpan AverageProcessingLatency { get; init; }
    public TimeSpan MaximumProcessingLatency { get; init; }
    public TimeSpan AveragePublicationLatency { get; init; }
    public TimeSpan MaximumPublicationLatency { get; init; }
}
