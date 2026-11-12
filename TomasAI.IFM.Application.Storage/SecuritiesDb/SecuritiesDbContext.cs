using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>
/// Provides Securities database read and write operations.
/// </summary>
/// <param name="connectionSettings">The database connection settings.</param>
/// <param name="dbFactory">The application database-context factory.</param>
/// <param name="logger">The database provider logger.</param>
public sealed class SecuritiesDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger)
    : ObjectDataRepository<SecuritiesDbContext>(connectionSettings[SecuritiesDbConnection] is { ProviderName: "System.Data.Postgres" } postgresConnection
        ? postgresConnection : throw new NotSupportedException("Securities persistence requires PostgreSQL; ScyllaDB is reserved for raw ticks."), logger), ISecuritiesDbContext
{
    /// <inheritdoc />
    public Task ProjectFuturesContractAsync(IEnlistedPostgresTransaction transaction, PreparedFuturesProjection projection,
        string? originalContractId = null, CancellationToken token = default)
        => this.ProjectFuturesContractAsyncCoreBinding(transaction, projection, originalContractId, token);
    /// <inheritdoc />
    public Task ProjectOptionContractAsync(IEnlistedPostgresTransaction transaction, PreparedOptionProjection projection,
        string? originalContractId = null, CancellationToken token = default)
        => this.ProjectOptionContractAsyncCoreBinding(transaction, projection, originalContractId, token);
    /// <inheritdoc />
    public Task RemoveFuturesContractAsync(EventSourceDb.IEnlistedPostgresTransaction transaction,string contractId,CancellationToken token = default)
=>this.RemoveFuturesContractAsyncCoreBinding(transaction, contractId, token);
    /// <inheritdoc />
    public Task RemoveOptionContractAsync(EventSourceDb.IEnlistedPostgresTransaction transaction,string contractId,CancellationToken token = default)
=>this.RemoveOptionContractAsyncCoreBinding(transaction, contractId, token);

    internal readonly PostgresSecuritiesReferenceStore NativeReferences = new();


    /// <inheritdoc />
    public Task<string?> GetOptionPricingReferenceBundlePayloadAsync(string bundleId, CancellationToken cancellationToken = default)
        => this.GetOptionPricingReferenceBundlePayloadCoreAsync(bundleId, cancellationToken);

    /// <inheritdoc />
    public Task InsertOptionPricingReferenceBundleAsync(string bundleId, string payload, CancellationToken cancellationToken = default)
        => this.InsertOptionPricingReferenceBundleCoreAsync(bundleId, payload, cancellationToken);

    /// <summary>Maps the bundle JSON at the stable payload ordinal.</summary>
    /// <param name="row">The database result row.</param>
    /// <returns>The persisted JSON.</returns>
    internal static string MapToOptionPricingReferenceBundlePayload(IObjectDataRecord row) => row.GetString(0);

    public const string SecuritiesDbConnection = "SecuritiesDbConnection";
    internal const string FuturesContractSymbolProjection = "futures_contract_by_symbol";
    internal const string FuturesOptionContractSymbolProjection = "futures_option_contract_by_symbol";
    internal const int CompletionStateLookupBatchSize = 100;
    internal const string GlobalProjectionOperationScope = "global";
    internal const string ProjectionOperationScopeCount = "scope-count";
    internal const string SymbolProjectionOperationScope = "symbol";
    internal readonly IDbContextFactory DbFactory = dbFactory;

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override SecuritiesDbContext Database => this;

    /// <summary>Gets the read contract implemented by this context.</summary>
    public ISecuritiesDbReadContext DbReader => this;

    /// <summary>Gets the write contract implemented by this context.</summary>
    public ISecuritiesDbWriteContext DbWriter => this;

    internal static FuturesContractV3ReadModel MapToFuturesContract<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => ReferencePayloadCodec.ReadFuture(e.IsNull(11) ? null : e.GetBytes(11), new(
            contractId: e.GetString(0),
            description: e.GetString(1),
            symbol: e.GetString(2),
            localSymbol: e.GetString(3),
            securityType: e.GetString(4),
            currency: e.GetString(5),
            exchange: e.GetString(6),
            multiplier: e.GetString(7),
            lastTradeDate: e.GetDateOnly(8),
            onTheRun: e.GetBool(9),
            rollover: e.GetBool(10)
        ));

    internal static FuturesOptionContractReadModel MapToFuturesOptionContract<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => ReferencePayloadCodec.ReadOption(e.IsNull(11) ? null : e.GetBytes(11), new(
            contractId: e.GetString(0),
            description: e.GetString(1),
            symbol: e.GetString(2),
            localSymbol: e.GetString(3),
            securityType: e.GetString(4),
            currency: e.GetString(5),
            exchange: e.GetString(6),
            multiplier: e.GetString(7),
            contractMonth: e.GetDateOnly(8),
            strikePrice: e.GetDouble(9),
            optionType: e.GetString(10)
        ));

    internal static FuturesContractRolloverReadModel MapToFuturesContractRollover<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new()
        {
            Symbol = e.GetString(0),
            ContractId = e.IsNull(1) ? null : e.GetString(1),
            NextRolloverDate = e.IsNull(2) ? null : e.GetDateOnly(2),
            UpdatedOn = e.IsNull(3) ? null : e.GetDateTime(3),
            UpdatedBy = e.IsNull(4) ? null : e.GetString(4),
            CreatedOn = e.GetDateTime(5),
            CreatedBy = e.GetString(6)
        };

    internal static FuturesContractProjectionKey MapToFuturesContractProjectionKey<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetString(0), e.GetBool(1), e.GetBool(2), e.GetDateOnly(3), e.GetString(4));

    internal static FuturesOptionContractProjectionKey MapToFuturesOptionContractProjectionKey<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetString(0), e.GetDateOnly(1), e.GetString(2), e.GetDouble(3), e.GetString(4));




    internal static bool MapToBoolean(IObjectDataRecord e)
        => e.GetBool(0);

    internal static ProjectionState MapToProjectionState(IObjectDataRecord e)
        => new(e.GetGuid(0), e.GetBool(1), e.IsCollectionEmpty(2));

    internal static SymbolProjectionState MapToSymbolProjectionState(IObjectDataRecord e)
        => new(e.GetString(0), e.GetGuid(1), e.GetBool(2), e.IsCollectionEmpty(3));

    internal static ProjectionOperationJournalEntry MapToProjectionOperationJournalEntry(IObjectDataRecord e)
        => new(e.GetGuid(0), e.GetDateTime(1), e.GetBool(2));

    internal static ProjectionOperationScope MapToProjectionOperationScope(IObjectDataRecord e)
        => new(e.GetString(0), e.GetString(1));

    internal static string MapToReferenceIdentityBinding(IObjectDataRecord row)
        => row.GetString(0);

    internal static ReferenceContractVersionSummaryReadModel MapToReferenceVersionSummary(IObjectDataRecord row)
        => new(row.GetString(0), row.GetBool(1));

    internal static ReferenceVersionStorageRow MapToReferenceVersionStorageRow(IObjectDataRecord row)
    {
        var bytes = row.GetBytes(2);
        if (bytes.Length is 0 or > 131072)
            throw new InvalidDataException("Reference version payload exceeds its size bound.");
        var value = MessagePackBinarySerializer.Shared.Deserialize<ReferenceContractVersion>(bytes)
            ?? throw new InvalidDataException("Reference version payload is missing.");
        return new(row.GetString(0), row.GetBool(1), value);
    }

    /// <summary>Stages the immutable reference version supplied by its publisher.</summary>
    /// <param name="value">The qualified provider identity, digest and accepted payload.</param>
    /// <param name="cancellationToken">Cancels the staging write.</param>
    /// <returns>The exact staged identity and digest.</returns>
    public Task<PendingReferenceVersion> StageReferenceVersionAsync(PreparedReferenceVersion value,
        CancellationToken cancellationToken = default)
        => this.StageReferenceVersionCoreAsync(value, cancellationToken);
    /// <summary>Publishes a previously staged immutable reference version.</summary>
    /// <param name="value">The accepted value supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public Task CommitReferenceVersionAsync(
        PendingReferenceVersion value,
        CancellationToken cancellationToken = default)
