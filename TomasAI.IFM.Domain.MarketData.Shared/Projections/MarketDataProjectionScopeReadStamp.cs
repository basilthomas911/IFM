namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Captures the global and scoped generations used to validate a projection read.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
public readonly record struct MarketDataProjectionScopeReadStamp(
    string ProjectionName,
    Guid GlobalGeneration,
    MarketDataProjectionScopeGeneration[] Scopes);
