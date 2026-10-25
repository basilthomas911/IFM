using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed record SupervisorRuntimeSnapshot(
    DateTime ObservedUtc,
    SupervisorActorHealthStatus OverallStatus,
    int ActorCount,
    int RunningActorCount,
    int ProcessingMailboxCount,
    int QueuedMessageCount,
    IReadOnlyList<SupervisorActorSnapshot> Actors,
    IReadOnlyList<SupervisorFailureRecord> Failures,
    IReadOnlyList<SupervisorProjectorSnapshot> Projectors,
    IReadOnlyList<SupervisorWorkerSnapshot>? Workers = null);
