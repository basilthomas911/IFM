using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Initial empty history store used before bounded history is enabled.</summary>
public sealed class EmptySupervisorHistoryStore : ISupervisorHistoryStore
{
    /// <inheritdoc />
    public long LatestRevision => 0;
    /// <inheritdoc />
    public IReadOnlyList<SupervisorHealthHistoryPoint> Read(DateTime fromUtc, DateTime toUtc, int maximumCount = 1440) => [];
}
