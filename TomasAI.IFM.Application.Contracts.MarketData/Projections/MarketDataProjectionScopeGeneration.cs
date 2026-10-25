namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Identifies the generation observed for one market-data projection scope.
/// </summary>
public readonly record struct MarketDataProjectionScopeGeneration(
    string ScopeKey,
    Guid Generation,
    bool IsMissing);
