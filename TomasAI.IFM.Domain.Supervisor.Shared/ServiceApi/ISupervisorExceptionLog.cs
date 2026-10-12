using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Receives polling failures through an independent best-effort exception path.</summary>
public interface ISupervisorExceptionLog
{
    void ActorSnapshotFailed(ActorMailboxId actorId, Exception exception);
    void PollCycleFailed(Exception exception);
    void HeartbeatLogFailed(Exception exception);
}
