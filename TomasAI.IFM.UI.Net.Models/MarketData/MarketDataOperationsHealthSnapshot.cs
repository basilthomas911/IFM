namespace TomasAI.IFM.UI.Net.Models.MarketData;

/// <summary>Versioned read-only operations projection; no worker/native handles or recovery authority.</summary>
public sealed record MarketDataOperationsHealthSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public long Revision { get; init; }
    public DateTime ObservedOnUtc { get; init; }
    public string OverallStatus { get; init; } = "Inactive";
    public string SessionState { get; init; } = "Unknown";
    public DateOnly? ValueDate { get; init; }
    public DateTime? LastProbeUtc { get; init; }
    public DateTime? NextProbeUtc { get; init; }
    public long RejectedStaleGenerationPublications { get; init; }
    public IReadOnlyList<MarketDataOperationStageSnapshot> Stages { get; init; } = [];
    public IReadOnlyList<MarketDataDatasetHealthSnapshot> Datasets { get; init; } = [];
}

public sealed record MarketDataOperationStageSnapshot
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

public sealed record MarketDataDatasetHealthSnapshot
{
    public string Dataset { get; init; } = string.Empty;
    public string Status { get; init; } = "Inactive";
    public string Reason { get; init; } = string.Empty;
    public string SessionState { get; init; } = "Unknown";
    public DateOnly? ValueDate { get; init; }
    public int ProcessId { get; init; }
    public Guid WorkerInstanceId { get; init; }
    public Guid GenerationId { get; init; }
    public DateTime? StartedOnUtc { get; init; }
    public DateTime? LastObservedUtc { get; init; }
    public DateTime? LastHealthyUtc { get; init; }
    public DateTime? IncidentOpenedUtc { get; init; }
    public DateTime? NextProbeUtc { get; init; }
    public TimeSpan? IncidentAge { get; init; }
    public int CooperativeAttempts { get; init; }
    public int ProcessReplacementCount { get; init; }
    public bool ProcessReplacementLatched { get; init; }
    public bool Running { get; init; }
    public bool Healthy { get; init; }
    public bool GracefulStopSucceeded { get; init; }
    public bool ForcedTermination { get; init; }
    public ulong RecordsProduced { get; init; }
    public ulong RecordsConsumed { get; init; }
    public ulong RingUsed { get; init; }
    public ulong RingCapacity { get; init; }
    public int ChannelBatchCount { get; init; }
    public int ChannelBatchCapacity { get; init; }
    public long RecordsStarted { get; init; }
    public long RecordsCompleted { get; init; }
}
