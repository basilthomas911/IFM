namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

/// <summary>Cumulative, immutable diagnostics; capacity/depth describe queued items, with at most one extra in-flight send.</summary>
/// <param name="PolicyEnabled">The policy enabled.</param>
/// <param name="Running">The running.</param>
/// <param name="Faulted">The faulted.</param>
/// <param name="CanRecover">The can recover.</param>
/// <param name="UncontainedSend">The uncontained send.</param>
/// <param name="Capacity">The capacity.</param>
/// <param name="Depth">The depth.</param>
/// <param name="InFlight">The in flight.</param>
/// <param name="OldestQueuedAge">The oldest queued age.</param>
/// <param name="InFlightAge">The in flight age.</param>
/// <param name="Accepted">The accepted.</param>
/// <param name="Published">The published.</param>
/// <param name="Rejected">The rejected.</param>
/// <param name="SaturationCount">The saturation count.</param>
/// <param name="GenerationCanceled">The generation canceled.</param>
/// <param name="ShutdownDiscarded">The shutdown discarded.</param>
/// <param name="Expired">The expired.</param>
/// <param name="Failed">The failed.</param>
/// <param name="Failure">The failure.</param>
/// <param name="FailureDetail">The failure detail.</param>
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
    string FailureDetail)
{
    public int RetainedQuoteItems { get; init; }
    public int MaximumRetainedQuoteItems { get; init; }
    public DateTime? LastAcceptedUtc { get; init; }
    public DateTime? LastPublishedUtc { get; init; }
    public DateTime? FirstFailureUtc { get; init; }
    public DateTime? LastFailureUtc { get; init; }
    public string LastExceptionType { get; init; } = string.Empty;
    public string LastExceptionMessage { get; init; } = string.Empty;
    public string InFlightEventType { get; init; } = string.Empty;
    public string InFlightSubject { get; init; } = string.Empty;
    public int CurrentAttempt { get; init; }
    public TimeSpan NoProgressAge { get; init; }
    public TimeSpan NoProgressResetThreshold { get; init; }
    public bool ResetRequired { get; init; }
}
