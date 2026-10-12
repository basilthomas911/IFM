using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Persists bounded Supervisor history away from the critical polling thread.</summary>
public interface ISupervisorHistoryPersistence
{
    ValueTask AppendAsync(SupervisorHealthHistoryPoint point, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadAsync(
        DateTime fromUtc, DateTime toUtc, int maximumCount, CancellationToken cancellationToken);
}
