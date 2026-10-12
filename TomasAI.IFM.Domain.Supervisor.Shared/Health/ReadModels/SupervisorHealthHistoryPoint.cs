using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

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
