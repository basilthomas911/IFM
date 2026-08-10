namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Bounded reliability and recovery settings for an event projector.
/// </summary>
public sealed record EventProjectorReliabilityOptions
{
    public int RecoveryBatchSize { get; init; } = 256;
    public int RecoveryStreamConcurrency { get; init; } = Math.Min(Environment.ProcessorCount, 8);
    public int MaximumReplayAttempts { get; init; } = 3;
    public TimeSpan InitialReplayDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ClaimLeaseDuration { get; init; } = TimeSpan.FromMinutes(2);
    public int OutboxBatchSize { get; init; } = 256;

    public EventProjectorReliabilityOptions Validate()
    {
        ValidateRange(RecoveryBatchSize, 1, 2_048, nameof(RecoveryBatchSize));
        ValidateRange(RecoveryStreamConcurrency, 1, 32, nameof(RecoveryStreamConcurrency));
        ValidateRange(MaximumReplayAttempts, 1, 20, nameof(MaximumReplayAttempts));
        ValidateRange(OutboxBatchSize, 1, 2_048, nameof(OutboxBatchSize));
        if (InitialReplayDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(InitialReplayDelay));
        if (ClaimLeaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ClaimLeaseDuration));
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
