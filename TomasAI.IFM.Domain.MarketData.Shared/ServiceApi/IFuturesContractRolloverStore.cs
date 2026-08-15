using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

/// <summary>
/// Persists the master currently-traded futures assignments used at startup and
/// by the future market-day rollover workflow.
/// </summary>
public interface IFuturesContractRolloverStore
{
    Task EnsureFuturesContractRolloverRowsAsync(
        IReadOnlyCollection<string> symbols,
        DateTime createdOnUtc,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task<FuturesContractRolloverReadModel?> GetFuturesContractRolloverAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FuturesContractRolloverReadModel>>
        GetFuturesContractRolloversAsync(
            CancellationToken cancellationToken = default);

    Task<FuturesContractV2ReadModel?> GetPersistedFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken = default);

    Task ReplaceCurrentlyTradedFuturesContractAsync(
        FuturesContractRolloverReadModel rollover,
        FuturesContractV2ReadModel contract,
        CancellationToken cancellationToken = default);
}