=>this.CommitReferenceVersionAsyncCoreBinding(value, cancellationToken);
    /// <summary>Gets a published immutable reference version.</summary>
    /// <param name="identity">The caller-qualified exact contract and revision.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public Task<ReferenceContractVersion?> GetReferenceVersionAsync(
        ExactReferenceVersion identity,
        CancellationToken cancellationToken = default)
        => this.GetReferenceVersionCoreAsync(identity.ContractId, identity.Version, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceContractVersion?>> GetReferenceVersionsAsync(
        IReadOnlyList<(string ContractId, string Version)> identities, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identities.Count == 0) return [];
        var ids = identities.Select(key => key.ContractId).ToArray();
        var revisions = identities.Select(key => key.Version).ToArray();
        var rows = await this.NativeTransaction().ExecuteOnceAsync(
            (db, token) => db.QueryAsync(SecuritiesDbSql.DbContext_GetManyAsync_1, (object? [])new ReadExactReferenceVersions(ids, revisions).Bind(), MapToExactReferenceVersionRow, token),
            cancellationToken)
            .ConfigureAwait(false);
        var versions = new Dictionary<(string ContractId, string Version), ReferenceContractVersion>(rows.Count);
        foreach (var row in rows)
        {
            var decoded = this.DecodeReferenceVersion(row.Value.Digest, row.Value.Published, row.Value.Payload);
            if (this.ValidatePublishedReferenceVersion(row.Key.ContractId, decoded) is { } published)
                versions[row.Key] = published;
        }
        return identities.Select(identity => versions.GetValueOrDefault(identity)).ToArray();
    }

    internal static (string Key, string Binding) MapToIdentityClaim(IObjectDataRecord row)
        => (row.GetString(0), row.GetString(1));

    internal static ReferenceContractVersionSummaryReadModel MapToReferenceVersionSummary(System.Data.Common.DbDataReader row)
        => new(row.GetString(0), row.GetBoolean(1));

    internal static (string Digest, bool Published, byte[] Payload) MapToReferencePayload(System.Data.Common.DbDataReader row)
        => (row.GetString(0), row.GetBoolean(1), row.GetFieldValue<byte[]>(2));

    internal static ExactReferenceVersionRow MapToExactReferenceVersionRow(System.Data.Common.DbDataReader row)
        => new((row.GetString(0), row.GetString(1)), (row.GetString(2), row.GetBoolean(3), row.GetFieldValue<byte[]>(4)));

    /// <summary>Gets whether a staged or published immutable reference version exists.</summary>
    /// <param name="identity">The caller-qualified exact contract and revision.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    public Task<bool> ContainsReferenceVersionAsync(
        ExactReferenceVersion identity,
        CancellationToken cancellationToken = default)
        => this.ContainsReferenceVersionCoreAsync(identity.ContractId, identity.Version, cancellationToken);

    /// <summary>Lists a bounded page of staged and published reference versions.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="afterVersion">The exclusive version cursor for the next page.</param>
    /// <param name="limit">The maximum number of returned rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    public Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsAsync(
        string contractId,
        string afterVersion = "",
        int limit = 100,
        CancellationToken cancellationToken = default)
