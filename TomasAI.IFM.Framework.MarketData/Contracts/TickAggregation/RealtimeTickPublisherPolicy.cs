namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

/// <summary>Opt-in bounded realtime delivery for supervised Stage 3. Null policy retains legacy behavior.</summary>
public sealed record RealtimeTickPublisherPolicy
{
    public int Capacity { get; init; } = 4096;
    public TimeSpan MaximumQueueAge { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan CancellationGracePeriod { get; init; } = TimeSpan.FromMilliseconds(100);

    public RealtimeTickPublisherPolicy Validate()
    {
        if (Capacity is < 1 or > 65536 || MaximumQueueAge <= TimeSpan.Zero
            || MaximumQueueAge > TimeSpan.FromMinutes(5) || SendTimeout <= TimeSpan.Zero
            || SendTimeout > TimeSpan.FromMinutes(1) || CancellationGracePeriod < TimeSpan.Zero
            || CancellationGracePeriod > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(RealtimeTickPublisherPolicy), "Realtime publisher bounds are invalid.");
        return this;
    }
}

public enum RealtimeTickPublisherFailure
{
    None,
    Saturated,
    QueueExpired,
    TransportFailed,
    SendTimedOut,
    NonCooperativeSend
}

/// <summary>Cumulative, immutable diagnostics; capacity/depth describe queued items, with at most one extra in-flight send.</summary>
public sealed record RealtimeTickPublisherSnapshot(
    bool PolicyEnabled,
    bool Running,
    bool Faulted,
    bool CanRecover,
    bool UncontainedSend,
    int Capacity,
    int Depth,
    int InFlight,
    TimeSpan OldestQueuedAge,
    TimeSpan InFlightAge,
    long Accepted,
    long Published,
    long Rejected,
    long SaturationCount,
    long GenerationCanceled,
    long ShutdownDiscarded,
    long Expired,
    long Failed,
    RealtimeTickPublisherFailure Failure,
    string FailureDetail);

public interface ITickAggregationPublisherDiagnostics
{
    RealtimeTickPublisherSnapshot GetSnapshot();
}

/// <summary>The caller retains any quote lease when this nonblocking admission rejection is thrown.</summary>
public sealed class RealtimeTickPublisherSaturatedException(int capacity)
    : InvalidOperationException($"The bounded realtime publisher queue is full (capacity={capacity}); this publication was rejected.")
{
    public int Capacity { get; } = capacity;
}

public sealed class RealtimeTickPublisherUnavailableException(string reason)
    : InvalidOperationException($"The bounded realtime publisher is unavailable: {reason}");
