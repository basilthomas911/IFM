using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Provides atomic storage for the latest immutable actor metrics snapshot.</summary>
public interface ISupervisorActorMetricsState
{
    SupervisorActorMetricsSnapshot Current { get; }
    void Store(SupervisorActorMetricsSnapshot snapshot);
}