=>this.ListReferenceVersionsAsyncCoreBinding(contractId, afterVersion, limit, cancellationToken);

























    /// <inheritdoc/>
    public Task<SecuritiesProjectionBackfillResult> ReplaceSymbolProjectionRowsFromCanonicalAsync(CancellationToken cancellationToken)
        => this.ReplaceSymbolProjectionRowsFromCanonicalCoreAsync(cancellationToken);

    /// <summary>
    /// Streams canonical and projected primary keys and reports missing or unexpected projection rows.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    public async Task<SecuritiesProjectionReconciliationResult> ReconcileSymbolProjectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var inventory = await (this.ReadProjectionInventoryAsync(DbFactory.SecuritiesDb, cancellationToken))
                .ConfigureAwait(false);
        return new SecuritiesProjectionReconciliationResult(
            inventory.FuturesContractSourceRows,
            inventory.FuturesContractTargetRows,
            this.CountMissing(inventory.FuturesContractSourceKeys, inventory.FuturesContractTargetKeys),
            this.CountMissing(inventory.FuturesContractTargetKeys, inventory.FuturesContractSourceKeys),
            inventory.FuturesOptionContractSourceRows,
            inventory.FuturesOptionContractTargetRows,
            this.CountMissing(inventory.FuturesOptionContractSourceKeys, inventory.FuturesOptionContractTargetKeys),
            this.CountMissing(inventory.FuturesOptionContractTargetKeys, inventory.FuturesOptionContractSourceKeys));
    }



    /// <summary>
    /// Insert a new futures contract into SecuritiesDb
    /// </summary>
    /// <param name="futuresContract">The futures contract to insert</param>
    /// <returns>Completion of the database operation.</returns>
    public Task InsertFuturesContractAsync(FuturesContractV3ReadModel futuresContract)
=>this.InsertFuturesContractAsyncCoreBinding(futuresContract);
    /// <summary>
    /// Asynchronously inserts a collection of futures contracts into the database.
    /// </summary>
    /// <remarks>This method uses the database factory to execute an insert command for each futures contract
    /// in the provided collection. Ensure that the collection is not null and contains valid futures contract data to
    /// avoid exceptions during execution.</remarks>
    /// <param name="futuresContracts">A collection of <see cref="FuturesContractV3ReadModel"/> objects representing the futures contracts to be
    /// inserted. Each contract must have valid properties set, such as contract ID, description, symbol, and other
    /// relevant details.</param>
    /// <returns>Completion of the database operation.</returns>
    public Task InsertFuturesContractsAsync(ICollection<FuturesContractV3ReadModel> futuresContracts)
=>this.InsertFuturesContractsAsyncCoreBinding(futuresContracts);
    /// <summary>
    /// Update an existing futures contract in SecuritiesDb
    /// </summary>
    /// <param name="e">The ID of the futures contract to update</param>
    /// <param name="futuresContract">The futures contract to update</param>
    /// <returns>Completion of the database operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task UpdateFuturesContractAsync(FuturesContractId e, FuturesContractV3ReadModel futuresContract)
