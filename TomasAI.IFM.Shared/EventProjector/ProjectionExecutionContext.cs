namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Immutable context supplied to an idempotent target projection operation.
/// </summary>
public sealed record ProjectionExecutionContext
{
    public ProjectionExecutionContext(
        string projectorName,
        long eventId,
        long eventStreamId,
        EventProjectorEffectIdentity effectIdentity,
        Guid executionToken,
        EventProjectionIdempotencyStrategy idempotencyStrategy,
        CancellationToken cancellationToken,
        long streamVersion = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        if (eventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventId));
        if (eventStreamId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventStreamId));
        ArgumentNullException.ThrowIfNull(effectIdentity);
        if (effectIdentity.ProjectorName != projectorName || effectIdentity.EventId != eventId)
            throw new ArgumentException("The effect identity must belong to this projector execution.", nameof(effectIdentity));
        if (executionToken == Guid.Empty)
            throw new ArgumentException("The execution token cannot be empty.", nameof(executionToken));
        if (idempotencyStrategy == EventProjectionIdempotencyStrategy.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(idempotencyStrategy));

        ProjectorName = projectorName;
        EventId = eventId;
        EventStreamId = eventStreamId;
        EffectIdentity = effectIdentity;
        ExecutionToken = executionToken;
        IdempotencyStrategy = idempotencyStrategy;
        CancellationToken = cancellationToken;
        StreamVersion = streamVersion;
    }

    public string ProjectorName { get; }
    public long EventId { get; }
    public long EventStreamId { get; }
    public EventProjectorEffectIdentity EffectIdentity { get; }
    public Guid ExecutionToken { get; }
    public EventProjectionIdempotencyStrategy IdempotencyStrategy { get; }
    public CancellationToken CancellationToken { get; }
    /// <summary>Gets the monotonic version of the source event within its event stream.</summary>
    public long StreamVersion { get; }
}
