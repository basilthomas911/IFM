using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Requests one validated, generation-fenced actor lifecycle operation.</summary>
public sealed record SupervisorActorOperationRequest(
    Guid OperationId,
    ActorThreadId Target,
    long ExpectedGeneration,
    SupervisorActorOperationKind Operation,
    string Requester,
    string Reason,
    TimeSpan Timeout);

/// <summary>Nonthrowing result returned by managed actor startup.</summary>
public sealed record SupervisorActorsStartupResult(
    SupervisorOperationOutcome Outcome,
    Guid OperationId,
    int ExpectedActors,
    int StartedActors,
    string Stage,
    string? FailureReason)
{
    /// <summary>Gets whether startup completed successfully.</summary>
    public bool Succeeded => Outcome == SupervisorOperationOutcome.Succeeded;
}

/// <summary>Nonthrowing result returned by managed actor shutdown.</summary>
public sealed record SupervisorActorsShutdownResult(
    SupervisorOperationOutcome Outcome,
    Guid OperationId,
    int ExpectedActors,
    int StoppedActors,
    string Stage,
    string? FailureReason)
{
    /// <summary>Gets whether shutdown completed successfully.</summary>
    public bool Succeeded => Outcome == SupervisorOperationOutcome.Succeeded;
}

/// <summary>Nonthrowing result returned by an individual actor operation.</summary>
public sealed record SupervisorActorOperationResult(
    SupervisorOperationOutcome Outcome,
    Guid OperationId,
    ActorThreadId Target,
    long ExpectedGeneration,
    string Stage,
    string? FailureReason)
{
    /// <summary>Gets whether the requested operation completed successfully.</summary>
    public bool Succeeded => Outcome == SupervisorOperationOutcome.Succeeded;
}

/// <summary>Sends authorized Supervisor lifecycle commands through the actor messaging transport.</summary>
public interface ISupervisorCommandApi
{
    /// <summary>Executes one audited, generation-fenced operation for an actor entity mailbox.</summary>
    ValueTask<ServiceResult<GuidResult>> ExecuteActorOperationAsync(
        ActorThreadId target,
        long expectedGeneration,
        SupervisorActorOperationKind operation,
        string requester,
        string reason,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

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

/// <summary>One bounded Supervisor recovery inspection or owned-component repair outcome.</summary>
public sealed record SupervisorRecoveryComponentResult(
    string Component, SupervisorActorHealth Health, string Action, string Detail);

/// <summary>Aggregate privileged actor/projector reconciliation evidence.</summary>
public sealed record SupervisorRecoveryResult(
    Guid OperationId, bool Qualified, int Healthy, int Degraded, int Critical, int Unknown,
    IReadOnlyList<SupervisorRecoveryComponentResult> Components, string Detail);

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

/// <summary>Provides atomic storage for the latest immutable actor metrics snapshot.</summary>
public interface ISupervisorActorMetricsState
{
    SupervisorActorMetricsSnapshot Current { get; }
    void Store(SupervisorActorMetricsSnapshot snapshot);
}

/// <summary>Provides a stable list of managed actors to the dedicated polling service.</summary>
public interface ISupervisorManagedActorMetricsSource
{
    IReadOnlyList<ISupervisorActorMetricsSource> Current { get; }
}

/// <summary>Captures metrics for one managed actor without actor messaging or asynchronous work.</summary>
public interface ISupervisorActorMetricsSource
{
    ActorMailboxId ActorId { get; }
    SupervisorActorMetrics CaptureSnapshot();
}

/// <summary>Receives polling failures through an independent best-effort exception path.</summary>
public interface ISupervisorExceptionLog
{
    void ActorSnapshotFailed(ActorMailboxId actorId, Exception exception);
    void PollCycleFailed(Exception exception);
    void HeartbeatLogFailed(Exception exception);
}

/// <summary>Future, optional, constant-time advisory observer. The initial implementation is a no-op.</summary>
public interface ISupervisorHealthLlmAdvisorySink
{
    void Observe(SupervisorHealthAdvisoryObservation observation);
}

/// <summary>Controls the dedicated actor-metrics polling thread.</summary>
public interface ISupervisorActorMetricsPollingService
{
    SupervisorPollingServiceState State { get; }
    SupervisorPollingServiceStatus CaptureStatus();
    void Start();
    bool Stop(TimeSpan timeout);
}

/// <summary>Evaluates stored actor health outside the critical polling thread.</summary>
public interface ISupervisorHealthManager
{
    SupervisorAuthorityState AuthorityState { get; }
}

/// <summary>Provides immutable incident reads retained by the Supervisor.</summary>
public interface ISupervisorIncidentReadStore
{
    int ActiveIncidentCount { get; }
    IReadOnlyList<SupervisorActorIncident> ActiveIncidents { get; }
}

/// <summary>Projects incident acknowledgements into the operational incident read model.</summary>
public interface ISupervisorIncidentStore : ISupervisorIncidentReadStore
{
    bool Acknowledge(ActorThreadId threadId, string requester, string reason);
}

/// <summary>Provides immutable lifecycle-operation reads retained by the Supervisor.</summary>
public interface ISupervisorOperationStore
{
    int ActiveOperationCount { get; }
    IReadOnlyList<SupervisorHealthOperation> RecentOperations { get; }
}

/// <summary>Accepts nonblocking, deduplicated automatic health actions.</summary>
public interface ISupervisorHealthActionCoordinator
{
    void Observe(SupervisorActorMetricsSnapshot snapshot);
}

/// <summary>Authorizes a named operator for one Supervisor lifecycle mutation.</summary>
public interface ISupervisorOperatorAuthorizer
{
    bool IsAuthorized(string requester, SupervisorActorOperationKind operation);
}

/// <summary>Provides immutable bounded Supervisor history reads.</summary>
public interface ISupervisorHistoryStore
{
    long LatestRevision { get; }
    IReadOnlyList<SupervisorHealthHistoryPoint> Read(DateTime fromUtc, DateTime toUtc, int maximumCount = 1440);
}

/// <summary>Persists bounded Supervisor history away from the critical polling thread.</summary>
public interface ISupervisorHistoryPersistence
{
    ValueTask AppendAsync(SupervisorHealthHistoryPoint point, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadAsync(
        DateTime fromUtc, DateTime toUtc, int maximumCount, CancellationToken cancellationToken);
}