=>this.UpdateFuturesContractAsyncCoreBinding(e, futuresContract);
    /// Delete a futures contract from SecuritiesDb by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to delete</param>
    /// <returns>Completion of the database operation.</returns>
    /// <summary>Deletes the selected futures contract.</summary>
    public async Task DeleteFuturesContractAsync(string contractId)
    {
        var db = DbFactory.SecuritiesDb;
        var preparedParameters1 = new GetFuturesContract(contractId);
        var contracts = await (db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContract)}", SecuritiesDbSql.GetFuturesContract)
            .SetParameters(preparedParameters1)
            .ExecuteQueryAsync(MapToFuturesContract!))
                .ConfigureAwait(false);
        var preparedParameters2 = new DeleteFuturesContract(contractId);
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContract)}", SecuritiesDbSql.DeleteFuturesContract)
            .SetParameters(preparedParameters2)
            .QueueCommand()
        ];
        queuedCommands.AddRange(contracts.Select(contract =>
        {
            var key = new DeleteFuturesContractBySymbolV3(
                contract.Symbol, contract.Rollover, contract.OnTheRun, contract.LastTradeDate, contract.ContractId);
            return db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbSql.DeleteFuturesContractBySymbolV3)
            .SetParameters(key)
            .QueueCommand();
        }));
        await (this.ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            contracts.Select(static contract => contract.Symbol),
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a futures contract from the database asynchronously.
    /// </summary>
    /// <remarks>This method removes the specified futures contract from the database. Ensure that the
    /// provided  <paramref name="e"/> contains valid and complete information for the contract to be deleted.</remarks>
    /// <param name="e">The identifier of the futures contract to delete, including the contract ID, symbol, and maturity date.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task DeleteFuturesContractAsync(FuturesContractId e)
=>this.DeleteFuturesContractAsyncCoreBinding(e);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task DeleteOnTheRunFuturesContractAsync(string symbol)
    {
        var fc = await (GetOnTheRunFuturesContractAsync(symbol))
                .ConfigureAwait(false);
        if (fc is not null)
            await (DeleteFuturesContractAsync(fc.Id))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Get currently traded futures contract from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    public async Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            () => db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetOnTheRunFuturesContract)}", SecuritiesDbSql.GetOnTheRunFuturesContract)
            .SetParameters(new GetOnTheRunFuturesContract(symbol))
            .ExecuteSingleAsync(MapToFuturesContract!)))
                .ConfigureAwait(false);
    }

    /// <summary>Reads on the run futures contract from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            token => db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetOnTheRunFuturesContract)}", SecuritiesDbSql.GetOnTheRunFuturesContract)
            .SetParameters(new GetOnTheRunFuturesContract(symbol))
            .ExecuteSingleAsync(MapToFuturesContract!, token),            cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Get currently traded futures contracts from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            () => db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetRolloverFuturesContracts)}", SecuritiesDbSql.GetRolloverFuturesContracts)
            .SetParameters(new GetRolloverFuturesContracts(symbol))
            .ExecuteQueryAsync(MapToFuturesContract!)))
                .ConfigureAwait(false);
    }

    /// <summary>Reads rollover futures contracts from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            token => db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetRolloverFuturesContracts)}", SecuritiesDbSql.GetRolloverFuturesContracts)
            .SetParameters(new GetRolloverFuturesContracts(symbol))
            .ExecuteQueryAsync(MapToFuturesContract!, token),            cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>Reads futures rollover set from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<IReadOnlyCollection<FuturesContractV3ReadModel>> GetFuturesRolloverSetAsync(
        string symbol,
        CancellationToken cancellationToken = default)
        => (await (GetRolloverFuturesContractsAsync(symbol, cancellationToken))
                .ConfigureAwait(false))
            .ToArray();

    /// <summary>
    /// Get a futures contract from the database by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to retrieve</param>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId)
    {
        var preparedParameters3 = new GetFuturesContract(contractId);
        var contracts = await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContract)}", SecuritiesDbSql.GetFuturesContract)
            .SetParameters(preparedParameters3)
            .ExecuteQueryAsync(MapToFuturesContract!))
                .ConfigureAwait(false);
        return contracts.Count switch
        {
            0 => null,
            1 => contracts.First(),
            _ => throw new StorageException(
                $"SecuritiesDb canonical futures contractId '{contractId}' is ambiguous across {contracts.Count} rows.")
        };
    }

    /// <summary>Reads futures contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken)
    {
        var preparedParameters4 = new GetFuturesContract(contractId);
        var contracts = await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContract)}", SecuritiesDbSql.GetFuturesContract)
            .SetParameters(preparedParameters4)
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken))
                .ConfigureAwait(false);
        return contracts.Count switch
        {
            0 => null,
            1 => contracts.First(),
            _ => throw new StorageException(
                $"SecuritiesDb canonical futures contractId '{contractId}' is ambiguous across {contracts.Count} rows.")
        };
    }

    /// <summary>
    /// Retrieves a futures contract based on the specified contract identifier.
    /// </summary>
    /// <remarks>This method queries the database to retrieve details of a specific futures contract. Ensure
    /// that the provided  <paramref name="e"/> contains valid and complete information for the query to
    /// succeed.</remarks>
    /// <param name="e">The identifier of the futures contract, including the contract ID, symbol, and maturity date.</param>
    /// <returns>A <see cref="FuturesContractV3ReadModel"/> representing the futures contract if found; otherwise, <see
    /// langword="null"/>.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId e)
=>this.GetFuturesContractAsyncCoreBinding(e);
    /// <summary>Reads futures contract from the committed projection.</summary>
    /// <param name="e">The e supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(
        FuturesContractId e,
        CancellationToken cancellationToken)
=>this.GetFuturesContractAsyncCoreBinding(e, cancellationToken);
    /// <summary>
    /// Get all futures contracts from the database
    /// </summary>
    /// <returns>A list of all futures contracts</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync()
        => await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContracts)}", SecuritiesDbSql.GetFuturesContracts)
            .ExecuteQueryAsync(MapToFuturesContract!))
                .ConfigureAwait(false);

    /// <summary>Reads futures contracts from the committed projection.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync(
        CancellationToken cancellationToken)
        => await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContracts)}", SecuritiesDbSql.GetFuturesContracts)
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken))
                .ConfigureAwait(false);

    /// <summary>
    /// Insert a new futures option contract into SecuritiesDb
    /// </summary>
    /// <param name="futuresOptionContract">The futures option contract supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    public Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract)
