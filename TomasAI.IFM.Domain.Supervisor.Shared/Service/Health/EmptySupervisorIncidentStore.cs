using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Initial empty incident store used before incident evaluation is enabled.</summary>
public sealed class EmptySupervisorIncidentStore : ISupervisorIncidentStore
{
    /// <inheritdoc />
    public int ActiveIncidentCount => 0;
    public IReadOnlyList<SupervisorActorIncident> ActiveIncidents => [];
    public bool Acknowledge(ActorThreadId threadId, string requester, string reason) => false;
}
