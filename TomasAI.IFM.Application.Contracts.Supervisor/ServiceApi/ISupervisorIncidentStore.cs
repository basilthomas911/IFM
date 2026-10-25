using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Projects incident acknowledgements into the operational incident read model.</summary>
public interface ISupervisorIncidentStore : ISupervisorIncidentReadStore
{
    bool Acknowledge(ActorThreadId threadId, string requester, string reason);
}
