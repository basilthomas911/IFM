using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Provides a stable list of managed actors to the dedicated polling service.</summary>
public interface ISupervisorManagedActorMetricsSource
{
    IReadOnlyList<ISupervisorActorMetricsSource> Current { get; }
}
