using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

/// <summary>Prepares and submits futures rollover changes through the owning catalog actor.</summary>
public interface IFuturesContractRolloverStore : IFuturesContractRolloverReadStore, IFuturesContractRolloverWriteStore
{
    /// <summary>Persists an accepted front contract and its rollover assignment.</summary>
    /// <param name="rollover">The accepted symbol assignment.</param>
    /// <param name="contract">The accepted front-contract definition.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of the assignment and contract writes.</returns>
    Task ReplaceOnTheRunFuturesContractAsync(FuturesContractRolloverReadModel rollover, FuturesContractV3ReadModel contract, CancellationToken cancellationToken = default);

    /// <summary>Persists an accepted currently traded set and its front-contract assignment.</summary>
    /// <param name="rollover">The accepted symbol assignment.</param>
    /// <param name="contracts">The accepted complete currently traded contract set.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of the atomic replacement.</returns>
    Task ReplaceFuturesRolloverSetAsync(FuturesContractRolloverReadModel rollover, IReadOnlyCollection<FuturesContractV3ReadModel> contracts, CancellationToken cancellationToken = default);
}
