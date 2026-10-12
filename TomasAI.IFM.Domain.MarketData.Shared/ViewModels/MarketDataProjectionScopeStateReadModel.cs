namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public readonly record struct MarketDataProjectionScopeStateReadModel(
    string ProjectionName,
    string ScopeKey,
    Guid Generation,
    bool IsReady,
    bool Blocked,
    bool ActiveOperationsEmpty)
{
    public bool CanRead => IsReady && !Blocked && ActiveOperationsEmpty;
}
