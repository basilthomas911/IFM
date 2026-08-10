namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Bounded reliability and recovery settings for an event projector.
/// </summary>
public sealed record EventProjectorReliabilityOptions
{
    public const string SectionName = "EventProjectorReliability";

    /// <summary>
    /// Enables the SWO-06 bounded joined-state recovery path. It remains disabled until its rollout gate is approved.
    /// </summary>
    public bool BoundedRecoveryEnabled { get; init; }

    /// <summary>
    /// Enables immutable descriptor dispatch and fenced stage execution. This remains an independent rollout switch
    /// so the descriptor conversion can ship without activating the new durable execution protocol.
    /// </summary>
    public bool FencedExecutionEnabled { get; init; }

    /// <summary>
    /// Enables atomic publication staging and the bounded outbox dispatcher. This requires fenced execution and
    /// remains disabled until Tranche D rollout evidence is approved.
    /// </summary>
    public bool TransactionalOutboxEnabled { get; init; }
    public int RecoveryBatchSize { get; init; } = 256;
    public int RecoveryStreamConcurrency { get; init; } = Math.Min(Environment.ProcessorCount, 8);
    public int MaximumReplayAttempts { get; init; } = 3;
    public TimeSpan InitialReplayDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ClaimLeaseDuration { get; init; } = TimeSpan.FromMinutes(2);
    public int OutboxBatchSize { get; init; } = 256;
    public int MaximumOutboxAttempts { get; init; } = 20;
    public TimeSpan OutboxPollingInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan OutboxDispatchLeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    public EventProjectorReliabilityOptions Validate()
    {
        ValidateRange(RecoveryBatchSize, 1, 2_048, nameof(RecoveryBatchSize));
        ValidateRange(RecoveryStreamConcurrency, 1, 32, nameof(RecoveryStreamConcurrency));
        ValidateRange(MaximumReplayAttempts, 1, 20, nameof(MaximumReplayAttempts));
        ValidateRange(OutboxBatchSize, 1, 2_048, nameof(OutboxBatchSize));
        ValidateRange(MaximumOutboxAttempts, 1, 100, nameof(MaximumOutboxAttempts));
        if (TransactionalOutboxEnabled && !FencedExecutionEnabled)
            throw new InvalidOperationException("Transactional outbox dispatch requires fenced execution.");
        if (InitialReplayDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(InitialReplayDelay));
        if (ClaimLeaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ClaimLeaseDuration));
        if (OutboxPollingInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(OutboxPollingInterval));
        if (OutboxDispatchLeaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(OutboxDispatchLeaseDuration));
        return this;
    }

    static void ValidateRange(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value must be between {minimum} and {maximum}.");
        }
    }
}
