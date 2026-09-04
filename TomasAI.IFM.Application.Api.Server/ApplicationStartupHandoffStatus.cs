namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Describes the API-host handoff from bootstrap to the Application lifecycle actor.</summary>
public enum ApplicationStartupHandoffState
{
    NotAttempted = 0,
    CommandAccepted = 1,
    LifecycleObserved = 2,
    CommandRejected = 3,
    Failed = 4,
    TimedOut = 5
}

/// <summary>Process-local evidence for the latest automatic startup handoff.</summary>
public sealed record ApplicationStartupHandoffStatus
{
    public ApplicationStartupHandoffState State { get; init; }
    public DateOnly ValueDate { get; init; }
    public Guid CommandId { get; init; }
    public DateTime? AcceptedAtUtc { get; init; }
    public DateTime? ObservationDeadlineUtc { get; init; }
    public DateTime? ObservedAtUtc { get; init; }
    public int AttemptCount { get; init; }
    public string LastError { get; init; } = string.Empty;
    public string Summary { get; init; } = "Application startup dispatch has not been attempted.";
}

public interface IApplicationStartupHandoffStatusStore
{
    ApplicationStartupHandoffStatus Current { get; }
    void Set(ApplicationStartupHandoffStatus status);
}

/// <summary>Thread-safe handoff status shared by the dispatcher and readiness health check.</summary>
public sealed class ApplicationStartupHandoffStatusStore : IApplicationStartupHandoffStatusStore
{
    ApplicationStartupHandoffStatus current = new();

    public ApplicationStartupHandoffStatus Current => Volatile.Read(ref current);

    public void Set(ApplicationStartupHandoffStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Volatile.Write(ref current, status);
    }
}
