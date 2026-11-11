using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Defines mutations supported by the Securities database.</summary>
public interface ISecuritiesDbWriteContext : IFuturesContractRolloverWriteStore
{
    /// <summary>Acquires the caller-selected trusted table lock set for maintenance.</summary>
    /// <param name="transaction">The caller-owned maintenance transaction.</param>
    /// <param name="includeSymbolTables">Whether rebuilding also locks the symbol projections.</param>
    /// <param name="token">Cancels lock acquisition.</param>
    /// <returns>Completion of lock acquisition; commit remains with the caller.</returns>
    Task LockCatalogMaintenanceAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, bool includeSymbolTables, CancellationToken token);

    /// <summary>Writes an exact accepted rollover row set without selecting retired contracts or creating events.</summary>
    /// <param name="transaction">The caller-owned event transaction.</param>
    /// <param name="contracts">The complete changed rows selected by the domain.</param>
    /// <param name="token">Cancels the row writes.</param>
    /// <returns>Completion of the canonical and symbol writes.</returns>
    Task WriteFuturesCatalogRowsAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, IReadOnlyList<FuturesContractV3ReadModel> contracts, CancellationToken token);

    /// <summary>Persists the accepted rollover pointer without choosing its target.</summary>
    /// <param name="transaction">The caller-owned event transaction.</param>
    /// <param name="rollover">The accepted complete pointer values.</param>
    /// <param name="token">Cancels the pointer writes.</param>
    /// <returns>Completion of the pointer writes.</returns>
    Task WriteFuturesRolloverPointerAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, FuturesContractRolloverReadModel rollover, CancellationToken token);

    /// <summary>Materializes accepted futures facts, indexes and immutable reference publication on the source transaction.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="projection">The accepted contract to persist.</param>
    /// <param name="originalContractId">The previous canonical identity when replacing a row.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectFuturesContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        PreparedFuturesProjection projection, string? originalContractId = null, CancellationToken token = default);
    /// <summary>Materializes accepted option facts, indexes and immutable reference publication on the source transaction.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="projection">The accepted contract to persist.</param>
    /// <param name="originalContractId">The previous canonical identity when replacing a row.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectOptionContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        PreparedOptionProjection projection, string? originalContractId = null, CancellationToken token = default);
    /// <summary>Removes canonical and symbol rows without deleting immutable reference evidence.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task RemoveFuturesContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        string contractId,CancellationToken token = default);
    /// <summary>Removes canonical and symbol option rows without deleting immutable reference evidence.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task RemoveOptionContractAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction,
        string contractId,CancellationToken token = default);

    /// <summary>Inserts immutable bundle JSON without replacing an existing publication.</summary>
    /// <param name="bundleId">The exact content digest qualified by the caller.</param>
    /// <param name="payload">The validated bundle JSON.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The asynchronous insertion operation.</returns>
    Task InsertOptionPricingReferenceBundleAsync(string bundleId, string payload, CancellationToken cancellationToken = default);

    /// <summary>Stages the immutable reference version supplied by its publisher.</summary>
    /// <param name="value">The qualified provider identity, digest and accepted payload.</param>
    /// <param name="cancellationToken">Cancels the staging write.</param>
    /// <returns>The exact staged identity and digest.</returns>
    Task<PendingReferenceVersion> StageReferenceVersionAsync(PreparedReferenceVersion value,
        CancellationToken cancellationToken = default);
    /// <summary>Publishes the previously staged reference payload.</summary>
    /// <param name="value">The accepted value supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task CommitReferenceVersionAsync(
        PendingReferenceVersion value,
        CancellationToken cancellationToken = default);
    /// <summary>Atomically persists the caller's complete option definition generation and pointer.</summary>
    /// <param name="publication">The prepared generation, coverage, timestamp and accepted rows.</param>
    /// <param name="cancellationToken">Cancels the publication transaction.</param>
    /// <returns>Completion after the complete generation commits.</returns>
    Task PublishOptionDefinitionsAsync(OptionDefinitionPublication publication, CancellationToken cancellationToken);
    /// <summary>Atomically replaces both symbol tables from their canonical PostgreSQL tables.</summary>
    /// <param name="cancellationToken">Cancels the projection transaction.</param>
    /// <returns>The counts of copied futures and option rows.</returns>
    Task<SecuritiesProjectionBackfillResult> ReplaceSymbolProjectionRowsFromCanonicalAsync(CancellationToken cancellationToken);

    /// <summary>Compares canonical and projected keys to identify missing or extra symbol rows.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<SecuritiesProjectionReconciliationResult> ReconcileSymbolProjectionsAsync(
        CancellationToken cancellationToken = default);
    /// <summary>Deletes the selected futures contract.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteFuturesContractAsync(string contractId);
    /// <summary>Deletes the selected futures contract.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteFuturesContractAsync(FuturesContractId contractId);
    /// <summary>Deletes the selected on the run futures contract.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteOnTheRunFuturesContractAsync(string symbol);
    /// <summary>Deletes the selected futures option contract.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>Completion of the database operation.</returns>
    Task DeleteFuturesOptionContractAsync(string contractId);
    /// <summary>Persists the supplied futures contract.</summary>
    /// <param name="futuresContract">The futures contract supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertFuturesContractAsync(FuturesContractV3ReadModel futuresContract);
    /// <summary>Persists the supplied futures contracts.</summary>
    /// <param name="futuresContracts">The futures contracts supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertFuturesContractsAsync(ICollection<FuturesContractV3ReadModel> futuresContracts);
    /// <summary>Persists the supplied futures option contract.</summary>
    /// <param name="futuresOptionContract">The futures option contract supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract);
    /// <summary>Persists the supplied futures option contracts.</summary>
    /// <param name="futuresOptionContracts">The futures option contracts supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertFuturesOptionContractsAsync(ICollection<FuturesOptionContractReadModel> futuresOptionContracts);
    /// <summary>Updates the persisted futures contract with the supplied values.</summary>
    /// <param name="originalContractId">The previous canonical identity when replacing a row.</param>
    /// <param name="futuresContract">The futures contract supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateFuturesContractAsync(FuturesContractId originalContractId, FuturesContractV3ReadModel futuresContract);
    /// <summary>Updates the persisted futures option contract with the supplied values.</summary>
    /// <param name="originalContractId">The previous canonical identity when replacing a row.</param>
    /// <param name="futuresOptionContract">The futures option contract supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel futuresOptionContract);
}
