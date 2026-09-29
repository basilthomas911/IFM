using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Health;

/// <summary>Initial read-only health manager used before policy evaluation is enabled.</summary>
public sealed class ObserveOnlySupervisorHealthManager : ISupervisorHealthManager
{
    /// <inheritdoc />
    public SupervisorAuthorityState AuthorityState => SupervisorAuthorityState.Available;
}

/// <summary>Initial empty incident store used before incident evaluation is enabled.</summary>
public sealed class EmptySupervisorIncidentStore : ISupervisorIncidentStore
{
    /// <inheritdoc />
    public int ActiveIncidentCount => 0;
    public IReadOnlyList<SupervisorActorIncident> ActiveIncidents => [];
    public bool Acknowledge(ActorThreadId threadId, string requester, string reason) => false;
}

/// <summary>Initial empty operation store used before lifecycle mutation is enabled.</summary>
public sealed class EmptySupervisorOperationStore : ISupervisorOperationStore
{
    /// <inheritdoc />
    public int ActiveOperationCount => 0;
    public IReadOnlyList<SupervisorHealthOperation> RecentOperations => [];
}

/// <summary>Initial empty history store used before bounded history is enabled.</summary>
public sealed class EmptySupervisorHistoryStore : ISupervisorHistoryStore
{
    /// <inheritdoc />
    public long LatestRevision => 0;
    /// <inheritdoc />
    public IReadOnlyList<SupervisorHealthHistoryPoint> Read(DateTime fromUtc, DateTime toUtc, int maximumCount = 1440) => [];
}
