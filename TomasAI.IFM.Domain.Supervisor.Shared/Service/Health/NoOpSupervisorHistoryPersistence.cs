using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Provides an explicitly nonpersistent history sink for isolated tests.</summary>
public sealed class NoOpSupervisorHistoryPersistence : ISupervisorHistoryPersistence
{
    public ValueTask AppendAsync(SupervisorHealthHistoryPoint point, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadAsync(
        DateTime fromUtc, DateTime toUtc, int maximumCount, CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<SupervisorHealthHistoryPoint>>([]);
}
