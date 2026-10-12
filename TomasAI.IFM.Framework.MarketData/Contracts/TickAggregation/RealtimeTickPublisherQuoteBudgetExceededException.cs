namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

/// <summary>Initializes a new RealtimeTickPublisherQuoteBudgetExceededException instance.</summary>
/// <param name="maximumItems">The maximum admitted quote item count.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "Publisher rejection exceptions require a concrete capacity/budget or failure reason; unclassified constructors would discard required diagnostics.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "Publisher rejection exceptions require a concrete capacity/budget or failure reason; unclassified constructors would discard required diagnostics.")]
public sealed class RealtimeTickPublisherQuoteBudgetExceededException(int maximumItems)
    : InvalidOperationException($"The bounded realtime publisher has reached its retained quote-item limit ({maximumItems}); this publication was rejected.")
{
    public int MaximumItems { get; } = maximumItems;
}