=>this.InsertFuturesOptionContractAsyncCoreBinding(futuresOptionContract);
    /// <summary>
    /// Asynchronously inserts a collection of futures option contracts into the database.
    /// </summary>
    /// <remarks>This method uses the database factory to execute an asynchronous command that inserts the
    /// provided futures option contracts. Ensure that each contract in the collection has all required fields populated
    /// to avoid database errors.</remarks>
    /// <param name="futuresOptionContract">A collection of <see cref="FuturesOptionContractReadModel"/> objects representing the futures option contracts
    /// to be inserted. Each object must contain valid contract details such as contract ID, description, symbol, and
    /// other relevant properties.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertFuturesOptionContractsAsync(ICollection<FuturesOptionContractReadModel> futuresOptionContract)
=>this.InsertFuturesOptionContractsAsyncCoreBinding(futuresOptionContract);
    /// <summary>
    /// Update an existing futures option contract in SecuritiesDb
    /// </summary>
    /// <param name="originalContractId">The previous canonical identity when replacing a row.</param>
    /// <param name="futuresOptionContract">The futures option contract supplied by the caller.</param>
    /// <returns>Completion of the database operation.</returns>
    public Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel futuresOptionContract)
=>this.UpdateFuturesOptionContractAsyncCoreBinding(originalContractId, futuresOptionContract);
    /// <summary>Deletes a futures option contract by identifier.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task DeleteFuturesOptionContractAsync(string contractId)
    {
        var db = DbFactory.SecuritiesDb;
        var preparedParameters5 = new GetFuturesOptionContract(contractId);
        var contracts = await (db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContract)}", SecuritiesDbSql.GetFuturesOptionContract)
            .SetParameters(preparedParameters5)
            .ExecuteQueryAsync(MapToFuturesOptionContract!))
                .ConfigureAwait(false);
        var preparedParameters6 = new DeleteFuturesOptionContract(contractId);
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesOptionContract)}", SecuritiesDbSql.DeleteFuturesOptionContract)
            .SetParameters(preparedParameters6)
            .QueueCommand()
        ];
        queuedCommands.AddRange(contracts.Select(contract =>
            db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesOptionContractBySymbolV2)}", SecuritiesDbSql.DeleteFuturesOptionContractBySymbolV2)
            .SetParameters(new DeleteFuturesOptionContractBySymbolV2(contract.Symbol, contract.ContractMonth, contract.OptionType, contract.StrikePrice, contract.ContractId))
            .QueueCommand()));
        await (this.ExecuteProjectionMutationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            contracts.Select(static contract => contract.Symbol),
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Get a futures option contract from the database by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures option contract to retrieve</param>
    /// <returns>The futures option contract with the specified ID</returns>
    public Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId)
        => GetFuturesOptionContractAsync(contractId, CancellationToken.None);

    /// <summary>Reads an exact canonical option definition, including the currently published startup definition cache.</summary>
    /// <param name="contractId">The established leg's unchanged canonical contract identity.</param>
    /// <param name="cancellationToken">Cancels reference storage reads.</param>
    /// <returns>The exact saved definition, or null when neither reference source contains it.</returns>
    public async Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        string contractId,
        CancellationToken cancellationToken)
    {
        var preparedParameters7 = new GetFuturesOptionContract(contractId);
        var contracts = await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContract)}", SecuritiesDbSql.GetFuturesOptionContract)
            .SetParameters(preparedParameters7)
            .ExecuteQueryAsync(MapToFuturesOptionContract!, cancellationToken))
                .ConfigureAwait(false);
        return contracts.Count switch
        {
            0 => await this.GetPublishedFuturesOptionContractAsync(contractId, cancellationToken)
            .ConfigureAwait(false),
            1 => contracts.First(),
            _ => throw new StorageException(
                $"SecuritiesDb canonical futures-option contractId '{contractId}' is ambiguous across {contracts.Count} rows.")
        };
    }

    /// <summary>Reads futures option contracts by ids from the committed projection.</summary>
    /// <param name="contractIds">The canonical contract identities to include.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(
        ICollection<string> contractIds)
=>this.GetFuturesOptionContractsByIdsAsyncCoreBinding(contractIds);
    /// <summary>Reads futures option contracts by ids from the committed projection.</summary>
    /// <param name="contractIds">The canonical contract identities to include.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(
        ICollection<string> contractIds,
        CancellationToken cancellationToken)
