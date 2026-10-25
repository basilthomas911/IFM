using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Read-only runtime source for one actor. It exposes metrics snapshots but no lifecycle authority.</summary>
public interface IActorRuntimeMetricsSource
{
    ActorMailboxId ActorId { get; }
    SupervisorActorSnapshot CaptureSnapshot();
}
