using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

/// <summary>
/// Identifies one workflow-owned registration for a transient ticker-data stream.
/// </summary>
/// <param name="WorkflowType">The type of workflow that owns the stream.</param>
/// <param name="WorkflowId">The identifier of the workflow that owns the stream.</param>
/// <param name="LegId">The identifier of the strategy leg that owns the stream.</param>
public readonly record struct TickerStreamOwner(
    string WorkflowType,
    string WorkflowId,
    string LegId)
{
    /// <summary>
    /// Validates that every component required for deterministic stream ownership is present.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This validates current instance properties rather than method parameters; existing exception semantics identify invalid ownership or policy state.")]
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkflowType)) throw new ArgumentException("Ticker ownership component is required: WorkflowType");
        if (string.IsNullOrWhiteSpace(WorkflowId)) throw new ArgumentException("Ticker ownership component is required: WorkflowId");
        if (string.IsNullOrWhiteSpace(LegId)) throw new ArgumentException("Ticker ownership component is required: LegId");
    }
}
