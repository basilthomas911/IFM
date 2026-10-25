using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Metrics;

/// <summary>Stores the latest immutable Supervisor snapshot using atomic reference replacement.</summary>
public sealed class SupervisorActorMetricsState : ISupervisorActorMetricsState
{
    SupervisorActorMetricsSnapshot _current = SupervisorActorMetricsSnapshot.Empty;

    /// <inheritdoc />
    public SupervisorActorMetricsSnapshot Current => Volatile.Read(ref _current);

    /// <inheritdoc />
    public void Store(SupervisorActorMetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.HasValidHealthCount)
            throw new ArgumentException("Supervisor snapshot health counts are inconsistent.", nameof(snapshot));
        Interlocked.Exchange(ref _current, snapshot);
    }
}
