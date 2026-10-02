using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Immutable metrics for one actor entity mailbox.</summary>
public sealed record SupervisorActorThreadMetrics(
    ActorThreadId ThreadId,
    int QueueDepth,
    int QueueCapacity,
    int PeakQueueDepth,
    TimeSpan OldestMessageAge,
    long Accepted,
    long Dequeued,
    long Succeeded,
    long Failed,
    long Cancelled,
    long Rejected,
    string? CurrentVerb,
    string? CurrentStage,
    TimeSpan CurrentElapsed,
    SupervisorActorHealth Health,
    TimeSpan ContinuousLimitDuration = default,
    bool RestartRequired = false,
    long Generation = 0,
    SupervisorMailboxPressureState PressureState = SupervisorMailboxPressureState.Normal,
    double Utilization = 0,
    int RemainingCapacity = 0,
    DateTime? LastProgressUtc = null,
    bool HasProgress = true,
    bool HandlerDurationWarning = false,
    bool NoProgressWarning = false);

/// <summary>Immutable aggregate metrics for one managed actor.</summary>
public sealed record SupervisorActorMetrics(
    ActorMailboxId ActorId,
    long Generation,
    bool IsRunning,
    SupervisorActorHealth Health,
    int EntityMailboxes,
    long QueueDepth,
    long Rejected,
    long Failed,
    SupervisorSnapshotQuality Quality,
    IReadOnlyList<SupervisorActorThreadMetrics> Threads);

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

/// <summary>Immutable status of the dedicated polling service.</summary>
public sealed record SupervisorPollingServiceStatus(
    SupervisorPollingServiceState State,
    string ThreadName,
    bool IsAlive,
    long CompletedCycles,
    long FailedCycles,
    long Overruns,
    DateTime? LastHeartbeatUtc);

/// <summary>Bounded observation reserved for a future fire-and-forget advisory sink.</summary>
public sealed record SupervisorHealthAdvisoryObservation(
    long Revision,
    DateTime ObservedUtc,
    int HealthyActors,
    int DegradedActors,
    int CriticalActors,
    int UnknownActors);

/// <summary>Bounded historical rollup for one completed Supervisor collection cycle.</summary>
public sealed record SupervisorHealthHistoryPoint(
    long Revision,
    DateTime ObservedUtc,
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
    double ProcessCpuPercent,
    long ProcessWorkingSetBytes,
    long ManagedHeapBytes,
    long TotalAllocatedBytes,
    long Gen0Collections,
    long Gen1Collections,
    long Gen2Collections,
    TimeSpan TotalGcPause,
    long ThreadPoolPendingItems,
    int ThreadPoolThreads);

/// <summary>Immutable current incident for one unhealthy actor thread.</summary>
public sealed record SupervisorActorIncident(
    ActorThreadId ThreadId,
    SupervisorActorHealth Health,
    DateTime FirstObservedUtc,
    DateTime LastObservedUtc,
    long Generation,
    int QueueDepth,
    int QueueCapacity,
    int RestartsIn60Minutes = 0,
    int RestartsIn24Hours = 0,
    bool RequiresTraderReview = false);

/// <summary>Immutable audit result for one automatic Supervisor lifecycle action.</summary>
public sealed record SupervisorHealthOperation(
    Guid OperationId,
    ActorThreadId ThreadId,
    long ExpectedGeneration,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    SupervisorOperationOutcome? Outcome,
    string Reason);
