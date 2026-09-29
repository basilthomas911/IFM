using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.Health;

/// <summary>Disabled initial advisory implementation with no allocation, I/O, retry, or backpressure.</summary>
public sealed class NoOpSupervisorHealthLlmAdvisorySink : ISupervisorHealthLlmAdvisorySink
{
    /// <inheritdoc />
    public void Observe(SupervisorHealthAdvisoryObservation observation)
    {
    }
}
