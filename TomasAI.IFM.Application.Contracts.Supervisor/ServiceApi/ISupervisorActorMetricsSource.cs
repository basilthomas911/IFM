using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Captures metrics for one managed actor without actor messaging or asynchronous work.</summary>
public interface ISupervisorActorMetricsSource
{
    ActorMailboxId ActorId { get; }
    SupervisorActorMetrics CaptureSnapshot();
}
