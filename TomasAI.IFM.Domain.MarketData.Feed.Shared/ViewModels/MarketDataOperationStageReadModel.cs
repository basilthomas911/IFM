namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

public sealed record MarketDataOperationStageReadModel
{
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = "Inactive";
    public string ReasonCode { get; init; } = "NotObserved";
    public string Reason { get; init; } = string.Empty;
    public bool Required { get; init; }
    public long Received { get; init; }
    public long Completed { get; init; }
    public long Failed { get; init; }
    public long Coalesced { get; init; }
    public long Pending { get; init; }
    public long Capacity { get; init; }
    public long HighWater { get; init; }
    public long Saturated { get; init; }
    public DateTime? LastObservedUtc { get; init; }
    public DateTime? LastSucceededUtc { get; init; }
    public DateTime? LastFailedUtc { get; init; }
    public DateTime? MarketDataAsOfUtc { get; init; }
    public TimeSpan? OldestPendingAge { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public TimeSpan MaximumLatency { get; init; }
    public TimeSpan P50Latency { get; init; }
    public TimeSpan P95Latency { get; init; }
    public TimeSpan P99Latency { get; init; }
}
