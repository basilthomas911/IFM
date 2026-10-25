using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed record SupervisorActorSnapshot(
    ActorMailboxId ActorId,
    string Domain,
    string Implementation,
    bool IsRunning,
    SupervisorActorLifecycleState LifecycleState,
    long Generation,
    SupervisorActorHealthStatus Status,
    int QueueDepth,
    long Accepted,
    long Dequeued,
    long Succeeded,
    long HandledFailures,
    long EscapedFailures,
    long Cancelled,
    long Rejected,
    IReadOnlyList<ActorMailboxMetricsSnapshot> Mailboxes);
