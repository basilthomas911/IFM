using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>
/// Defines read-only operations for retrieving futures and futures option contract data from the securities database.
/// </summary>
/// <remarks>This interface provides asynchronous methods to query currently traded and historical futures
/// contracts, as well as futures option contracts, by symbol, contract identifier, or in bulk. Implementations are
/// expected to provide efficient, read-only access to contract information for use in trading, analytics, or reporting
/// scenarios.</remarks>
public interface ISecuritiesDbReadContext : ICurrentFuturesContractCatalog, IFuturesContractRolloverReadStore
{
    /// <summary>Reads a committed stream revision for optimistic command preparation without creating metadata.</summary>
    /// <param name="stream">The exact stream name selected by the producer.</param>
    /// <param name="token">Cancels the metadata read.</param>
    /// <returns>The committed revision, or zero when the stream does not exist.</returns>
    Task<long> ReadCatalogRevisionAsync(string stream, CancellationToken token);

    /// <summary>Reads canonical rows under an already acquired maintenance lock.</summary>
    /// <param name="transaction">The caller-owned maintenance transaction.</param>
    /// <param name="token">Cancels all row reads.</param>
    /// <returns>The persisted canonical definitions and rollover pointers.</returns>
    Task<SecuritiesCatalogRows> ReadCatalogRowsAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, CancellationToken token);

    /// <summary>Reads original source events for caller-selected maintenance scopes.</summary>
    /// <param name="transaction">The caller-owned maintenance transaction.</param>
    /// <param name="exactStreams">The exact stream identities to include.</param>
    /// <param name="prefixes">The explicitly selected legacy stream prefixes.</param>
    /// <param name="token">Cancels the ordered source query.</param>
    /// <returns>Persisted events with stream names in global event revision order.</returns>
    Task<IReadOnlyList<TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamSourceReadModel>> ReadCatalogSourcesAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, string[] exactStreams, string[] prefixes, CancellationToken token);

    /// <summary>Checks for committed events in legacy source scopes selected by the caller.</summary>
    /// <param name="transaction">The caller-owned event transaction.</param>
    /// <param name="excludedStreams">Exact consolidated stream identities excluded from the lookup.</param>
    /// <param name="prefixes">Legacy stream prefixes to test.</param>
    /// <param name="token">Cancels the lookup.</param>
    /// <returns>True when a matching stream has committed events.</returns>
    Task<bool> HasLegacyCatalogHistoryAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, string[] excludedStreams, string[] prefixes, CancellationToken token);

    /// <summary>Checks committed stream presence without creating a stream.</summary>
    /// <param name="transaction">The caller-owned maintenance transaction.</param>
    /// <param name="stream">The exact stream identity.</param>
    /// <param name="token">Cancels the lookup.</param>
    /// <returns>True when the stream has committed events.</returns>
    Task<bool> HasCatalogSourceStreamAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, string stream, CancellationToken token);

    /// <summary>Reports whether canonical or symbol tables contain rows.</summary>
    /// <param name="transaction">The caller-owned maintenance transaction.</param>
    /// <param name="token">Cancels the query.</param>
    /// <returns>True when at least one projection row exists.</returns>
    Task<bool> CatalogProjectionHasRowsAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, CancellationToken token);

    /// <summary>Resolves option identifiers against immutable provider ownership before generation publication.</summary>
    /// <param name="rows">The rows supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<CachedOptionContractDefinitionReadModel[]> ResolveOptionReferenceIdentitiesAsync(
        IReadOnlyList<CachedOptionContractDefinitionReadModel> rows, CancellationToken cancellationToken)
        => throw new NotSupportedException("Option identity reconciliation requires the PostgreSQL securities context.");

    /// <summary>Reads the immutable bundle JSON by its content digest.</summary>
    /// <param name="bundleId">The exact content digest qualified by the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored JSON, or null when absent.</returns>
    Task<string?> GetOptionPricingReferenceBundlePayloadAsync(string bundleId, CancellationToken cancellationToken = default);

    /// <summary>Reads exactly the requested immutable reference versions in request order.</summary>
    /// <param name="identities">The canonical contract and mapping version pairs.</param>
    /// <param name="cancellationToken">Cancels the batch read.</param>
    /// <returns>One version per input identity, with null for missing or unpublished versions.</returns>
    Task<IReadOnlyList<ReferenceContractVersion?>> GetReferenceVersionsAsync(
        IReadOnlyList<(string ContractId, string Version)> identities, CancellationToken cancellationToken);

    /// <summary>Reads reference version from the committed projection.</summary>
    /// <param name="identity">The caller-qualified exact contract and revision.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<ReferenceContractVersion?> GetReferenceVersionAsync(
        ExactReferenceVersion identity,
        CancellationToken cancellationToken = default);
    /// <summary>Contains reference version.</summary>
    /// <param name="identity">The caller-qualified exact contract and revision.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    Task<bool> ContainsReferenceVersionAsync(
        ExactReferenceVersion identity,
        CancellationToken cancellationToken = default);
    /// <summary>List reference versions.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="afterVersion">The exclusive version cursor for the next page.</param>
    /// <param name="limit">The maximum number of returned rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsAsync(
        string contractId,
        string afterVersion = "",
        int limit = 100,
        CancellationToken cancellationToken = default);
    /// <summary>Reads option contract expiries from the committed projection.</summary>
    /// <param name="range">The canonical root and inclusive expiry boundaries.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<IReadOnlyList<OptionContractExpiryReadModel>> GetOptionContractExpiriesAsync(
        OptionDefinitionExpiryRange range,
        CancellationToken cancellationToken = default);
    /// <summary>Reads cached option contract definitions from the committed projection.</summary>
    /// <param name="partition">The caller-qualified option definition partition.</param>
    /// <param name="providerRoots">The allowed provider roots, or null to include all roots.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> GetCachedOptionContractDefinitionsAsync(
        OptionDefinitionPartition partition,
        IReadOnlyCollection<string>? providerRoots = null,
        CancellationToken cancellationToken = default);
    /// <summary>Reads the current immutable definition generation pointer.</summary>
    /// <param name="symbol">Normalized underlying root.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The active generation and coverage, or null when unpublished.</returns>
    Task<OptionDefinitionGenerationReadModel?> GetOptionDefinitionGenerationAsync(string symbol, CancellationToken cancellationToken);

    /// <summary>Reads expiries from the exact previously selected generation.</summary>
    /// <param name="symbol">Normalized underlying root.</param>
    /// <param name="first">Inclusive start expiry.</param>
    /// <param name="last">Inclusive end expiry.</param>
    /// <param name="generation">The publication and coverage selected by the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Matching expiries, including the nearest following expiry when available.</returns>
    Task<IReadOnlyList<OptionContractExpiryReadModel>> GetOptionDefinitionExpiriesAsync(
        string symbol, DateOnly first, DateOnly last, OptionDefinitionGenerationReadModel generation, CancellationToken cancellationToken);

    /// <summary>Streams the selected immutable partitions in expiry order.</summary>
    /// <param name="symbol">Normalized underlying root.</param>
    /// <param name="generation">Exact published generation.</param>
    /// <param name="expiries">The selected partition dates.</param>
    /// <param name="cancellationToken">Cancels the stream.</param>
    /// <returns>Rows for the selected partitions only.</returns>
    IAsyncEnumerable<CachedOptionContractDefinitionReadModel> ReadOptionDefinitionPartitionsAsync(
        string symbol, Guid generation, DateOnly[] expiries, CancellationToken cancellationToken);

    /// <summary>Reads the qualified expiry range from one published definition generation.</summary>
    /// <param name="range">The caller-qualified root, expiry bounds and capacity.</param>
    /// <param name="providerRoots">The allowed provider roots, or null to include all roots.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>An asynchronous stream of the selected rows.</returns>
    IAsyncEnumerable<CachedOptionContractDefinitionReadModel> ReadPublishedOptionDefinitionsAsync(
        OptionDefinitionExpiryRange range,
        IReadOnlyCollection<string>? providerRoots = null,
        CancellationToken cancellationToken = default);
    /// <summary>Reads on the run futures contract from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol);
    /// <summary>Reads on the run futures contract from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol, CancellationToken cancellationToken);
    /// <summary>Reads rollover futures contracts from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol);
    /// <summary>Reads rollover futures contracts from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol, CancellationToken cancellationToken);
    /// <summary>Reads futures contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId);
    /// <summary>Reads futures contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId, CancellationToken cancellationToken);
    /// <summary>Reads futures contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId contractId);
    /// <summary>Reads futures contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId contractId, CancellationToken cancellationToken);
    /// <summary>Reads futures contracts from the committed projection.</summary>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync();
    /// <summary>Reads futures contracts from the committed projection.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync(CancellationToken cancellationToken);
    /// <summary>Reads futures contracts by symbol from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol);
    /// <summary>Reads futures contracts by symbol from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol, CancellationToken cancellationToken);
    /// <summary>Reads futures contracts by ids from the committed projection.</summary>
    /// <param name="contractIds">The canonical contract identities to include.</param>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol);
    /// <summary>Reads futures contracts by ids from the committed projection.</summary>
    /// <param name="contractIds">The canonical contract identities to include.</param>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol, CancellationToken cancellationToken);
    /// <summary>Reads futures option contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId);
    /// <summary>Reads futures option contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId, CancellationToken cancellationToken);
    /// <summary>Reads futures option contracts by ids from the committed projection.</summary>
    /// <param name="contractIds">The canonical contract identities to include.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(ICollection<string> contractIds);
    /// <summary>Reads futures option contracts by ids from the committed projection.</summary>
    /// <param name="contractIds">The canonical contract identities to include.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(ICollection<string> contractIds, CancellationToken cancellationToken);
    /// <summary>Reads futures option contracts page from the committed projection.</summary>
    /// <param name="request">The caller's typed selection, including its filtering and page bounds.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<FuturesOptionContractPageReadModel> GetFuturesOptionContractsPageAsync(
        QualifiedOptionCatalogPage request,
        CancellationToken cancellationToken = default);
    /// <summary>Reads futures option contracts from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol);
    /// <summary>Reads futures option contracts from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol, CancellationToken cancellationToken);
    /// <summary>Reads futures option contracts from the committed projection.</summary>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync();
    /// <summary>Reads futures option contracts from the committed projection.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(CancellationToken cancellationToken);
}
