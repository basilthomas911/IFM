using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Authorizes a named operator for one Supervisor lifecycle mutation.</summary>
public interface ISupervisorOperatorAuthorizer
{
    bool IsAuthorized(string requester, SupervisorActorOperationKind operation);
}
