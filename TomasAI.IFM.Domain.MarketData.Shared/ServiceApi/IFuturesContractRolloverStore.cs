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

    Task<FuturesContractV3ReadModel?> GetPersistedFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FuturesContractV3ReadModel>> GetFuturesRolloverSetAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    Task ReplaceOnTheRunFuturesContractAsync(
        FuturesContractRolloverReadModel rollover,
        FuturesContractV3ReadModel contract,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the complete currently-traded set for a root symbol and advances
    /// its rollover authority to the front contract in that set.
    /// </summary>
    Task ReplaceFuturesRolloverSetAsync(
        FuturesContractRolloverReadModel rollover,
        IReadOnlyCollection<FuturesContractV3ReadModel> contracts,
        CancellationToken cancellationToken = default);
}
