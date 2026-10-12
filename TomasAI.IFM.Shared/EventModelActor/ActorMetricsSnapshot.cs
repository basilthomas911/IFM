using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed record ActorMetricsSnapshot(
    ActorMailboxId ActorId,
    DateTime ObservedUtc,
    IReadOnlyList<ActorMailboxMetricsSnapshot> Mailboxes,
    long Revision);
