namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>Identifies a projector's execution of one persisted event.</summary>
public sealed record EventProjectorIdentity
{
    public EventProjectorIdentity(long eventId, string projectorName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        EventId = eventId;
        ProjectorName = projectorName;
    }

    public long EventId { get; }
    public string ProjectorName { get; }
}
