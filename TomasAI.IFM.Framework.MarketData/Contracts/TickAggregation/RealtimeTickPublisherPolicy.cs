namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

/// <summary>Opt-in bounded realtime delivery for supervised Stage 3. Null policy retains legacy behavior.</summary>
public sealed record RealtimeTickPublisherPolicy
{
    public int Capacity { get; init; } = 4096;
    public int MaximumQueuedQuoteItems { get; init; } = 65_536;
    public TimeSpan MaximumQueueAge { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan CancellationGracePeriod { get; init; } = TimeSpan.FromMilliseconds(100);
    public int MaximumRetryAttempts { get; init; } = 5;
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan NoProgressResetThreshold { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Validates the supplied configuration and its supported operating limits.</summary>
    /// <returns>This policy after successful validation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This validates current instance properties rather than method parameters; existing exception semantics identify invalid ownership or policy state.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This validates current instance properties rather than method parameters; existing exception semantics identify invalid ownership or policy state.")]
    public RealtimeTickPublisherPolicy Validate()
    {
        if (Capacity is < 1 or > 65536 || MaximumQueuedQuoteItems is < 1 or > 4_194_304
            || MaximumQueueAge <= TimeSpan.Zero
            || MaximumQueueAge > TimeSpan.FromMinutes(5) || SendTimeout <= TimeSpan.Zero
            || SendTimeout > TimeSpan.FromMinutes(1) || CancellationGracePeriod < TimeSpan.Zero
            || CancellationGracePeriod > TimeSpan.FromSeconds(5)
            || MaximumRetryAttempts is < 0 or > 10
            || InitialRetryDelay < TimeSpan.Zero || InitialRetryDelay > TimeSpan.FromSeconds(10)
            || MaximumRetryDelay < InitialRetryDelay || MaximumRetryDelay > TimeSpan.FromMinutes(1)
            || NoProgressResetThreshold <= TimeSpan.Zero || NoProgressResetThreshold > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(RealtimeTickPublisherPolicy), "Realtime publisher bounds are invalid.");
        return this;
    }
}
