namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

public sealed record MarketDataDatasetHealthReadModel
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
