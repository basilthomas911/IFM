using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Initial read-only health manager used before policy evaluation is enabled.</summary>
public sealed class ObserveOnlySupervisorHealthManager : ISupervisorHealthManager
{
    /// <inheritdoc />
    public SupervisorAuthorityState AuthorityState => SupervisorAuthorityState.Available;
}
