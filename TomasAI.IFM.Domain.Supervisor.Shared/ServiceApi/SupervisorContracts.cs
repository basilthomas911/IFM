using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;

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

/// <summary>Owns construction, startup, shutdown, and controlled mutation of non-Supervisor actors.</summary>
public interface ISupervisorManagedActorLifecycle
{
    ValueTask<SupervisorActorsStartupResult> StartupActorsAsync(CancellationToken cancellationToken);
    ValueTask<SupervisorActorsShutdownResult> ShutdownActorsAsync(CancellationToken cancellationToken);
    ValueTask<SupervisorActorOperationResult> ExecuteAsync(
        SupervisorActorOperationRequest request,
        CancellationToken cancellationToken);
}

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
public interface ISupervisorIncidentStore
{
    int ActiveIncidentCount { get; }
    IReadOnlyList<SupervisorActorIncident> ActiveIncidents { get; }
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

/// <summary>
/// Privileged capability context available only to Supervisor actors. It exposes named operations and immutable
/// reads, never runtime dictionaries, actors, mailboxes, workers, transports, or the dependency-injection container.
/// </summary>
public interface ISupervisorActorContext
{
    ISupervisorManagedActorLifecycle ManagedActors { get; }
    ISupervisorActorMetricsState ActorMetrics { get; }
    ISupervisorHealthManager Health { get; }
    ISupervisorIncidentStore Incidents { get; }
    ISupervisorOperationStore Operations { get; }
    ISupervisorHistoryStore History { get; }
}
