using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Initial empty operation store used before lifecycle mutation is enabled.</summary>
public sealed class EmptySupervisorOperationStore : ISupervisorOperationStore
{
    /// <inheritdoc />
    public int ActiveOperationCount => 0;
    public IReadOnlyList<SupervisorHealthOperation> RecentOperations => [];
}
