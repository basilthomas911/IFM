using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Future, optional, constant-time advisory observer. The initial implementation is a no-op.</summary>
public interface ISupervisorHealthLlmAdvisorySink
{
    void Observe(SupervisorHealthAdvisoryObservation observation);
}
