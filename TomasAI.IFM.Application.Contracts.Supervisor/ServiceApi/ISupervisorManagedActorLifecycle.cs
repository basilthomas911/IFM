using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Owns construction, startup, shutdown, and controlled mutation of non-Supervisor actors.</summary>
public interface ISupervisorManagedActorLifecycle
{
    ValueTask<SupervisorActorsStartupResult> StartupActorsAsync(CancellationToken cancellationToken);
    ValueTask<SupervisorActorsShutdownResult> ShutdownActorsAsync(CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> PauseAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> DrainAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> ResumeAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> StopAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> RestartAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> QuarantineAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> RetireAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> RecycleAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken);

    /// <summary>Inspects and repairs only Supervisor-owned actors and their projectors before soft admission.</summary>
    ValueTask<SupervisorRecoveryResult> ReconcileAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
