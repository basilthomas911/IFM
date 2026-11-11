using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

/// <summary>Reads persisted futures definitions and current rollover assignments.</summary>
public interface IFuturesContractRolloverReadStore
{
    /// <summary>Reads one symbol's persisted rollover assignment.</summary>
    /// <param name="symbol">The validated futures root symbol.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The assignment, or null when none is persisted.</returns>
    Task<FuturesContractRolloverReadModel?> GetFuturesContractRolloverAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>Reads all persisted rollover assignments.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The persisted symbol assignments.</returns>
    Task<IReadOnlyCollection<FuturesContractRolloverReadModel>> GetFuturesContractRolloversAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the canonical persisted definition by contract identity.</summary>
    /// <param name="contractId">The validated canonical contract identity.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The contract, or null when no canonical row exists.</returns>
    Task<FuturesContractV3ReadModel?> GetPersistedFuturesContractAsync(string contractId, CancellationToken cancellationToken = default);

    /// <summary>Reads the persisted currently traded set for one symbol.</summary>
    /// <param name="symbol">The validated futures root symbol.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The persisted rollover set.</returns>
    Task<IReadOnlyCollection<FuturesContractV3ReadModel>> GetFuturesRolloverSetAsync(string symbol, CancellationToken cancellationToken = default);
}
