using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Disabled initial advisory implementation with no allocation, I/O, retry, or backpressure.</summary>
public sealed class NoOpSupervisorHealthLlmAdvisorySink : ISupervisorHealthLlmAdvisorySink
{
    /// <inheritdoc />
    public void Observe(SupervisorHealthAdvisoryObservation observation)
    {
    }
}
