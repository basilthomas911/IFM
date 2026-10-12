namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public readonly record struct MarketDataProjectionScopeMutationReadModel(
    string ProjectionName,
    string ScopeKey,
    Guid MutationId,
    DateTime StartedOn)
{
    public bool IsFailed => StartedOn == DateTime.UnixEpoch;
}
