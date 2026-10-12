using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Provides immutable lifecycle-operation reads retained by the Supervisor.</summary>
public interface ISupervisorOperationStore
{
    int ActiveOperationCount { get; }
    IReadOnlyList<SupervisorHealthOperation> RecentOperations { get; }
}
