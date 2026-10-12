using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>
/// Minimal host-only boundary that starts Supervisor actors before the managed population and stops them after it.
/// It cannot start, stop, or mutate non-Supervisor actors.
/// </summary>
public interface ISupervisorBootstrap
{
    bool IsRunning { get; }
    IReadOnlyList<ActorType> ActorTypes { get; }
    ValueTask<SupervisorActorsStartupResult> StartSupervisorAsync(CancellationToken cancellationToken);
    ValueTask<SupervisorActorsShutdownResult> StopSupervisorAsync(CancellationToken cancellationToken);
}