=>this.GetFuturesOptionContractsByIdsAsyncCoreBinding(contractIds, cancellationToken);
    /// <summary>
    /// Get all futures option contracts from the database
    /// </summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>A list of all futures option contracts</returns>
    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            () => this.LoadFuturesOptionContractProjectionAsync(symbol)))
                .ConfigureAwait(false);
    }

    /// <summary>Reads futures option contracts from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            token => this.LoadFuturesOptionContractProjectionAsync(symbol, token),            cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously retrieves a collection of futures option contracts.
    /// </summary>
    /// <remarks>This method queries the database to obtain the current futures option contracts and maps them
    /// to the <see cref="FuturesOptionContractReadModel"/> type. The returned collection may be empty  if no contracts
    /// are available.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="FuturesOptionContractReadModel"/> representing the futures option contracts.</returns>
    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync()
        => await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContracts)}", SecuritiesDbSql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!))
                .ConfigureAwait(false);

    /// <summary>Reads futures option contracts from the committed projection.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(
        CancellationToken cancellationToken)
        => await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContracts)}", SecuritiesDbSql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!, cancellationToken))
                .ConfigureAwait(false);

    /// <summary>
    /// Get futures contracts from the database by a list of contract IDs by symbol
    /// </summary>
    /// <param name="contractIds">The list of contract IDs to retrieve</param>
    /// <param name="symbol">The symbol of the futures contracts to retrieve</param>
    /// <returns>A list of futures contracts with the specified IDs</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol)
        => await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractsByIds)}", SecuritiesDbSql.GetFuturesContractsByIds)
            .SetParameters(new GetFuturesContractsByIds(contractIds, symbol))
            .ExecuteQueryAsync(MapToFuturesContract!))
                .ConfigureAwait(false);

    /// <summary>Reads futures contracts by ids from the committed projection.</summary>
    /// <param name="contractIds">The canonical contract identities to include.</param>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(
        ICollection<string> contractIds,
        string symbol,
        CancellationToken cancellationToken)
        => await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractsByIds)}", SecuritiesDbSql.GetFuturesContractsByIds)
            .SetParameters(new GetFuturesContractsByIds(contractIds, symbol))
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken))
                .ConfigureAwait(false);

    /// <summary>
    /// Get futures contracts from the database by symbol
    /// </summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            () => this.LoadFuturesContractProjectionAsync(symbol)))
                .ConfigureAwait(false);
    }

    /// <summary>Reads futures contracts by symbol from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await (this.ReadProjectionAsync(            token => this.LoadFuturesContractProjectionAsync(symbol, token),            cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>Ensure futures contract rollover rows.</summary>
    /// <param name="symbols">The symbols supplied by the caller.</param>
    /// <param name="createdOnUtc">The originating UTC timestamp supplied by the caller.</param>
    /// <param name="createdBy">The originating user or service identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    public async Task EnsureFuturesContractRolloverRowsAsync(
        IReadOnlyCollection<string> symbols,
        DateTime createdOnUtc,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameters = new InsertFuturesContractRolloverIfMissing(symbol, createdOnUtc, createdBy);
            await (DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesContractRolloverIfMissing)}", SecuritiesDbSql.InsertFuturesContractRolloverIfMissing)
            .SetParameters(parameters)
            .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
        }
    }

    /// <summary>Reads futures contract rollover from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public async Task<FuturesContractRolloverReadModel?> GetFuturesContractRolloverAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var preparedParameters8 = new GetFuturesContractRollover(symbol);
        return await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractRollover)}", SecuritiesDbSql.GetFuturesContractRollover)
            .SetParameters(preparedParameters8)
            .ExecuteSingleAsync(MapToFuturesContractRollover!, cancellationToken))
                .ConfigureAwait(false);
    }

    /// <summary>Reads persisted futures contract from the committed projection.</summary>
    /// <param name="contractId">The exact canonical contract identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    public Task<FuturesContractV3ReadModel?> GetPersistedFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken = default)
        => GetFuturesContractAsync(contractId, cancellationToken);

    /// <summary>Reads futures contract rollovers from the committed projection.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<IReadOnlyCollection<FuturesContractRolloverReadModel>> GetFuturesContractRolloversAsync(
        CancellationToken cancellationToken = default)
        => (await (DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractRollovers)}", SecuritiesDbSql.GetFuturesContractRollovers)
            .ExecuteQueryAsync(MapToFuturesContractRollover!, cancellationToken))
                .ConfigureAwait(false))
            .ToArray();

    // Tokens belong to this server lifetime. A restart requires a fresh first page.
    /// <summary>Reads one page from a completed symbol projection without scanning or repairing the base table.</summary>
    /// <param name="request">The caller's typed selection, including its filtering and page bounds.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<FuturesOptionContractPageReadModel> GetFuturesOptionContractsPageAsync(
        QualifiedOptionCatalogPage request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await this.ReadNativeOptionPageAsync(request,cancellationToken)
            .ConfigureAwait(false);
    }



    internal sealed record NativeOptionCursor(string Symbol, int PageSize, DateOnly Month, string OptionType, double Strike, string ContractId);

    internal static OptionExpiryCalendarState MapToOptionExpiryCalendarState<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Shared.Storage.IObjectDataRecord => new(
            row.GetGuid(0), row.GetDateOnly(1), row.GetDateOnly(2), row.GetDateTime(3));

    internal static OptionContractExpiryReadModel MapToOptionContractExpiry<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Shared.Storage.IObjectDataRecord => new()
        {
            Symbol = row.GetString(0),
            ContractId = row.IsNull(1) ? string.Empty : row.GetString(1),
            ExpiryDate = row.GetDateOnly(2),
            ProviderRoot = row.GetString(3),
            OptionFamily = row.GetString(4),
            RefreshedAtUtc = row.GetDateTime(5)
        };

    internal static CachedOptionContractDefinitionReadModel? MapToCachedOptionContractDefinition<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Shared.Storage.IObjectDataRecord
    {
        if (row.IsNull(1) || row.IsNull(5)) return null;
        return new()
        {
            Symbol = row.GetString(0),
            UnderlyingContractId = row.GetString(1),
            ExpiryDate = row.GetDateOnly(2),
            ProviderRoot = row.GetString(3),
            OptionFamily = row.GetString(4),
            Definition = ReferencePayloadCodec.ReadOption(row.GetBytes(5)),
            RefreshedAtUtc = row.GetDateTime(6)
        };
    }

    /// <summary>Reads option contract expiries from the committed projection.</summary>
    /// <param name="range">The canonical root and inclusive expiry boundaries.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<IReadOnlyList<OptionContractExpiryReadModel>> GetOptionContractExpiriesAsync(
        OptionDefinitionExpiryRange range,
        CancellationToken cancellationToken = default)
    {
        var state = await this.ReadExpiryStateAsync(range.Symbol, cancellationToken)
            .ConfigureAwait(false);
        return await this.ReadExpiriesAsync(range.Symbol, range.FromExpiry, range.ThroughExpiry, state, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reads cached option contract definitions from the committed projection.</summary>
    /// <param name="partition">The caller-qualified option definition partition.</param>
    /// <param name="providerRoots">The allowed provider roots, or null to include all roots.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    public async Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> GetCachedOptionContractDefinitionsAsync(
        OptionDefinitionPartition partition,
        IReadOnlyCollection<string>? providerRoots = null, CancellationToken cancellationToken = default)
    {
        var (symbol, underlyingContractId, expiryDate) = partition;
        var db = DbFactory.SecuritiesDb;
        var state = await this.ReadExpiryStateAsync(symbol, cancellationToken)
            .ConfigureAwait(false);
        if (state is null || expiryDate < state.CoverageFrom || expiryDate > state.CoverageThrough) return [];
        var roots = (providerRoots ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var preparedParameters9 = new GetCachedOptionContractDefinitions(symbol, state.Generation, expiryDate);
        var rows = await db.Use(nameof(SecuritiesDbSql.GetCachedOptionContractDefinitions), SecuritiesDbSql.GetCachedOptionContractDefinitions)
            .SetParameters(preparedParameters9)
            .ExecuteQueryAsync(MapToCachedOptionContractDefinition!, cancellationToken)
            .ConfigureAwait(false);
        return rows.FilterDefinitions(underlyingContractId, roots).ToArray();
    }

    /// <inheritdoc/>
    public async Task<OptionDefinitionGenerationReadModel?> GetOptionDefinitionGenerationAsync(string symbol, CancellationToken cancellationToken)
        => await this.ReadExpiryStateAsync(symbol, cancellationToken)
            .ConfigureAwait(false) is { } state
            ? new(state.Generation, state.CoverageFrom, state.CoverageThrough) : null;

    /// <inheritdoc/>
    public Task<IReadOnlyList<OptionContractExpiryReadModel>> GetOptionDefinitionExpiriesAsync(
        string symbol, DateOnly first, DateOnly last, OptionDefinitionGenerationReadModel generation, CancellationToken cancellationToken)
        => this.ReadExpiriesAsync(symbol, first, last,
            new OptionExpiryCalendarState(generation.Generation, generation.CoverageFrom, generation.CoverageThrough, default), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<CachedOptionContractDefinitionReadModel> ReadOptionDefinitionPartitionsAsync(
        string symbol, Guid generation, DateOnly[] expiries, CancellationToken cancellationToken)
        => this.ReadDefinitionPartitionsAsync(symbol, generation, expiries, cancellationToken);

    /// <inheritdoc />
    public Task<CachedOptionContractDefinitionReadModel[]> ResolveOptionReferenceIdentitiesAsync(
        IReadOnlyList<CachedOptionContractDefinitionReadModel> rows, CancellationToken cancellationToken)
        => this.ResolveOptionReferenceIdentitiesCoreAsync(rows, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<CachedOptionContractDefinitionReadModel> ReadPublishedOptionDefinitionsAsync(
        OptionDefinitionExpiryRange range,
        IReadOnlyCollection<string>? providerRoots = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (symbol, first, last, maximumExpiries) = (range.Symbol, range.FromExpiry, range.ThroughExpiry, range.MaximumExpiries);
        var state = await this.ReadExpiryStateAsync(symbol, cancellationToken)
            .ConfigureAwait(false);
        if (state is null) yield break;
        var expiries = await this.ReadExpiriesAsync(symbol, first, last, state, cancellationToken)
            .ConfigureAwait(false);
        if (expiries.Count > maximumExpiries)
            throw new InvalidDataException("Raw option expiry capacity exceeded.");

        var pairs = expiries.Select(row => (row.ExpiryDate, row.ContractId)).Distinct();

        var groups = pairs.GroupBy(row => row.ExpiryDate).ToArray();
        var missing = groups.Select(group => group.Key).ToArray();
        var roots = (providerRoots ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await using var cold = this.ReadDefinitionPartitionsAsync(symbol, state.Generation, missing, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        var more = await cold.MoveNextAsync()
            .ConfigureAwait(false);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = new List<CachedOptionContractDefinitionReadModel>();
            while (more && cold.Current.ExpiryDate == group.Key)
            {
                rows.Add(cold.Current);
                more = await cold.MoveNextAsync()
            .ConfigureAwait(false);
            }
            foreach (var (_, underlying) in group)
                foreach (var row in rows.FilterDefinitions(underlying, roots))
                    yield return row;
        }
    }

    /// <summary>Replaces the active option definition generation and its coverage atomically.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="coverageFrom">The inclusive beginning of the definition publication.</param>
    /// <param name="coverageThrough">The inclusive end of the definition publication.</param>
    /// <param name="definitions">The accepted option definitions for the published generation.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    /// <inheritdoc />
    public async Task PublishOptionDefinitionsAsync(OptionDefinitionPublication publication, CancellationToken cancellationToken)
    {
        var rows = publication.Rows.Select(row => (row.ExpiryDate, row.ProviderRoot, row.Definition.ContractId,
            row.UnderlyingContractId, row.OptionFamily, ReferencePayloadCodec.Write(row.Definition))).ToArray();
        await this.NativeTransaction().ExecuteOnceAsync(async (db, token) =>
        {
            await NativeReferences.PublishDefinitionsAsync(db, publication.Symbol, publication.Generation,
                publication.CoverageFrom, publication.CoverageThrough, publication.RefreshedAtUtc, rows, token)
            .ConfigureAwait(false);
            return true;
        }, cancellationToken)
            .ConfigureAwait(false);
    }


    /// <inheritdoc/>
    public async Task<long> ReadCatalogRevisionAsync(string stream, CancellationToken token)
        => await Use(nameof(EventSourceActorDbSql.HasCommittedMaintenanceStream), EventSourceActorDbSql.HasCommittedMaintenanceStream)
            .SetParameters(new MaintenanceSourceStream(stream))
            .ExecuteScalarAsync(MapToCatalogRevision, token)
            .ConfigureAwait(false);

    /// <summary>Maps the committed stream revision at ordinal zero.</summary>
    internal static long MapToCatalogRevision(IObjectDataRecord row) => row.GetLong(0);

    /// <inheritdoc/>
    public Task LockCatalogMaintenanceAsync(IEnlistedPostgresTransaction transaction, bool includeSymbolTables, CancellationToken token)
        => transaction.ExecuteAsync(includeSymbolTables ? SecuritiesDbSql.LockRebuildTables : SecuritiesDbSql.LockBaselineTables, [], token);

    /// <inheritdoc/>
    public Task<bool> HasLegacyCatalogHistoryAsync(IEnlistedPostgresTransaction transaction, string[] excludedStreams, string[] prefixes, CancellationToken token)
        => transaction.HasCommittedStreamWithPrefixAsync(excludedStreams, prefixes, token);

    /// <inheritdoc/>
    public Task<bool> HasCatalogSourceStreamAsync(IEnlistedPostgresTransaction transaction, string stream, CancellationToken token)
        => transaction.HasCommittedMaintenanceStreamAsync(stream, token);

    /// <inheritdoc/>
    public Task<IReadOnlyList<EventStreamSourceReadModel>> ReadCatalogSourcesAsync(IEnlistedPostgresTransaction transaction, string[] exactStreams, string[] prefixes, CancellationToken token)
        => transaction.ReadMaintenanceSourcesAsync(exactStreams, prefixes, token);

    /// <inheritdoc/>
    public async Task<bool> CatalogProjectionHasRowsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token)
        => await transaction.ScalarAsync(SecuritiesDbSql.CatalogProjectionHasRows, [], token)
            .ConfigureAwait(false) is true;

    /// <inheritdoc/>
    public async Task<SecuritiesCatalogRows> ReadCatalogRowsAsync(IEnlistedPostgresTransaction transaction, CancellationToken token)
    {
        var futures = await transaction.QueryAsync(SecuritiesDbSql.GetFuturesContracts, [], MapToCatalogFuture, token)
            .ConfigureAwait(false);
        var options = await transaction.QueryAsync(SecuritiesDbSql.GetFuturesOptionContracts, [], MapToCatalogOption, token)
            .ConfigureAwait(false);
        var pointers = await transaction.QueryAsync(SecuritiesDbSql.GetFuturesContractRollovers, [], MapToCatalogRollover, token)
            .ConfigureAwait(false);
        return new(futures.ToArray(), options.ToArray(), pointers.ToArray());
    }

    /// <inheritdoc/>
    public Task WriteFuturesCatalogRowsAsync(IEnlistedPostgresTransaction transaction, IReadOnlyList<FuturesContractV3ReadModel> contracts, CancellationToken token)
        => this.WriteFuturesCatalogRowsCoreAsync(transaction, contracts, token);

    /// <inheritdoc/>
    public Task WriteFuturesRolloverPointerAsync(IEnlistedPostgresTransaction transaction, FuturesContractRolloverReadModel rollover, CancellationToken token)
        => this.WriteFuturesRolloverPointerCoreAsync(transaction, rollover, token);

    /// <summary>Maps an enlisted canonical futures record with the ordinary context ordinals.</summary>
    internal static FuturesContractV3ReadModel MapToCatalogFuture(System.Data.Common.DbDataReader row)
        => MapToFuturesContract(new AdoNetDataRecord().SetReader(row));

    /// <summary>Maps an enlisted canonical option record with the ordinary context ordinals.</summary>
    internal static FuturesOptionContractReadModel MapToCatalogOption(System.Data.Common.DbDataReader row)
        => MapToFuturesOptionContract(new AdoNetDataRecord().SetReader(row));

    /// <summary>Maps an enlisted rollover pointer with the ordinary context ordinals.</summary>
    internal static FuturesContractRolloverReadModel MapToCatalogRollover(System.Data.Common.DbDataReader row)
        => MapToFuturesContractRollover(new AdoNetDataRecord().SetReader(row));
}
