namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>An execution lease prepared by the projector before it accesses storage.</summary>
public sealed class EventProjectorLease
{
    public EventProjectorLease(long eventId, string projectorName, Guid executionToken,
        DateTime nowUtc, TimeSpan leaseDuration, long expectedRevision = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        if (executionToken == Guid.Empty)
            throw new ArgumentException("The execution token cannot be empty.", nameof(executionToken));
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The timestamp must use DateTimeKind.Utc.", nameof(nowUtc));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        EventId = eventId;
        ProjectorName = projectorName;
        ExecutionToken = executionToken;
        NowUtc = nowUtc;
        ExpiresAtUtc = nowUtc.Add(leaseDuration);
        ExpectedRevision = expectedRevision;
    }

    public long EventId { get; }
    public string ProjectorName { get; }
    public Guid ExecutionToken { get; }
    public DateTime NowUtc { get; }
    public DateTime ExpiresAtUtc { get; }
    public long ExpectedRevision { get; }
}
