using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Actor-owned operational measurements. Reads are observational and never execute on the actor mailbox.
/// </summary>
public interface IActorMetricsStore
{
    ActorMailboxId ActorId { get; }
    ActorMetricsSnapshot CaptureSnapshot();
    bool TryGetMailboxSnapshot(ActorThreadId threadId, out ActorMailboxMetricsSnapshot? snapshot);
}
