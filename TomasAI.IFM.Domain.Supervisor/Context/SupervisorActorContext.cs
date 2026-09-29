using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.Context;

/// <summary>Capability-restricted context supplied exclusively to Supervisor actors.</summary>
public sealed class SupervisorActorContext(
    ISupervisorManagedActorLifecycle managedActors,
    ISupervisorActorMetricsState actorMetrics,
    ISupervisorHealthManager health,
    ISupervisorIncidentStore incidents,
    ISupervisorOperationStore operations,
    ISupervisorHistoryStore history) : ISupervisorActorContext
{
    /// <inheritdoc />
    public ISupervisorManagedActorLifecycle ManagedActors { get; } =
        managedActors ?? throw new ArgumentNullException(nameof(managedActors));

    /// <inheritdoc />
    public ISupervisorActorMetricsState ActorMetrics { get; } =
        actorMetrics ?? throw new ArgumentNullException(nameof(actorMetrics));

    /// <inheritdoc />
    public ISupervisorHealthManager Health { get; } =
        health ?? throw new ArgumentNullException(nameof(health));

    /// <inheritdoc />
    public ISupervisorIncidentStore Incidents { get; } =
        incidents ?? throw new ArgumentNullException(nameof(incidents));

    /// <inheritdoc />
    public ISupervisorOperationStore Operations { get; } =
        operations ?? throw new ArgumentNullException(nameof(operations));

    /// <inheritdoc />
    public ISupervisorHistoryStore History { get; } =
        history ?? throw new ArgumentNullException(nameof(history));
}
