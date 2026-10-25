using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Provides immutable incident reads retained by the Supervisor.</summary>
public interface ISupervisorIncidentReadStore
{
    int ActiveIncidentCount { get; }
    IReadOnlyList<SupervisorActorIncident> ActiveIncidents { get; }
}
