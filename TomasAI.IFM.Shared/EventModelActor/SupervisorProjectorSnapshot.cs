using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed record SupervisorProjectorSnapshot(
    string ActorName,
    string ProjectorName,
    string DurableProcessQueue,
    string DurableReplayQueue,
    bool IsReady,
    long RecoveryEventsDiscovered,
    long RecoveryEventsQueued,
    DateTime UpdatedUtc,
    string FailureReason,
    long PendingCount = 0,
    double OldestPendingAgeSeconds = 0,
    long BlockedCount = 0,
    long TerminalFailedCount = 0,
    long ExpiredLeaseCount = 0,
    long OutboxPendingCount = 0,
    double OldestOutboxAgeSeconds = 0,
    long OutboxRetryCount = 0,
    int BusyWorkers = 0,
    int WorkerCapacity = 0);
