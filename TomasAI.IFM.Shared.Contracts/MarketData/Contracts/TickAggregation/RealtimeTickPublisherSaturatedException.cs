namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

/// <summary>The caller retains any quote lease when this nonblocking admission rejection is thrown.</summary>
/// <param name="capacity">The maximum capacity of the buffer, store, or queue.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "Publisher rejection exceptions require a concrete capacity/budget or failure reason; unclassified constructors would discard required diagnostics.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "Publisher rejection exceptions require a concrete capacity/budget or failure reason; unclassified constructors would discard required diagnostics.")]
public sealed class RealtimeTickPublisherSaturatedException(int capacity)
    : InvalidOperationException($"The bounded realtime publisher queue is full (capacity={capacity}); this publication was rejected.")
{
    public int Capacity { get; } = capacity;
}
