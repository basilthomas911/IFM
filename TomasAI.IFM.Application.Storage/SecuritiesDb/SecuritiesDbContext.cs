using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>
/// Provides Securities database read and write operations.
/// </summary>
/// <param name="connectionSettings">The database connection settings.</param>
/// <param name="dbFactory">The application database-context factory.</param>
/// <param name="logger">The database provider logger.</param>
public sealed class SecuritiesDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger)
    : ObjectDataRepository<SecuritiesDbContext>(connectionSettings[SecuritiesDbConnection], logger), ISecuritiesDbContext
{
    public const string SecuritiesDbConnection = "SecuritiesDbConnection";
    internal const string FuturesContractSymbolProjection = "futures_contract_by_symbol_v3";
    internal const string FuturesOptionContractSymbolProjection = "futures_option_contract_by_symbol_v2";
    internal const int CompletionStateLookupBatchSize = 100;
    internal const string GlobalProjectionOperationScope = "global";
    internal const string ProjectionOperationScopeCount = "scope-count";
    internal const string SymbolProjectionOperationScope = "symbol";
    internal readonly IDbContextFactory DbFactory = dbFactory;
    internal readonly ConcurrentDictionary<string, SemaphoreSlim> RolloverMutationGates =
        new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Stages an immutable futures reference version.</summary>
    public Task<PendingReferenceVersion> StageReferenceVersionAsync(
        FuturesContractV3ReadModel value,
        CancellationToken cancellationToken = default)
        => this.StageReferenceVersionCoreAsync(value, cancellationToken);

    /// <summary>Stages an immutable futures-option reference version.</summary>
    public Task<PendingReferenceVersion> StageReferenceVersionAsync(
        FuturesOptionContractReadModel value,
        CancellationToken cancellationToken = default)
        => this.StageReferenceVersionCoreAsync(value, cancellationToken);

    /// <summary>Publishes a previously staged immutable reference version.</summary>
    public Task CommitReferenceVersionAsync(
        PendingReferenceVersion value,
        CancellationToken cancellationToken = default)
        => this.CommitReferenceVersionCoreAsync(value, cancellationToken);

    /// <summary>Gets a published immutable reference version.</summary>
    public Task<ReferenceContractVersion?> GetReferenceVersionAsync(
        string contractId,
        string version,
        CancellationToken cancellationToken = default)
        => this.GetReferenceVersionCoreAsync(contractId, version, cancellationToken);

    /// <inheritdoc />
    public async Task<OptionPricingConvention?> GetAsync(
        string contractId,
        string mappingVersion,
        CancellationToken cancellationToken)
        => (await GetReferenceVersionAsync(contractId, mappingVersion, cancellationToken)
            .ConfigureAwait(false))?.Convention;

    /// <summary>Gets whether a staged or published immutable reference version exists.</summary>
    public Task<bool> ContainsReferenceVersionAsync(
        string contractId,
        string version,
        CancellationToken cancellationToken = default)
        => this.ContainsReferenceVersionCoreAsync(contractId, version, cancellationToken);

    /// <summary>Gets a published reference version effective at the specified UTC instant.</summary>
    public Task<ReferenceContractVersion?> GetEffectiveReferenceVersionAsync(
        string contractId,
        string version,
        DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default)
        => this.GetEffectiveReferenceVersionCoreAsync(contractId, version, effectiveAtUtc, cancellationToken);

    /// <summary>Lists a bounded page of staged and published reference versions.</summary>
    public Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsAsync(
        string contractId,
        string afterVersion = "",
        int limit = 100,
        CancellationToken cancellationToken = default)
        => this.ListReferenceVersionsCoreAsync(contractId, afterVersion, limit, cancellationToken);


























    /// <summary>
    /// Streams the canonical Securities tables and idempotently rebuilds both symbol projections.
    /// </summary>
    /// <param name="batchSize">Maximum number of projection rows written per batch.</param>
    /// <param name="cancellationToken">Stops the backfill before cutover.</param>
    /// <param name="staleOperationCutoffUtc">
    /// Optional operator-verified UTC cutoff. When supplied, journaled operations started
    /// at or before this instant are removed from only their recorded state scopes before
    /// rebuilding. All Securities projection writers must first be drained and prevented
    /// from resuming. A null value performs no stale-operation recovery.
    /// </param>
    public async Task<SecuritiesProjectionBackfillResult> BackfillSymbolProjectionsAsync(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
    {
        this.ValidateBackfillParameters(batchSize, staleOperationCutoffUtc);

        var db = DbFactory.SecuritiesDb;
        if (staleOperationCutoffUtc is { } verifiedInactiveCutoffUtc)
        {
            // This is deliberately opt-in. The caller must first drain every Securities
            // projection writer and verify that operations at/before the cutoff cannot
            // resume; age alone is not proof that an operation is dead.
            await this.RecoverVerifiedInactiveProjectionOperationsAsync(
                db,
                FuturesContractSymbolProjection,
                verifiedInactiveCutoffUtc,
                cancellationToken)
                    .ConfigureAwait(false);
            await this.RecoverVerifiedInactiveProjectionOperationsAsync(
                db,
                FuturesOptionContractSymbolProjection,
                verifiedInactiveCutoffUtc,
                cancellationToken)
                    .ConfigureAwait(false);
        }

        // Cutover is disabled before validation starts. An ambiguous canonical identity
        // or an inventory failure must never leave a previous global completion visible.
        await this.InvalidateGlobalProjectionStateAsync(
            db,
            FuturesContractSymbolProjection,
            cancellationToken);
        await this.InvalidateGlobalProjectionStateAsync(
            db,
            FuturesOptionContractSymbolProjection,
            cancellationToken);

        // Inventory both sides before deleting target partitions. The source identity
        // validation prevents an arbitrary symbol from winning for APIs keyed by contractId.
        var inventory = await this.ReadProjectionInventoryAsync(db, cancellationToken);
        var futuresContractSymbols = inventory.FuturesContractSourceKeys
            .Select(static key => key.Symbol)
            .Concat(inventory.FuturesContractTargetKeys.Select(static key => key.Symbol))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var futuresOptionContractSymbols = inventory.FuturesOptionContractSourceKeys
            .Select(static key => key.Symbol)
            .Concat(inventory.FuturesOptionContractTargetKeys.Select(static key => key.Symbol))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        ProjectionOperation? futuresOperation = null;
        ProjectionOperation? futuresOptionOperation = null;
        var targetMutationSubmissionStarted = false;
        try
        {
            futuresOperation = await this.BeginProjectionOperationAsync(
                db,
                FuturesContractSymbolProjection,
                futuresContractSymbols,
                cancellationToken);
            futuresOptionOperation = await this.BeginProjectionOperationAsync(
                db,
                FuturesOptionContractSymbolProjection,
                futuresOptionContractSymbols,
                cancellationToken);

            targetMutationSubmissionStarted = true;
            if (futuresContractSymbols.Length > 0)
            {
                await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3Partition)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3Partition)
                    .SetParameters(futuresContractSymbols.Select(static symbol =>
                        new DeleteFuturesContractBySymbolV3Partition(symbol)))
                    .ExecuteCommandAsync(cancellationToken);
            }
            if (futuresOptionContractSymbols.Length > 0)
            {
                await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)}", SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)
                    .SetParameters(futuresOptionContractSymbols.Select(static symbol =>
                        new DeleteFuturesOptionContractBySymbolV2Partition(symbol)))
                    .ExecuteCommandAsync(cancellationToken);
            }

            var futuresContractsUpserted = 0;
            var futuresOptionContractsUpserted = 0;
            var futuresContracts = new List<FuturesContractV3ReadModel>(batchSize);
            await foreach (var contract in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
                .ExecuteStreamAsync(MapToFuturesContract!, cancellationToken))
            {
                futuresContracts.Add(contract);
                if (futuresContracts.Count < batchSize)
                    continue;

                await this.PopulateFuturesContractSymbolProjectionAsync(futuresContracts, cancellationToken);
                futuresContractsUpserted += futuresContracts.Count;
                futuresContracts.Clear();
            }
            await this.PopulateFuturesContractSymbolProjectionAsync(futuresContracts, cancellationToken);
            futuresContractsUpserted += futuresContracts.Count;

            var futuresOptionContracts = new List<FuturesOptionContractReadModel>(batchSize);
            await foreach (var contract in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", SecuritiesDbCql.GetFuturesOptionContracts)
                .ExecuteStreamAsync(MapToFuturesOptionContract!, cancellationToken))
            {
                futuresOptionContracts.Add(contract);
                if (futuresOptionContracts.Count < batchSize)
                    continue;

                await this.PopulateFuturesOptionContractSymbolProjectionAsync(futuresOptionContracts, cancellationToken);
                futuresOptionContractsUpserted += futuresOptionContracts.Count;
                futuresOptionContracts.Clear();
            }
            await this.PopulateFuturesOptionContractSymbolProjectionAsync(futuresOptionContracts, cancellationToken);
            futuresOptionContractsUpserted += futuresOptionContracts.Count;

            var reconciliation = await ReconcileSymbolProjectionsAsync(cancellationToken);
            if (!reconciliation.IsConsistent)
            {
                throw new StorageException(
                    "SecuritiesDb V2 symbol-projection backfill did not reconcile " +
                    $"(futures missing={reconciliation.FuturesContractMissingKeys}, " +
                    $"futures unexpected={reconciliation.FuturesContractUnexpectedKeys}, " +
                    $"options missing={reconciliation.FuturesOptionContractMissingKeys}, " +
                    $"options unexpected={reconciliation.FuturesOptionContractUnexpectedKeys}). " +
                    "Completion remains disabled; replay the backfill before cutover.");
            }

            var futuresCompleted = await this.CompleteProjectionOperationAsync(
                db,
                futuresOperation,
                completeGlobal: true,
                completeAllSymbols: true,
                cancellationToken);
            var futuresOptionsCompleted = await this.CompleteProjectionOperationAsync(
                db,
                futuresOptionOperation,
                completeGlobal: true,
                completeAllSymbols: true,
                cancellationToken);
            if (!futuresCompleted || !futuresOptionsCompleted)
            {
                throw new StorageException(
                    "SecuritiesDb V2 symbol-projection completion raced another write or repair. " +
                    $"Completion results: futures={futuresCompleted}, options={futuresOptionsCompleted}. " +
                    "Completion remains disabled; replay the backfill after the competing operation finishes.");
            }

            return new SecuritiesProjectionBackfillResult(
                futuresContractsUpserted,
                futuresOptionContractsUpserted);
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                if (futuresOptionOperation is not null)
                    await this.EndProjectionOperationAsync(db, futuresOptionOperation, CancellationToken.None)
                        .ConfigureAwait(false);
                if (futuresOperation is not null)
                    await this.EndProjectionOperationAsync(db, futuresOperation, CancellationToken.None)
                        .ConfigureAwait(false);
            }
            throw;
        }
    }


    /// <summary>
    /// Streams canonical and projected primary keys and reports missing or unexpected projection rows.
    /// </summary>
    public async Task<SecuritiesProjectionReconciliationResult> ReconcileSymbolProjectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var inventory = await this.ReadProjectionInventoryAsync(DbFactory.SecuritiesDb, cancellationToken);
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
    /// <returns></returns>
    public async Task InsertFuturesContractAsync(FuturesContractV3ReadModel futuresContract)
    {
        var reference = await this.StageReferenceAsync(futuresContract);
        var db = DbFactory.SecuritiesDb;
        var parameters = this.ToInsertParameters(futuresContract);
        this.EnsureDistinctFuturesContractWrites([futuresContract]);
        List<object> queuedCommands = this.OtherProjectionKeys(futuresContract)
            .Select(key => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(new DeleteFuturesContractBySymbolV3(
                    key.Symbol, key.Rollover, key.OnTheRun, key.LastTradeDate, key.ContractId))
                .QueueCommand())
            .ToList();
        queuedCommands.AddRange([
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(parameters)
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                .SetParameters(parameters)
                .QueueCommand()
        ]);
        await this.ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            [futuresContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
        await this.CommitReferenceAsync(reference);
    }

    /// <summary>
    /// Asynchronously inserts a collection of futures contracts into the database.
    /// </summary>
    /// <remarks>This method uses the database factory to execute an insert command for each futures contract
    /// in the provided collection. Ensure that the collection is not null and contains valid futures contract data to
    /// avoid exceptions during execution.</remarks>
    /// <param name="futuresContracts">A collection of <see cref="FuturesContractV3ReadModel"/> objects representing the futures contracts to be
    /// inserted. Each contract must have valid properties set, such as contract ID, description, symbol, and other
    /// relevant details.</param>
    /// <returns></returns>
    public async Task InsertFuturesContractsAsync(ICollection<FuturesContractV3ReadModel> futuresContracts)
    {
        if (futuresContracts.Count == 0)
            return;

        var db = DbFactory.SecuritiesDb;
        this.EnsureDistinctFuturesContractWrites(futuresContracts);
        var references = new List<PendingReferenceVersion?>();
        foreach (var contract in futuresContracts) references.Add(await this.StageReferenceAsync(contract));
        var operation = await this.BeginProjectionOperationAsync(
            db,
            FuturesContractSymbolProjection,
            futuresContracts.Select(static contract => contract.Symbol));
        var targetMutationSubmissionStarted = false;
        try
        {
            targetMutationSubmissionStarted = true;
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(futuresContracts.SelectMany(this.OtherProjectionKeys).Select(static key =>
                    new DeleteFuturesContractBySymbolV3(
                        key.Symbol, key.Rollover, key.OnTheRun, key.LastTradeDate, key.ContractId)))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(futuresContracts.Select(this.ToInsertParameters))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                .SetParameters(futuresContracts.Select(this.ToInsertParameters))
                .ExecuteCommandAsync();
            await this.CompleteProjectionOperationAsync(
                db,
                operation,
                completeGlobal: false,
                completeAllSymbols: false);
            foreach (var reference in references) await this.CommitReferenceAsync(reference);
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                await this.EndProjectionOperationAsync(db, operation, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            throw;
        }
    }

    /// <summary>
    /// Update an existing futures contract in SecuritiesDb
    /// </summary>
    /// <param name="e">The ID of the futures contract to update</param>
    /// <param name="futuresContract">The futures contract to update</param>
    /// <returns></returns>
    public async Task UpdateFuturesContractAsync(FuturesContractId e, FuturesContractV3ReadModel futuresContract)
    {
        var reference = await this.StageReferenceAsync(futuresContract, e.ContractId);
        var db = DbFactory.SecuritiesDb;
        var originalContract = await GetFuturesContractAsync(e);
        var replacementProjectionKey = this.ToProjectionKey(futuresContract);
        List<object> queuedCommands = [];

        if (originalContract is not null)
        {
            var originalProjectionKey = this.ToProjectionKey(originalContract);
            if (originalContract.ContractId != futuresContract.ContractId ||
                originalContract.Symbol != futuresContract.Symbol ||
                originalContract.LastTradeDate != futuresContract.LastTradeDate)
            {
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", SecuritiesDbCql.DeleteFuturesContractById)
                    .SetParameters(new DeleteFuturesContractById(
                        originalContract.ContractId,
                        originalContract.Symbol,
                        originalContract.LastTradeDate))
                    .QueueCommand());
            }
            if (originalProjectionKey != replacementProjectionKey)
            {
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                    .SetParameters(new DeleteFuturesContractBySymbolV3(
                        originalContract.Symbol,
                        originalContract.Rollover,
                        originalContract.OnTheRun,
                        originalContract.LastTradeDate,
                        originalContract.ContractId))
                    .QueueCommand());
            }
        }
        else
        {
            if (e.ContractId != futuresContract.ContractId ||
                e.Symbol != futuresContract.Symbol ||
                e.MaturityDate != futuresContract.LastTradeDate)
            {
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", SecuritiesDbCql.DeleteFuturesContractById)
                    .SetParameters(new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                    .QueueCommand());
            }
            foreach (var rollover in new[] { false, true })
            foreach (var onTheRun in new[] { false, true })
            {
                var candidate = new FuturesContractProjectionKey(
                    e.Symbol,
                    rollover,
                    onTheRun,
                    e.MaturityDate,
                    e.ContractId);
                if (candidate == replacementProjectionKey)
                    continue;

                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                    .SetParameters(new DeleteFuturesContractBySymbolV3(
                        candidate.Symbol,
                        candidate.Rollover,
                        candidate.OnTheRun,
                        candidate.LastTradeDate,
                        candidate.ContractId))
                    .QueueCommand());
            }
        }

        queuedCommands.AddRange([
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(this.ToInsertParameters(futuresContract))
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                .SetParameters(this.ToInsertParameters(futuresContract))
                .QueueCommand()]);
        await this.ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            [e.Symbol, futuresContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
        await this.CommitReferenceAsync(reference);
    }

    /// Delete a futures contract from SecuritiesDb by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to delete</param>
    /// <returns></returns>
    public async Task DeleteFuturesContractAsync(string contractId)
    {
        var db = DbFactory.SecuritiesDb;
        var contracts = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContract)}", SecuritiesDbCql.GetFuturesContract)
            .SetParameters(new GetFuturesContract(contractId))
            .ExecuteQueryAsync(MapToFuturesContract!);
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContract)}", SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(new DeleteFuturesContract(contractId))
                .QueueCommand()
        ];
        queuedCommands.AddRange(contracts.Select(contract =>
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(new DeleteFuturesContractBySymbolV3(
                    contract.Symbol,
                    contract.Rollover,
                    contract.OnTheRun,
                    contract.LastTradeDate,
                    contract.ContractId))
                .QueueCommand()));
        await this.ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            contracts.Select(static contract => contract.Symbol),
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
    }

    /// <summary>
    /// Deletes a futures contract from the database asynchronously.
    /// </summary>
    /// <remarks>This method removes the specified futures contract from the database. Ensure that the
    /// provided  <paramref name="e"/> contains valid and complete information for the contract to be deleted.</remarks>
    /// <param name="e">The identifier of the futures contract to delete, including the contract ID, symbol, and maturity date.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteFuturesContractAsync(FuturesContractId e)
    {
        var db = DbFactory.SecuritiesDb;
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", SecuritiesDbCql.DeleteFuturesContractById)
                .SetParameters(new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                .QueueCommand(),
        ];
        foreach (var rollover in new[] { false, true })
        foreach (var onTheRun in new[] { false, true })
        {
            queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(new DeleteFuturesContractBySymbolV3(
                    e.Symbol, rollover, onTheRun, e.MaturityDate, e.ContractId))
                .QueueCommand());
        }
        await this.ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            [e.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task DeleteOnTheRunFuturesContractAsync(string symbol)
    {
        var fc = await GetOnTheRunFuturesContractAsync(symbol);
        if (fc is not null)
            await DeleteFuturesContractAsync(fc.Id);
    }

/// <summary>
    /// Get currently traded futures contract from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync(
            db,
            FuturesContractSymbolProjection,
            symbol,
            () => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOnTheRunFuturesContract)}", SecuritiesDbCql.GetOnTheRunFuturesContract)
                .SetParameters(new GetOnTheRunFuturesContract(symbol))
                .ExecuteSingleAsync(MapToFuturesContract!),
            async () => (await this.LoadAndPopulateFuturesContractsBySymbolAsync(symbol))
                .FirstOrDefault(static candidate => candidate.OnTheRun));
    }

    public async Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync(
            db,
            FuturesContractSymbolProjection,
            symbol,
            cancellationToken,
            token => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOnTheRunFuturesContract)}", SecuritiesDbCql.GetOnTheRunFuturesContract)
                .SetParameters(new GetOnTheRunFuturesContract(symbol))
                .ExecuteSingleAsync(MapToFuturesContract!, token),
            async token => (await this.LoadAndPopulateFuturesContractsBySymbolAsync(symbol, token))
                .FirstOrDefault(static candidate => candidate.OnTheRun));
    }

    /// <summary>
    /// Get currently traded futures contracts from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync<ICollection<FuturesContractV3ReadModel>>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            () => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetRolloverFuturesContracts)}", SecuritiesDbCql.GetRolloverFuturesContracts)
                .SetParameters(new GetRolloverFuturesContracts(symbol))
                .ExecuteQueryAsync(MapToFuturesContract!),
            async () => (await this.LoadAndPopulateFuturesContractsBySymbolAsync(symbol))
                .Where(static contract => contract.Rollover)
                .ToArray());
    }

    public async Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync<ICollection<FuturesContractV3ReadModel>>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            cancellationToken,
            token => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetRolloverFuturesContracts)}", SecuritiesDbCql.GetRolloverFuturesContracts)
                .SetParameters(new GetRolloverFuturesContracts(symbol))
                .ExecuteQueryAsync(MapToFuturesContract!, token),
            async token => (await this.LoadAndPopulateFuturesContractsBySymbolAsync(symbol, token))
                .Where(static contract => contract.Rollover)
                .ToArray());
    }

    public async Task<IReadOnlyCollection<FuturesContractV3ReadModel>> GetFuturesRolloverSetAsync(
        string symbol,
        CancellationToken cancellationToken = default)
        => (await GetRolloverFuturesContractsAsync(symbol, cancellationToken))
            .ToArray();

    /// <summary>
    /// Get a futures contract from the database by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to retrieve</param>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId)
    {
        var contracts = await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContract)}", SecuritiesDbCql.GetFuturesContract)
            .SetParameters(new GetFuturesContract(contractId))
            .ExecuteQueryAsync(MapToFuturesContract!);
        return contracts.Count switch
        {
            0 => null,
            1 => contracts.First(),
            _ => throw new StorageException(
                $"SecuritiesDb canonical futures contractId '{contractId}' is ambiguous across {contracts.Count} rows.")
        };
    }

    public async Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken)
    {
        var contracts = await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContract)}", SecuritiesDbCql.GetFuturesContract)
            .SetParameters(new GetFuturesContract(contractId))
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken);
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
    public async Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId e)
        => await DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractById)}", SecuritiesDbCql.GetFuturesContractById)
                .SetParameters(new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                .ExecuteSingleAsync(MapToFuturesContract!);

    public async Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(
        FuturesContractId e,
        CancellationToken cancellationToken)
        => await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractById)}", SecuritiesDbCql.GetFuturesContractById)
            .SetParameters(new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
            .ExecuteSingleAsync(MapToFuturesContract!, cancellationToken);

    /// <summary>
    /// Get all futures contracts from the database
    /// </summary>
    /// <returns>A list of all futures contracts</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync()
        => await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
            .ExecuteQueryAsync(MapToFuturesContract!);

    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync(
        CancellationToken cancellationToken)
        => await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken);

    /// <summary>
    /// Insert a new futures option contract into SecuritiesDb
    /// </summary>
    /// <param name="futuresOptionContract"></param>
    /// <returns></returns>
    public async Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract)
    {
        var reference = await this.StageReferenceAsync(futuresOptionContract);
        var db = DbFactory.SecuritiesDb;
        var parameters = this.ToInsertParameters(futuresOptionContract);
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(parameters)
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(parameters)
                .QueueCommand()
        ];
        await this.ExecuteProjectionMutationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            [futuresOptionContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
        await this.CommitReferenceAsync(reference);
    }

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
    public async Task InsertFuturesOptionContractsAsync(ICollection<FuturesOptionContractReadModel> futuresOptionContract)
    {
        if (futuresOptionContract.Count == 0)
            return;

        var db = DbFactory.SecuritiesDb;
        this.EnsureDistinctFuturesOptionContractWrites(futuresOptionContract);
        var references = new List<PendingReferenceVersion?>();
        foreach (var contract in futuresOptionContract) references.Add(await this.StageReferenceAsync(contract));
        var operation = await this.BeginProjectionOperationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            futuresOptionContract.Select(static contract => contract.Symbol));
        var targetMutationSubmissionStarted = false;
        try
        {
            targetMutationSubmissionStarted = true;
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(futuresOptionContract.Select(this.ToInsertParameters))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(futuresOptionContract.Select(this.ToInsertParameters))
                .ExecuteCommandAsync();
            await this.CompleteProjectionOperationAsync(
                db,
                operation,
                completeGlobal: false,
                completeAllSymbols: false);
            foreach (var reference in references) await this.CommitReferenceAsync(reference);
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                await this.EndProjectionOperationAsync(db, operation, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            throw;
        }
    }

    /// <summary>
    /// Update an existing futures option contract in SecuritiesDb
    /// </summary>
    /// <param name="originalContractId"></param>
    /// <param name="futuresOptionContract"></param>
    /// <returns></returns>
    public async Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel futuresOptionContract)
    {
        var reference = await this.StageReferenceAsync(futuresOptionContract, originalContractId);
        var db = DbFactory.SecuritiesDb;
        var originalContract = await GetFuturesOptionContractAsync(originalContractId);

        var replacementProjectionKey = this.ToProjectionKey(futuresOptionContract);
        List<object> queuedCommands = [];
        if (originalContract is not null)
        {
            var originalProjectionKey = this.ToProjectionKey(originalContract);
            if (originalProjectionKey != replacementProjectionKey)
            {
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractById)}", SecuritiesDbCql.DeleteFuturesOptionContractById)
                    .SetParameters(new DeleteFuturesOptionContractById(originalContract.ContractId, originalContract.ContractMonth, originalContract.Symbol, originalContract.OptionType, originalContract.StrikePrice))
                    .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)
                    .SetParameters(new DeleteFuturesOptionContractBySymbolV2(originalContract.Symbol, originalContract.ContractMonth, originalContract.OptionType, originalContract.StrikePrice, originalContract.ContractId))
                    .QueueCommand());
            }
        }
        else if (!string.Equals(originalContractId, futuresOptionContract.ContractId, StringComparison.Ordinal))
        {
            queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContract)}", SecuritiesDbCql.DeleteFuturesOptionContract)
                .SetParameters(new DeleteFuturesOptionContract(originalContractId))
                .QueueCommand());
        }

        queuedCommands.AddRange([
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(this.ToInsertParameters(futuresOptionContract))
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(this.ToInsertParameters(futuresOptionContract))
                .QueueCommand()]);
        await this.ExecuteProjectionMutationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            originalContract is null ? [futuresOptionContract.Symbol] : [originalContract.Symbol, futuresOptionContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
        await this.CommitReferenceAsync(reference);
    }

    /// <summary>Deletes a futures option contract by identifier.</summary>
    public async Task DeleteFuturesOptionContractAsync(string contractId)
    {
        var db = DbFactory.SecuritiesDb;
        var contracts = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContract)}", SecuritiesDbCql.GetFuturesOptionContract)
            .SetParameters(new GetFuturesOptionContract(contractId))
            .ExecuteQueryAsync(MapToFuturesOptionContract!);
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContract)}", SecuritiesDbCql.DeleteFuturesOptionContract)
                .SetParameters(new DeleteFuturesOptionContract(contractId))
                .QueueCommand()
        ];
        queuedCommands.AddRange(contracts.Select(contract =>
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)
                .SetParameters(new DeleteFuturesOptionContractBySymbolV2(contract.Symbol, contract.ContractMonth, contract.OptionType, contract.StrikePrice, contract.ContractId))
                .QueueCommand()));
        await this.ExecuteProjectionMutationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            contracts.Select(static contract => contract.Symbol),
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
    }

    /// <summary>
    /// Get a futures option contract from the database by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures option contract to retrieve</param>
    /// <returns>The futures option contract with the specified ID</returns>
    public async Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId)
    {
        var contracts = await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContract)}", SecuritiesDbCql.GetFuturesOptionContract)
            .SetParameters(new GetFuturesOptionContract(contractId))
            .ExecuteQueryAsync(MapToFuturesOptionContract!);
        return contracts.Count switch
        {
            0 => null,
            1 => contracts.First(),
            _ => throw new StorageException(
                $"SecuritiesDb canonical futures-option contractId '{contractId}' is ambiguous across {contracts.Count} rows.")
        };
    }

    public async Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        string contractId,
        CancellationToken cancellationToken)
    {
        var contracts = await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContract)}", SecuritiesDbCql.GetFuturesOptionContract)
            .SetParameters(new GetFuturesOptionContract(contractId))
            .ExecuteQueryAsync(MapToFuturesOptionContract!, cancellationToken);
        return contracts.Count switch
        {
            0 => null,
            1 => contracts.First(),
            _ => throw new StorageException(
                $"SecuritiesDb canonical futures-option contractId '{contractId}' is ambiguous across {contracts.Count} rows.")
        };
    }

    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(
        ICollection<string> contractIds)
    {
        if (contractIds.Count == 0)
            return [];
        return await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsByIds)}", SecuritiesDbCql.GetFuturesOptionContractsByIds)
            .SetParameters(new GetFuturesOptionContractsByIds(contractIds))
            .ExecuteQueryAsync(MapToFuturesOptionContract!);
    }

    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(
        ICollection<string> contractIds,
        CancellationToken cancellationToken)
    {
        if (contractIds.Count == 0)
            return [];
        return await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsByIds)}", SecuritiesDbCql.GetFuturesOptionContractsByIds)
            .SetParameters(new GetFuturesOptionContractsByIds(contractIds))
            .ExecuteQueryAsync(MapToFuturesOptionContract!, cancellationToken);
    }

    /// <summary>
    /// Get all futures option contracts from the database
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns>A list of all futures option contracts</returns>
    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync<FuturesOptionContractReadModel[]>(
            db,
            FuturesOptionContractSymbolProjection,
            symbol,
            () => this.LoadFuturesOptionContractProjectionAsync(symbol),
            () => this.LoadAndPopulateFuturesOptionContractsBySymbolAsync(symbol));
    }

    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync<FuturesOptionContractReadModel[]>(
            db,
            FuturesOptionContractSymbolProjection,
            symbol,
            cancellationToken,
            token => this.LoadFuturesOptionContractProjectionAsync(symbol, token),
            token => this.LoadAndPopulateFuturesOptionContractsBySymbolAsync(symbol, token));
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
        => await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", SecuritiesDbCql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!);

    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(
        CancellationToken cancellationToken)
        => await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", SecuritiesDbCql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!, cancellationToken);

    /// <summary>
    /// Get futures contracts from the database by a list of contract IDs by symbol
    /// </summary>
    /// <param name="contractIds">The list of contract IDs to retrieve</param>
    /// <param name="symbol">The symbol of the futures contracts to retrieve</param>
    /// <returns>A list of futures contracts with the specified IDs</returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol)
        =>  await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractsByIds)}", SecuritiesDbCql.GetFuturesContractsByIds)
            .SetParameters(new GetFuturesContractsByIds(contractIds, symbol))
            .ExecuteQueryAsync(MapToFuturesContract!);

    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(
        ICollection<string> contractIds,
        string symbol,
        CancellationToken cancellationToken)
        => await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractsByIds)}", SecuritiesDbCql.GetFuturesContractsByIds)
            .SetParameters(new GetFuturesContractsByIds(contractIds, symbol))
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken);

    /// <summary>
    /// Get futures contracts from the database by symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync<FuturesContractV3ReadModel[]>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            () => this.LoadFuturesContractProjectionAsync(symbol),
            () => this.LoadAndPopulateFuturesContractsBySymbolAsync(symbol));
    }

    public async Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = DbFactory.SecuritiesDb;
        return await this.ReadProjectionOrFallbackAsync<FuturesContractV3ReadModel[]>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            cancellationToken,
            token => this.LoadFuturesContractProjectionAsync(symbol, token),
            token => this.LoadAndPopulateFuturesContractsBySymbolAsync(symbol, token));
    }

    public async Task EnsureFuturesContractRolloverRowsAsync(
        IReadOnlyCollection<string> symbols,
        DateTime createdOnUtc,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var normalized = this.ValidateAndNormalizeRolloverSymbols(symbols, createdBy);

        foreach (var symbol in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractRolloverIfMissing)}", SecuritiesDbCql.InsertFuturesContractRolloverIfMissing)
                .SetParameters(new InsertFuturesContractRolloverIfMissing(
                    symbol!, createdOnUtc, createdBy))
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<FuturesContractRolloverReadModel?> GetFuturesContractRolloverAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        symbol = this.ValidateAndNormalizeSymbol(symbol);
        return await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractRollover)}", SecuritiesDbCql.GetFuturesContractRollover)
            .SetParameters(new GetFuturesContractRollover(symbol))
            .ExecuteSingleAsync(MapToFuturesContractRollover!, cancellationToken);
    }

    public Task<FuturesContractV3ReadModel?> GetPersistedFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken = default)
        => GetFuturesContractAsync(contractId, cancellationToken);

    public async Task<IReadOnlyCollection<FuturesContractRolloverReadModel>> GetFuturesContractRolloversAsync(
        CancellationToken cancellationToken = default)
        => (await DbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractRollovers)}", SecuritiesDbCql.GetFuturesContractRollovers)
            .ExecuteQueryAsync(MapToFuturesContractRollover!, cancellationToken))
            .ToArray();

    public async Task ReplaceOnTheRunFuturesContractAsync(
        FuturesContractRolloverReadModel rollover,
        FuturesContractV3ReadModel contract,
        CancellationToken cancellationToken = default)
        => await ReplaceFuturesRolloverSetAsync(
            rollover, [contract], cancellationToken);

    public async Task ReplaceFuturesRolloverSetAsync(
        FuturesContractRolloverReadModel rollover,
        IReadOnlyCollection<FuturesContractV3ReadModel> contracts,
        CancellationToken cancellationToken = default)
    {
        this.ValidateRollover(rollover);
        var symbol = rollover.Symbol.Trim().ToUpperInvariant();
        var gate = RolloverMutationGates.GetOrAdd(
            symbol, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await this.ReplaceFuturesRolloverSetCoreAsync(
                rollover, contracts, cancellationToken)
                    .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }



    // Tokens belong to this server lifetime. A restart requires a fresh first page.
    /// <summary>Reads one page from a completed symbol projection without scanning or repairing the base table.</summary>
    public async Task<FuturesOptionContractPageReadModel> GetFuturesOptionContractsPageAsync(
        GetFuturesOptionContractsPageParameter request, CancellationToken cancellationToken = default)
    {
        this.ValidatePagingRequest(request);
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = string.IsNullOrEmpty(request.ContinuationToken) ? null : this.DecodeOptionCursor(request);
        var db = DbFactory.SecuritiesDb;
        var stamp = await this.GetProjectionReadStampAsync(db, FuturesOptionContractSymbolProjection,
            request.Symbol, cancellationToken)
                .ConfigureAwait(false);
        if (stamp is null)
            throw new InvalidOperationException("The option contract catalog is not ready. Complete its symbol projection and retry.");
        if (cursor is not null && cursor.Stamp != stamp.Value)
            throw new InvalidOperationException("The option contract catalog changed. Refresh the list to restart paging.");

        var page = await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsBySymbol)}", SecuritiesDbCql.GetFuturesOptionContractsBySymbol)
            .SetParameters(new GetFuturesOptionContractsBySymbol(request.Symbol))
            .ExecutePageAsync(MapToFuturesOptionContract!, request.PageSize, cursor?.State, cancellationToken)
            .ConfigureAwait(false);
        if (!await this.IsProjectionReadStampCurrentAsync(db, stamp.Value, cancellationToken)
            .ConfigureAwait(false))
            throw new InvalidOperationException("The option contract catalog changed. Refresh the list to restart paging.");
        var token = page.PagingState is null ? null : this.EncodeOptionCursor(
            new OptionPageCursor(request.Symbol, request.PageSize, stamp.Value, page.PagingState));
        return new FuturesOptionContractPageReadModel(page.Items, token);
    }



    internal static OptionExpiryCalendarState MapToOptionExpiryCalendarState<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Framework.Storage.IObjectDataRecord => new(
            row.GetGuid(0), row.GetDateOnly(1), row.GetDateOnly(2), row.GetDateTime(3));

    internal static OptionContractExpiryReadModel MapToOptionContractExpiry<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Framework.Storage.IObjectDataRecord => new()
        {
            Symbol = row.GetString(0),
            ContractId = row.IsNull(1) ? string.Empty : row.GetString(1),
            ExpiryDate = row.GetDateOnly(2),
            ProviderRoot = row.GetString(3),
            OptionFamily = row.GetString(4),
            RefreshedAtUtc = row.GetDateTime(5)
        };

    internal static CachedOptionContractDefinitionReadModel? MapToCachedOptionContractDefinition<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Framework.Storage.IObjectDataRecord
    {
        if (row.IsNull(1) || row.IsNull(5)) return null;
        return new()
        {
            Symbol = row.GetString(0), UnderlyingContractId = row.GetString(1),
            ExpiryDate = row.GetDateOnly(2), ProviderRoot = row.GetString(3),
            OptionFamily = row.GetString(4), Definition = ReferencePayloadCodec.ReadOption(row.GetBytes(5)),
            RefreshedAtUtc = row.GetDateTime(6)
        };
    }

    public async Task<IReadOnlyList<OptionContractExpiryReadModel>> GetOptionContractExpiriesAsync(
        string symbol,
        DateOnly fromExpiry,
        DateOnly throughExpiry,
        CancellationToken cancellationToken = default)
    {
        symbol = this.ValidateAndNormalizeExpiryRange(symbol, fromExpiry, throughExpiry);
        var db = DbFactory.SecuritiesDb;
        var state = await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiryCalendarState)}",
                SecuritiesDbCql.GetOptionContractExpiryCalendarState)
            .SetParameters(new GetOptionContractExpiryCalendarState(symbol))
            .ExecuteSingleAsync(MapToOptionExpiryCalendarState!, cancellationToken);
        if (state is null || fromExpiry < state.CoverageFrom || fromExpiry > state.CoverageThrough)
            return [];
        var rows = (await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiries)}",
                SecuritiesDbCql.GetOptionContractExpiries)
            .SetParameters(new GetOptionContractExpiries(symbol, state.Generation, fromExpiry, state.CoverageThrough))
            .ExecuteQueryAsync(MapToOptionContractExpiry!, cancellationToken)).ToArray();
        var nearestAfter = rows.Where(row => row.ExpiryDate > throughExpiry)
            .Select(row => row.ExpiryDate).OrderBy(date => date).FirstOrDefault();
        return rows.Where(row => !string.IsNullOrWhiteSpace(row.ContractId)
                                 && (row.ExpiryDate <= throughExpiry
                                 || nearestAfter != default && row.ExpiryDate == nearestAfter))
            .DistinctBy(row => (row.ContractId, row.ExpiryDate, row.ProviderRoot)).ToArray();
    }

    public async Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> GetCachedOptionContractDefinitionsAsync(
        string symbol, string underlyingContractId, DateOnly expiryDate,
        IReadOnlyCollection<string>? providerRoots = null, CancellationToken cancellationToken = default)
    {
        symbol = this.ValidateAndNormalizeCachedDefinitionQuery(symbol, underlyingContractId);
        var db = DbFactory.SecuritiesDb;
        var state = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiryCalendarState)}",
                SecuritiesDbCql.GetOptionContractExpiryCalendarState)
            .SetParameters(new GetOptionContractExpiryCalendarState(symbol))
            .ExecuteSingleAsync(MapToOptionExpiryCalendarState!, cancellationToken);
        if (state is null || expiryDate < state.CoverageFrom || expiryDate > state.CoverageThrough) return [];
        var roots = (providerRoots ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetCachedOptionContractDefinitions)}",
                SecuritiesDbCql.GetCachedOptionContractDefinitions)
            .SetParameters(new GetCachedOptionContractDefinitions(symbol, state.Generation, expiryDate))
            .ExecuteQueryAsync(MapToCachedOptionContractDefinition!, cancellationToken);
        return rows.Where(row => row is not null
                                 && string.Equals(row.UnderlyingContractId, underlyingContractId, StringComparison.OrdinalIgnoreCase)
                                 && (roots.Count == 0 || roots.Contains(row.ProviderRoot)))
            .Select(row => row!).DistinctBy(row => row.Definition.ContractId).ToArray();
    }

    public async Task ReplaceOptionContractDefinitionsAsync(
        string symbol,
        DateOnly coverageFrom,
        DateOnly coverageThrough,
        IReadOnlyCollection<CachedOptionContractDefinitionReadModel> definitions,
        CancellationToken cancellationToken = default)
    {
        symbol = this.ValidateAndNormalizeOptionDefinitions(symbol, coverageFrom, coverageThrough, definitions);

        var generation = Guid.NewGuid();
        var refreshedAtUtc = DateTime.UtcNow;
        var db = DbFactory.SecuritiesDb;
        var previous = await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiryCalendarState)}",
                SecuritiesDbCql.GetOptionContractExpiryCalendarState)
            .SetParameters(new GetOptionContractExpiryCalendarState(symbol))
            .ExecuteSingleAsync(MapToOptionExpiryCalendarState!, cancellationToken);
        var definitionCommands = definitions
            .DistinctBy(row => (row.ExpiryDate, row.ProviderRoot.ToUpperInvariant(), row.Definition.ContractId))
            .Select(row => db
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertOptionContractExpiry)}",
                    SecuritiesDbCql.InsertOptionContractExpiry)
                .SetParameters(new InsertOptionContractExpiry(
                    symbol, generation, row.ExpiryDate, row.ProviderRoot.ToUpperInvariant(),
                    row.Definition.ContractId, row.UnderlyingContractId, row.OptionFamily,
                    ReferencePayloadCodec.Write(row.Definition), refreshedAtUtc))
                .QueueCommand())
            .Cast<object>()
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();
        // A generation may contain many thousands of definition payloads. Sending them as one
        // logged CQL batch exceeds Scylla's batch-size limit. Write the unpublished generation
        // sequentially, then publish only its small state pointer after every row succeeds.
        if (definitionCommands.Count > 0)
            await db.ExecuteQueuedCommandsAsync(definitionCommands, false);
        var publish = db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.PublishOptionContractExpiryGeneration)}",
                SecuritiesDbCql.PublishOptionContractExpiryGeneration)
            .SetParameters(new PublishOptionContractExpiryGeneration(
                symbol, generation, coverageFrom, coverageThrough, refreshedAtUtc))
            .QueueCommand();
        cancellationToken.ThrowIfCancellationRequested();
        await db.ExecuteQueuedCommandsAsync([publish], true);
        if (previous is not null && previous.Generation != generation)
        {
            await db.ExecuteQueuedCommandsAsync(
            [
                db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteOptionContractExpiryGeneration)}",
                        SecuritiesDbCql.DeleteOptionContractExpiryGeneration)
                    .SetParameters(new DeleteOptionContractExpiryGeneration(symbol, previous.Generation))
                    .QueueCommand()
            ], true);
        }
    }

}
