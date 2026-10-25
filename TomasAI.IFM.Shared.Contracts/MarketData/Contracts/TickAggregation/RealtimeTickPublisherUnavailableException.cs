namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

/// <summary>Initializes a new RealtimeTickPublisherUnavailableException instance.</summary>
/// <param name="reason">The reason the publisher is unavailable.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "Publisher rejection exceptions require a concrete capacity/budget or failure reason; unclassified constructors would discard required diagnostics.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "Publisher rejection exceptions require a concrete capacity/budget or failure reason; unclassified constructors would discard required diagnostics.")]
public sealed class RealtimeTickPublisherUnavailableException(string reason)
    : InvalidOperationException($"The bounded realtime publisher is unavailable: {reason}");
