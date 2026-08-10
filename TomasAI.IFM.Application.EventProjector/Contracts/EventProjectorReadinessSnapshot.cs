namespace TomasAI.IFM.Application.EventProjector.Contracts;

/// <summary>
/// Low-cardinality readiness state for one projector runtime.
/// </summary>
public sealed record EventProjectorReadinessSnapshot(
    string ProjectorName,
    bool IsReady,
    long RecoveryEventsDiscovered,
    long RecoveryEventsQueued,
    DateTimeOffset UpdatedAtUtc,
    string FailureReason = "");

public interface IEventProjectorReadiness
{
    EventProjectorReadinessSnapshot GetSnapshot(string projectorName);
}
