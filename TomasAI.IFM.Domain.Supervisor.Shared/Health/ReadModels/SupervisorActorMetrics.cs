using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

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
