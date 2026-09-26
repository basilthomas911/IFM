namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public readonly record struct MarketDataProjectionStateReadModel(
    string ProjectionName,
    Guid Generation,
    bool IsReady);

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

public readonly record struct MarketDataProjectionScopeMutationReadModel(
    string ProjectionName,
    string ScopeKey,
    Guid MutationId,
    DateTime StartedOn)
{
    public bool IsFailed => StartedOn == DateTime.UnixEpoch;
}

public readonly record struct MarketDataProjectionMutationReadModel(
    Guid MutationId,
    DateTime StartedOn)
{
    public bool IsFailed => StartedOn == DateTime.UnixEpoch;
}

public readonly record struct VixFuturesContractIndexReadModel(
    int Bucket,
    string ContractId);
