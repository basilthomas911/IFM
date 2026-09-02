using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

public interface ISecuritiesDbWriteContext
{
    /// <summary>
    /// Rebuilds the symbol projections. Supplying <paramref name="staleOperationCutoffUtc"/>
    /// explicitly recovers journaled operations at or before that UTC instant.
    /// </summary>
    /// <remarks>
    /// A cutoff may be supplied only after every Securities projection writer has been
    /// drained and the operator has verified that matching processes cannot resume.
    /// Leaving it <see langword="null"/> never removes an unclassified operation.
    /// </remarks>
    Task<SecuritiesProjectionBackfillResult> BackfillSymbolProjectionsAsync(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
        => throw new NotSupportedException();
    Task<SecuritiesProjectionReconciliationResult> ReconcileSymbolProjectionsAsync(
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    Task DeleteFuturesContractAsync(string contractId);
    Task DeleteFuturesContractAsync(FuturesContractId contractId);
    Task DeleteOnTheRunFuturesContractAsync(string symbol);
    Task DeleteFuturesOptionContractAsync(string contractId);
    Task InsertFuturesContractAsync(FuturesContractV3ReadModel futuresContract);
    Task InsertFuturesContractsAsync(ICollection<FuturesContractV3ReadModel> futuresContracts);
    Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract);
    Task InsertFuturesOptionContractsAsync(ICollection<FuturesOptionContractReadModel> futuresOptionContracts);
    Task UpdateFuturesContractAsync(FuturesContractId originalContractId, FuturesContractV3ReadModel futuresContract);
    Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel futuresOptionContract);
}
