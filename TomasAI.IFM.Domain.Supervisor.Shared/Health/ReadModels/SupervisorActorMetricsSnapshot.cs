using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Immutable aggregate snapshot stored by the Supervisor after each polling cycle.</summary>
public sealed record SupervisorActorMetricsSnapshot(
    long Revision,
    DateTime ObservedUtc,
    long StartedTimestamp,
    long CompletedTimestamp,
    TimeSpan Elapsed,
    DateTime NextPollUtc,
    int ExpectedActors,
    int CollectedActors,
    int FailedActors,
    int HealthyActors,
    int DegradedActors,
    int CriticalActors,
    int UnknownActors,
    int EntityMailboxes,
    long QueueDepth,
    long Rejected,
    long Failed,
    SupervisorSnapshotQuality Quality,
    IReadOnlyList<SupervisorActorMetrics> Actors,
    double ProcessCpuPercent = 0,
    long ProcessWorkingSetBytes = 0,
    long ManagedHeapBytes = 0,
    long TotalAllocatedBytes = 0,
    long Gen0Collections = 0,
    long Gen1Collections = 0,
    long Gen2Collections = 0,
    TimeSpan TotalGcPause = default,
    long ThreadPoolPendingItems = 0,
    int ThreadPoolThreads = 0)
{
    /// <summary>Gets an initial snapshot used before the first collection cycle completes.</summary>
    public static SupervisorActorMetricsSnapshot Empty { get; } = new(
        0, DateTime.UnixEpoch, 0, 0, TimeSpan.Zero, DateTime.UnixEpoch,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        SupervisorSnapshotQuality.Minimal, []);

    /// <summary>Returns whether the required health-count invariant holds.</summary>
    public bool HasValidHealthCount =>
        HealthyActors + DegradedActors + CriticalActors + UnknownActors == CollectedActors + FailedActors;
}
