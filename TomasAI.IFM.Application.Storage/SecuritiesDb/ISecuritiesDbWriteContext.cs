using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Defines mutations supported by the Securities database.</summary>
public interface ISecuritiesDbWriteContext
{
    /// <summary>Materializes accepted futures facts, indexes and immutable reference publication on the source transaction.</summary>
    Task ProjectFuturesContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        FuturesContractV3ReadModel contract, string? originalContractId = null, CancellationToken token = default);
    /// <summary>Materializes accepted option facts, indexes and immutable reference publication on the source transaction.</summary>
    Task ProjectOptionContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        FuturesOptionContractReadModel contract, string? originalContractId = null, CancellationToken token = default);
    /// <summary>Removes canonical and symbol rows without deleting immutable reference evidence.</summary>
    Task RemoveFuturesContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        string contractId,CancellationToken token = default);
    /// <summary>Removes canonical and symbol option rows without deleting immutable reference evidence.</summary>
    Task RemoveOptionContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        string contractId,CancellationToken token = default);

    /// <summary>Inserts immutable bundle JSON without replacing an existing publication.</summary>
    /// <param name="bundleId">The exact content digest qualified by the caller.</param>
    /// <param name="payload">The validated bundle JSON.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The asynchronous insertion operation.</returns>
    Task InsertOptionPricingReferenceBundleAsync(string bundleId, string payload, CancellationToken cancellationToken = default);

    Task<PendingReferenceVersion> StageReferenceVersionAsync(
        FuturesContractV3ReadModel value,
        CancellationToken cancellationToken = default);
    Task<PendingReferenceVersion> StageReferenceVersionAsync(
        FuturesOptionContractReadModel value,
        CancellationToken cancellationToken = default);
    Task CommitReferenceVersionAsync(
        PendingReferenceVersion value,
        CancellationToken cancellationToken = default);
    Task ReplaceOptionContractDefinitionsAsync(
        string symbol, DateOnly coverageFrom, DateOnly coverageThrough,
        IReadOnlyCollection<CachedOptionContractDefinitionReadModel> definitions,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Rebuilds the symbol projections. Supplying <paramref name="staleOperationCutoffUtc"/>
    /// explicitly recovers journaled operations at or before that UTC instant.
    /// </summary>
    /// <remarks>
    /// A cutoff may be supplied only after every Securities projection writer has been
    /// drained and the operator has verified that matching processes cannot resume.
    /// Leaving it <see langword="null"/> never removes an unclassified operation.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    Task<SecuritiesProjectionBackfillResult> BackfillSymbolProjectionsAsync(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null);
    Task<SecuritiesProjectionReconciliationResult> ReconcileSymbolProjectionsAsync(
        CancellationToken cancellationToken = default);
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
