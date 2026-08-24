using Microsoft.Extensions.Logging;
using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>
/// securities database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
///  <param name="logger"
public class SecuritiesDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger) 
    : ObjectDataRepository<SecuritiesDbContext>(connectionSettings[SecuritiesDbConnection], logger), ISecuritiesDbContext
{
    public const string SecuritiesDbConnection = "SecuritiesDbConnection";
    internal const string FuturesContractSymbolProjection = "futures_contract_by_symbol_v2";
    internal const string FuturesOptionContractSymbolProjection = "futures_option_contract_by_symbol_v2";
    const int CompletionStateLookupBatchSize = 100;
    const string GlobalProjectionOperationScope = "global";
    const string ProjectionOperationScopeCount = "scope-count";
    const string SymbolProjectionOperationScope = "symbol";
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override IObjectRepository Database => this;

    public ISecuritiesDbReadContext DbReader => this;
    public ISecuritiesDbWriteContext DbWriter => this;

    static FuturesContractV2ReadModel MapToFuturesContract<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            description: e.GetString(1),
            symbol: e.GetString(2),
            localSymbol: e.GetString(3),
            securityType: e.GetString(4),
            currency: e.GetString(5),
            exchange: e.GetString(6),
            multiplier: e.GetString(7),
            lastTradeDate: e.GetDateOnly(8),
            currentlyTraded: e.GetBool(9)
        );

    static FuturesOptionContractReadModel MapToFuturesOptionContract<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
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
        );

    static FuturesContractRolloverReadModel MapToFuturesContractRollover<TDataRecord>(TDataRecord e)
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

    readonly record struct FuturesContractProjectionKey(
        string Symbol,
        bool CurrentlyTraded,
        DateOnly LastTradeDate,
        string ContractId);

    readonly record struct FuturesOptionContractProjectionKey(
        string Symbol,
        DateOnly ContractMonth,
        string OptionType,
        double StrikePrice,
        string ContractId);

    readonly record struct ProjectionState(
        Guid Generation,
        bool IsComplete,
        bool HasNoActiveOperations);

    readonly record struct SymbolProjectionState(
        string Symbol,
        Guid Generation,
        bool IsComplete,
        bool HasNoActiveOperations);

    readonly record struct ProjectionReadStamp(
        string ProjectionName,
        string Symbol,
        Guid Generation,
        bool IsGlobal,
        Guid? GlobalGeneration);

    readonly record struct ProjectionOperationJournalEntry(
        Guid OperationId,
        DateTime StartedOn,
        bool StateMayBeActive);

    readonly record struct ProjectionOperationScope(
        string ScopeType,
        string ScopeKey);

    sealed record ProjectionOperation(
        Guid OperationId,
        string ProjectionName,
        bool GlobalWasComplete,
        HashSet<string> CompletedSymbols,
        string[] AffectedSymbols);

    sealed record ProjectionInventory(
        HashSet<FuturesContractProjectionKey> FuturesContractSourceKeys,
        HashSet<FuturesContractProjectionKey> FuturesContractTargetKeys,
        HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractSourceKeys,
        HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractTargetKeys,
        int FuturesContractSourceRows,
        int FuturesContractTargetRows,
        int FuturesOptionContractSourceRows,
        int FuturesOptionContractTargetRows);

    static FuturesContractProjectionKey MapToFuturesContractProjectionKey<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetString(0), e.GetBool(1), e.GetDateOnly(2), e.GetString(3));

    static FuturesOptionContractProjectionKey MapToFuturesOptionContractProjectionKey<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetString(0), e.GetDateOnly(1), e.GetString(2), e.GetDouble(3), e.GetString(4));

    static FuturesContractProjectionKey ToProjectionKey(FuturesContractV2ReadModel contract)
        => new(contract.Symbol, contract.CurrentlyTraded, contract.LastTradeDate, contract.ContractId);

    static FuturesOptionContractProjectionKey ToProjectionKey(FuturesOptionContractReadModel contract)
        => new(contract.Symbol, contract.ContractMonth, contract.OptionType, contract.StrikePrice, contract.ContractId);

    static bool MapToBoolean(IObjectDataRecord e)
        => e.GetBool(0);

    static ProjectionState MapToProjectionState(IObjectDataRecord e)
        => new(e.GetGuid(0), e.GetBool(1), e.IsCollectionEmpty(2));

    static SymbolProjectionState MapToSymbolProjectionState(IObjectDataRecord e)
        => new(e.GetString(0), e.GetGuid(1), e.GetBool(2), e.IsCollectionEmpty(3));

    static ProjectionOperationJournalEntry MapToProjectionOperationJournalEntry(IObjectDataRecord e)
        => new(e.GetGuid(0), e.GetDateTime(1), e.GetBool(2));

    static ProjectionOperationScope MapToProjectionOperationScope(IObjectDataRecord e)
        => new(e.GetString(0), e.GetString(1));

    static InsertFuturesContract ToInsertParameters(FuturesContractV2ReadModel contract)
        => new(
            contract.ContractId,
            contract.Description,
            contract.Symbol,
            contract.LocalSymbol,
            contract.SecurityType,
            contract.Currency,
            contract.Exchange,
            contract.Multiplier,
            contract.LastTradeDate,
            contract.CurrentlyTraded);

    static InsertFuturesOptionContract ToInsertParameters(FuturesOptionContractReadModel contract)
        => new(
            contract.ContractId,
            contract.Description,
            contract.Symbol,
            contract.LocalSymbol,
            contract.SecurityType,
            contract.Currency,
            contract.Exchange,
            contract.Multiplier,
            contract.ContractMonth,
            contract.StrikePrice,
            contract.OptionType);

    static void EnsureDistinctFuturesContractWrites(
        IEnumerable<FuturesContractV2ReadModel> contracts)
    {
        var keysByContractId = new Dictionary<string, FuturesContractProjectionKey>(StringComparer.Ordinal);
        foreach (var contract in contracts)
        {
            var key = ToProjectionKey(contract);
            if (!keysByContractId.TryAdd(contract.ContractId, key))
            {
                throw new ArgumentException(
                    $"The futures-contract write contains duplicate or ambiguous contractId '{contract.ContractId}'.",
                    nameof(contracts));
            }
        }
    }

    static void EnsureDistinctFuturesOptionContractWrites(
        IEnumerable<FuturesOptionContractReadModel> contracts)
    {
        var keysByContractId = new Dictionary<string, FuturesOptionContractProjectionKey>(StringComparer.Ordinal);
        foreach (var contract in contracts)
        {
            var key = ToProjectionKey(contract);
            if (!keysByContractId.TryAdd(contract.ContractId, key))
            {
                throw new ArgumentException(
                    $"The futures-option write contains duplicate or ambiguous contractId '{contract.ContractId}'.",
                    nameof(contracts));
            }
        }
    }

    static async Task<ProjectionState?> ReadProjectionStateAsync(
        IObjectRepository db,
        string projectionName,
        CancellationToken cancellationToken = default)
    {
        var states = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionStateV3)}", SecuritiesDbCql.GetSecuritiesProjectionStateV3)
            .SetParameters(new GetSecuritiesProjectionStateV3(projectionName))
            .ExecuteQueryAsync(MapToProjectionState!, cancellationToken);
        return states.Count == 1 ? states.First() : null;
    }

    static async Task<ProjectionState?> ReadSymbolProjectionStateAsync(
        IObjectRepository db,
        string projectionName,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var states = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3)}", SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3)
            .SetParameters(new GetSecuritiesSymbolProjectionStateV3(projectionName, symbol))
            .ExecuteQueryAsync(static row => new ProjectionState(
                row.GetGuid(0),
                row.GetBool(1),
                row.IsCollectionEmpty(2)), cancellationToken);
        return states.Count == 1 ? states.First() : null;
    }

    static async Task InvalidateGlobalProjectionStateAsync(
        IObjectRepository db,
        string projectionName,
        CancellationToken cancellationToken = default)
        => await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3)}", SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3)
            .SetParameters(new InvalidateSecuritiesProjectionStateV3(Guid.NewGuid(), projectionName))
            .ExecuteCommandAsync(cancellationToken);

    static async Task<ProjectionReadStamp?> GetProjectionReadStampAsync(
        IObjectRepository db,
        string projectionName,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var global = await ReadProjectionStateAsync(db, projectionName, cancellationToken).ConfigureAwait(false);
        if (global is { HasNoActiveOperations: false })
            return null;
        if (global is { IsComplete: true, HasNoActiveOperations: true })
        {
            return new ProjectionReadStamp(
                projectionName,
                symbol,
                global.Value.Generation,
                IsGlobal: true,
                GlobalGeneration: global.Value.Generation);
        }

        var symbolState = await ReadSymbolProjectionStateAsync(db, projectionName, symbol, cancellationToken).ConfigureAwait(false);
        return symbolState is { IsComplete: true, HasNoActiveOperations: true }
            ? new ProjectionReadStamp(
                projectionName,
                symbol,
                symbolState.Value.Generation,
                IsGlobal: false,
                GlobalGeneration: global?.Generation)
            : null;
    }

    static async Task<bool> IsProjectionReadStampCurrentAsync(
        IObjectRepository db,
        ProjectionReadStamp stamp,
        CancellationToken cancellationToken = default)
    {
        var global = await ReadProjectionStateAsync(db, stamp.ProjectionName, cancellationToken).ConfigureAwait(false);
        if (stamp.IsGlobal)
        {
            return global is { IsComplete: true, HasNoActiveOperations: true } &&
                global.Value.Generation == stamp.Generation;
        }

        var symbol = await ReadSymbolProjectionStateAsync(
            db,
            stamp.ProjectionName,
            stamp.Symbol,
            cancellationToken).ConfigureAwait(false);
        return IsSymbolProjectionReadFenceCurrent(
            stamp.GlobalGeneration,
            global?.Generation,
            global?.IsComplete ?? false,
            global?.HasNoActiveOperations ?? true,
            stamp.Generation,
            symbol?.Generation,
            symbol?.IsComplete ?? false,
            symbol?.HasNoActiveOperations ?? false);
    }

    internal static bool IsSymbolProjectionReadFenceCurrent(
        Guid? stampedGlobalGeneration,
        Guid? currentGlobalGeneration,
        bool currentGlobalIsComplete,
        bool currentGlobalHasNoActiveOperations,
        Guid stampedSymbolGeneration,
        Guid? currentSymbolGeneration,
        bool currentSymbolIsComplete,
        bool currentSymbolHasNoActiveOperations)
        => stampedGlobalGeneration == currentGlobalGeneration &&
            !currentGlobalIsComplete &&
            currentGlobalHasNoActiveOperations &&
            currentSymbolGeneration == stampedSymbolGeneration &&
            currentSymbolIsComplete &&
            currentSymbolHasNoActiveOperations;

    static async Task<TResult> ReadProjectionOrFallbackAsync<TResult>(
        IObjectRepository db,
        string projectionName,
        string symbol,
        Func<Task<TResult>> readProjection,
        Func<Task<TResult>> readFallback)
    {
        var stamp = await GetProjectionReadStampAsync(db, projectionName, symbol).ConfigureAwait(false);
        if (stamp is null)
            return await readFallback().ConfigureAwait(false);

        var projected = await readProjection().ConfigureAwait(false);
        return await IsProjectionReadStampCurrentAsync(db, stamp.Value).ConfigureAwait(false)
            ? projected
            : await readFallback().ConfigureAwait(false);
    }

    static async Task<TResult> ReadProjectionOrFallbackAsync<TResult>(
        IObjectRepository db,
        string projectionName,
        string symbol,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<TResult>> readProjection,
        Func<CancellationToken, Task<TResult>> readFallback)
    {
        var stamp = await GetProjectionReadStampAsync(
            db,
            projectionName,
            symbol,
            cancellationToken).ConfigureAwait(false);
        if (stamp is null)
            return await readFallback(cancellationToken).ConfigureAwait(false);

        var projected = await readProjection(cancellationToken).ConfigureAwait(false);
        return await IsProjectionReadStampCurrentAsync(
            db,
            stamp.Value,
            cancellationToken).ConfigureAwait(false)
            ? projected
            : await readFallback(cancellationToken).ConfigureAwait(false);
    }

    static async Task<ProjectionOperation> BeginProjectionOperationAsync(
        IObjectRepository db,
        string projectionName,
        IEnumerable<string> affectedSymbols,
        CancellationToken cancellationToken = default)
    {
        var symbols = affectedSymbols
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var globalWasComplete = (await ReadProjectionStateAsync(db, projectionName, cancellationToken).ConfigureAwait(false))
            is { IsComplete: true, HasNoActiveOperations: true };
        var completedSymbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbolBatch in symbols.Chunk(CompletionStateLookupBatchSize))
        {
            var states = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesSymbolProjectionStatesV3)}", SecuritiesDbCql.GetSecuritiesSymbolProjectionStatesV3)
                .SetParameters(new GetSecuritiesSymbolProjectionStatesV3(projectionName, symbolBatch))
                .ExecuteQueryAsync(MapToSymbolProjectionState!, cancellationToken);
            foreach (var state in states)
            {
                if (state.IsComplete && state.HasNoActiveOperations)
                    completedSymbols.Add(state.Symbol);
            }
        }

        var operationId = Guid.NewGuid();
        var activeOperations = new HashSet<Guid> { operationId };
        var operation = new ProjectionOperation(
            operationId,
            projectionName,
            globalWasComplete,
            completedSymbols,
            symbols);
        var journalActivated = false;
        var activationResponseUnknown = false;

        try
        {
            // The catalog begins in a preparation phase. Recovery can discard a torn
            // preparation without touching state because invalidation starts only after
            // every scope is durable and the phase is conditionally advanced.
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertSecuritiesProjectionOperationV3)}", SecuritiesDbCql.InsertSecuritiesProjectionOperationV3)
                .SetParameters(new InsertSecuritiesProjectionOperationV3(
                    projectionName,
                    operationId,
                    DateTime.UtcNow))
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertSecuritiesProjectionOperationScopeV3)}", SecuritiesDbCql.InsertSecuritiesProjectionOperationScopeV3)
                .SetParameters(
                    new[]
                    {
                        new InsertSecuritiesProjectionOperationScopeV3(
                            projectionName,
                            operationId,
                            GlobalProjectionOperationScope,
                            projectionName),
                        new InsertSecuritiesProjectionOperationScopeV3(
                            projectionName,
                            operationId,
                            ProjectionOperationScopeCount,
                            (symbols.Length + 1).ToString(CultureInfo.InvariantCulture))
                    }.Concat(symbols.Select(symbol =>
                        new InsertSecuritiesProjectionOperationScopeV3(
                            projectionName,
                            operationId,
                            SymbolProjectionOperationScope,
                            symbol))))
                .ExecuteCommandAsync(cancellationToken);
            activationResponseUnknown = true;
            var journalActivationApplied = await db
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)}", SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)
                .SetParameters(new SetSecuritiesProjectionOperationStateMayBeActiveV3(
                    true,
                    projectionName,
                    operationId,
                    false))
                .ExecuteSingleAsync(MapToBoolean!, cancellationToken);
            activationResponseUnknown = false;
            if (journalActivationApplied != true)
            {
                throw new StorageException(
                    $"SecuritiesDb could not activate projection operation {operationId}; no data was changed.");
            }
            journalActivated = true;

            activationResponseUnknown = true;
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.BeginSecuritiesProjectionOperationV3)}", SecuritiesDbCql.BeginSecuritiesProjectionOperationV3)
                .SetParameters(new BeginSecuritiesProjectionOperationV3(
                    operationId,
                    activeOperations,
                    projectionName))
                .ExecuteCommandAsync(cancellationToken);
            activationResponseUnknown = false;
            if (symbols.Length > 0)
            {
                activationResponseUnknown = true;
                await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(symbols.Select(symbol =>
                        new BeginSecuritiesSymbolProjectionOperationV3(
                            operationId,
                            activeOperations,
                            projectionName,
                            symbol)))
                    .ExecuteCommandAsync(cancellationToken);
                activationResponseUnknown = false;
            }
            return operation;
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted: false,
                activationResponseConfirmed: !activationResponseUnknown))
            {
                if (journalActivated)
                    await EndProjectionOperationAsync(db, operation, CancellationToken.None).ConfigureAwait(false);
                else
                    await DeleteProjectionOperationJournalAsync(db, operation, CancellationToken.None).ConfigureAwait(false);
            }
            // An activation/set-add request can apply after a timeout. Preserve its
            // original journal when the response is unknown; only explicit stale
            // recovery may classify it after writers have drained.
            throw;
        }
    }

    static async Task EndProjectionOperationAsync(
        IObjectRepository db,
        ProjectionOperation operation,
        CancellationToken cancellationToken = default)
    {
        var endGeneration = Guid.NewGuid();
        var activeOperations = new HashSet<Guid> { operation.OperationId };
        if (operation.AffectedSymbols.Length > 0)
        {
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)
                .SetParameters(operation.AffectedSymbols.Select(symbol =>
                    new EndSecuritiesSymbolProjectionOperationV3(
                        endGeneration,
                        activeOperations,
                        operation.ProjectionName,
                        symbol)))
                .ExecuteCommandAsync(cancellationToken);
        }
        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.EndSecuritiesProjectionOperationV3)}", SecuritiesDbCql.EndSecuritiesProjectionOperationV3)
            .SetParameters(new EndSecuritiesProjectionOperationV3(
                endGeneration,
                activeOperations,
                operation.ProjectionName))
            .ExecuteCommandAsync(cancellationToken);
        await DeleteProjectionOperationJournalAsync(db, operation, cancellationToken).ConfigureAwait(false);
    }

    static async Task DeleteProjectionOperationJournalAsync(
        IObjectRepository db,
        ProjectionOperation operation,
        CancellationToken cancellationToken = default)
    {
        // State is cleaned first by the caller. Move the journal to an inert phase before
        // deleting scopes so a crash between deletes is distinguishable from live work.
        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)}", SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)
            .SetParameters(new SetSecuritiesProjectionOperationStateMayBeActiveV3(
                false,
                operation.ProjectionName,
                operation.OperationId,
                true))
            .ExecuteSingleAsync(MapToBoolean!);
        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)
            .SetParameters(new DeleteSecuritiesProjectionOperationScopesV3(
                operation.ProjectionName,
                operation.OperationId))
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)
            .SetParameters(new DeleteSecuritiesProjectionOperationV3(
                operation.ProjectionName,
                operation.OperationId))
            .ExecuteCommandAsync(cancellationToken);
    }

    static async Task<bool> CompleteProjectionOperationAsync(
        IObjectRepository db,
        ProjectionOperation operation,
        bool completeGlobal,
        bool completeAllSymbols,
        CancellationToken cancellationToken = default)
    {
        var activeOperations = new HashSet<Guid> { operation.OperationId };
        var allCompleted = true;
        foreach (var symbol in operation.AffectedSymbols)
        {
            var shouldComplete = completeAllSymbols || operation.CompletedSymbols.Contains(symbol);
            if (!shouldComplete)
            {
                await EndSymbolOperationAsync(symbol).ConfigureAwait(false);
                continue;
            }

            var applied = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new CompleteSecuritiesSymbolProjectionOperationV3(
                    activeOperations,
                    operation.ProjectionName,
                    symbol,
                    operation.OperationId,
                    activeOperations))
                .ExecuteSingleAsync(MapToBoolean!);
            if (applied == true)
                continue;

            allCompleted = false;
            await EndSymbolOperationAsync(symbol).ConfigureAwait(false);
        }

        var shouldCompleteGlobal = completeGlobal || operation.GlobalWasComplete;
        if (shouldCompleteGlobal && (!completeGlobal || allCompleted))
        {
            var applied = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.CompleteSecuritiesProjectionOperationV3)}", SecuritiesDbCql.CompleteSecuritiesProjectionOperationV3)
                .SetParameters(new CompleteSecuritiesProjectionOperationV3(
                    activeOperations,
                    operation.ProjectionName,
                    operation.OperationId,
                    activeOperations))
                .ExecuteSingleAsync(MapToBoolean!);
            if (applied == true)
            {
                await DeleteProjectionOperationJournalAsync(
                    db,
                    operation,
                    CancellationToken.None).ConfigureAwait(false);
                return allCompleted;
            }

            allCompleted = false;
        }

        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.EndSecuritiesProjectionOperationV3)}", SecuritiesDbCql.EndSecuritiesProjectionOperationV3)
            .SetParameters(new EndSecuritiesProjectionOperationV3(
                Guid.NewGuid(),
                activeOperations,
                operation.ProjectionName))
            .ExecuteCommandAsync(cancellationToken);
        await DeleteProjectionOperationJournalAsync(
            db,
            operation,
            CancellationToken.None).ConfigureAwait(false);
        return !shouldCompleteGlobal && allCompleted;

        Task<long[]> EndSymbolOperationAsync(string symbol)
            => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new EndSecuritiesSymbolProjectionOperationV3(
                    Guid.NewGuid(),
                    activeOperations,
                    operation.ProjectionName,
                    symbol))
                .ExecuteCommandAsync(cancellationToken);
    }

    static async Task ExecuteProjectionMutationAsync(
        IObjectRepository db,
        string projectionName,
        IEnumerable<string> affectedSymbols,
        Func<Task> mutation)
    {
        var operation = await BeginProjectionOperationAsync(db, projectionName, affectedSymbols);
        // Invoking the mutation begins the submission boundary. It may have applied
        // even when invocation or its returned task fails (for example, a driver
        // timeout), so leave the journal and active state for stale-cutoff recovery.
        var mutationTask = mutation();
        await mutationTask.ConfigureAwait(false);
        await CompleteProjectionOperationAsync(
            db,
            operation,
            completeGlobal: false,
            completeAllSymbols: false).ConfigureAwait(false);
    }

    async Task PopulateFuturesContractSymbolProjectionAsync(
        ICollection<FuturesContractV2ReadModel> contracts,
        CancellationToken cancellationToken = default)
    {
        if (contracts.Count == 0)
            return;

        await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesContractBySymbolV2)
            .SetParameters(contracts.Select(ToInsertParameters))
            .ExecuteCommandAsync(cancellationToken);
    }

    async Task PopulateFuturesOptionContractSymbolProjectionAsync(
        ICollection<FuturesOptionContractReadModel> contracts,
        CancellationToken cancellationToken = default)
    {
        if (contracts.Count == 0)
            return;

        await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
            .SetParameters(contracts.Select(ToInsertParameters))
            .ExecuteCommandAsync(cancellationToken);
    }

    async Task<FuturesContractV2ReadModel[]> LoadFuturesContractProjectionAsync(
        string symbol,
        CancellationToken cancellationToken = default)
        => [.. (await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractsBySymbol)}", SecuritiesDbCql.GetFuturesContractsBySymbol)
            .SetParameters(new GetFuturesContractsBySymbol(symbol))
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken))];

    async Task<FuturesOptionContractReadModel[]> LoadFuturesOptionContractProjectionAsync(
        string symbol,
        CancellationToken cancellationToken = default)
        => [.. (await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsBySymbol)}", SecuritiesDbCql.GetFuturesOptionContractsBySymbol)
            .SetParameters(new GetFuturesOptionContractsBySymbol(symbol))
            .ExecuteQueryAsync(MapToFuturesOptionContract!, cancellationToken))];

    async Task<FuturesContractV2ReadModel[]> LoadAndPopulateFuturesContractsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var db = _dbFactory.SecuritiesDb;
        var operation = await BeginProjectionOperationAsync(
            db,
            FuturesContractSymbolProjection,
            [symbol],
            cancellationToken);
        var targetMutationSubmissionStarted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            targetMutationSubmissionStarted = true;
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesContractBySymbolV2Partition(symbol))
                .ExecuteCommandAsync(CancellationToken.None);

            var matchingContracts = new List<FuturesContractV2ReadModel>();
            await foreach (var contract in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
                .ExecuteStreamAsync(MapToFuturesContract!, CancellationToken.None))
            {
                if (string.Equals(contract.Symbol, symbol, StringComparison.Ordinal))
                    matchingContracts.Add(contract);
            }
            var contracts = matchingContracts
                .OrderByDescending(contract => contract.CurrentlyTraded)
                .ThenByDescending(contract => contract.LastTradeDate)
                .ThenBy(contract => contract.ContractId, StringComparer.Ordinal)
                .ToArray();
            await PopulateFuturesContractSymbolProjectionAsync(contracts, CancellationToken.None);

            var projectedContracts = await LoadFuturesContractProjectionAsync(symbol, CancellationToken.None);
            if (!HasExactFuturesContractKeys(contracts, projectedContracts))
            {
                throw new StorageException(
                    $"SecuritiesDb could not reconcile the '{symbol}' futures-contract symbol projection; completion was not recorded.");
            }

            await CompleteProjectionOperationAsync(
                db,
                operation,
                completeGlobal: false,
                completeAllSymbols: true,
                cancellationToken: CancellationToken.None);
            return contracts;
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                await EndProjectionOperationAsync(db, operation, CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
    }

    async Task<FuturesOptionContractReadModel[]> LoadAndPopulateFuturesOptionContractsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var db = _dbFactory.SecuritiesDb;
        var operation = await BeginProjectionOperationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            [symbol],
            cancellationToken);
        var targetMutationSubmissionStarted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            targetMutationSubmissionStarted = true;
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)}", SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesOptionContractBySymbolV2Partition(symbol))
                .ExecuteCommandAsync(CancellationToken.None);

            var matchingContracts = new List<FuturesOptionContractReadModel>();
            await foreach (var contract in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", SecuritiesDbCql.GetFuturesOptionContracts)
                .ExecuteStreamAsync(MapToFuturesOptionContract!, CancellationToken.None))
            {
                if (string.Equals(contract.Symbol, symbol, StringComparison.Ordinal))
                    matchingContracts.Add(contract);
            }
            var contracts = matchingContracts
                .OrderByDescending(contract => contract.ContractMonth)
                .ThenBy(contract => contract.OptionType, StringComparer.Ordinal)
                .ThenBy(contract => contract.StrikePrice)
                .ThenBy(contract => contract.ContractId, StringComparer.Ordinal)
                .ToArray();
            await PopulateFuturesOptionContractSymbolProjectionAsync(contracts, CancellationToken.None);

            var projectedContracts = await LoadFuturesOptionContractProjectionAsync(symbol, CancellationToken.None);
            if (!HasExactFuturesOptionContractKeys(contracts, projectedContracts))
            {
                throw new StorageException(
                    $"SecuritiesDb could not reconcile the '{symbol}' futures-option symbol projection; completion was not recorded.");
            }

            await CompleteProjectionOperationAsync(
                db,
                operation,
                completeGlobal: false,
                completeAllSymbols: true,
                cancellationToken: CancellationToken.None);
            return contracts;
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                await EndProjectionOperationAsync(db, operation, CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
    }

    static bool HasExactFuturesContractKeys(
        IReadOnlyCollection<FuturesContractV2ReadModel> source,
        IReadOnlyCollection<FuturesContractV2ReadModel> target)
        => source.Count == target.Count
            && source.Select(ToProjectionKey).ToHashSet().SetEquals(target.Select(ToProjectionKey));

    static bool HasExactFuturesOptionContractKeys(
        IReadOnlyCollection<FuturesOptionContractReadModel> source,
        IReadOnlyCollection<FuturesOptionContractReadModel> target)
        => source.Count == target.Count
            && source.Select(ToProjectionKey).ToHashSet().SetEquals(target.Select(ToProjectionKey));

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
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (staleOperationCutoffUtc is { } cutoff)
        {
            if (cutoff.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "The stale-operation cutoff must have DateTimeKind.Utc.",
                    nameof(staleOperationCutoffUtc));
            }
            if (cutoff > DateTime.UtcNow)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(staleOperationCutoffUtc),
                    cutoff,
                    "The stale-operation cutoff cannot be in the future.");
            }
        }

        var db = _dbFactory.SecuritiesDb;
        if (staleOperationCutoffUtc is { } verifiedInactiveCutoffUtc)
        {
            // This is deliberately opt-in. The caller must first drain every Securities
            // projection writer and verify that operations at/before the cutoff cannot
            // resume; age alone is not proof that an operation is dead.
            await RecoverVerifiedInactiveProjectionOperationsAsync(
                db,
                FuturesContractSymbolProjection,
                verifiedInactiveCutoffUtc,
                cancellationToken).ConfigureAwait(false);
            await RecoverVerifiedInactiveProjectionOperationsAsync(
                db,
                FuturesOptionContractSymbolProjection,
                verifiedInactiveCutoffUtc,
                cancellationToken).ConfigureAwait(false);
        }

        // Cutover is disabled before validation starts. An ambiguous canonical identity
        // or an inventory failure must never leave a previous global completion visible.
        await InvalidateGlobalProjectionStateAsync(
            db,
            FuturesContractSymbolProjection,
            cancellationToken);
        await InvalidateGlobalProjectionStateAsync(
            db,
            FuturesOptionContractSymbolProjection,
            cancellationToken);

        // Inventory both sides before deleting target partitions. The source identity
        // validation prevents an arbitrary symbol from winning for APIs keyed by contractId.
        var inventory = await ReadProjectionInventoryAsync(db, cancellationToken);
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
            futuresOperation = await BeginProjectionOperationAsync(
                db,
                FuturesContractSymbolProjection,
                futuresContractSymbols,
                cancellationToken);
            futuresOptionOperation = await BeginProjectionOperationAsync(
                db,
                FuturesOptionContractSymbolProjection,
                futuresOptionContractSymbols,
                cancellationToken);

            targetMutationSubmissionStarted = true;
            if (futuresContractSymbols.Length > 0)
            {
                await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                    .SetParameters(futuresContractSymbols.Select(static symbol =>
                        new DeleteFuturesContractBySymbolV2Partition(symbol)))
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
            var futuresContracts = new List<FuturesContractV2ReadModel>(batchSize);
            await foreach (var contract in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
                .ExecuteStreamAsync(MapToFuturesContract!, cancellationToken))
            {
                futuresContracts.Add(contract);
                if (futuresContracts.Count < batchSize)
                    continue;

                await PopulateFuturesContractSymbolProjectionAsync(futuresContracts, cancellationToken);
                futuresContractsUpserted += futuresContracts.Count;
                futuresContracts.Clear();
            }
            await PopulateFuturesContractSymbolProjectionAsync(futuresContracts, cancellationToken);
            futuresContractsUpserted += futuresContracts.Count;

            var futuresOptionContracts = new List<FuturesOptionContractReadModel>(batchSize);
            await foreach (var contract in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", SecuritiesDbCql.GetFuturesOptionContracts)
                .ExecuteStreamAsync(MapToFuturesOptionContract!, cancellationToken))
            {
                futuresOptionContracts.Add(contract);
                if (futuresOptionContracts.Count < batchSize)
                    continue;

                await PopulateFuturesOptionContractSymbolProjectionAsync(futuresOptionContracts, cancellationToken);
                futuresOptionContractsUpserted += futuresOptionContracts.Count;
                futuresOptionContracts.Clear();
            }
            await PopulateFuturesOptionContractSymbolProjectionAsync(futuresOptionContracts, cancellationToken);
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

            var futuresCompleted = await CompleteProjectionOperationAsync(
                db,
                futuresOperation,
                completeGlobal: true,
                completeAllSymbols: true,
                cancellationToken);
            var futuresOptionsCompleted = await CompleteProjectionOperationAsync(
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
                    await EndProjectionOperationAsync(db, futuresOptionOperation, CancellationToken.None).ConfigureAwait(false);
                if (futuresOperation is not null)
                    await EndProjectionOperationAsync(db, futuresOperation, CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
    }

    static async Task RecoverVerifiedInactiveProjectionOperationsAsync(
        IObjectRepository db,
        string projectionName,
        DateTime staleOperationCutoffUtc,
        CancellationToken cancellationToken)
    {
        var journalEntries = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)}", SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)
            .SetParameters(new GetSecuritiesProjectionOperationsV3(projectionName))
            .ExecuteQueryAsync(MapToProjectionOperationJournalEntry!);
        var staleEntries = journalEntries
            .Where(entry => AsUtc(entry.StartedOn) <= staleOperationCutoffUtc)
            .ToArray();
        if (staleEntries.Length == 0)
            return;

        var globalOperationIds = new HashSet<Guid>();
        var operationIdsBySymbol = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        foreach (var entry in staleEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.StateMayBeActive)
                continue;

            var scopes = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.GetSecuritiesProjectionOperationScopesV3)
                .SetParameters(new GetSecuritiesProjectionOperationScopesV3(
                    projectionName,
                    entry.OperationId))
                .ExecuteQueryAsync(MapToProjectionOperationScope!);
            var hasGlobalScope = false;
            var stateScopeCount = 0;
            int? expectedStateScopeCount = null;
            foreach (var scope in scopes)
            {
                if (scope.ScopeType == ProjectionOperationScopeCount)
                {
                    if (expectedStateScopeCount.HasValue ||
                        !int.TryParse(
                            scope.ScopeKey,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var scopeCount) ||
                        scopeCount < 1)
                    {
                        throw new StorageException(
                            $"SecuritiesDb projection operation {entry.OperationId} has an invalid scope count. " +
                            "No stale operation was cleared.");
                    }
                    expectedStateScopeCount = scopeCount;
                    continue;
                }

                if (scope.ScopeType == GlobalProjectionOperationScope &&
                    scope.ScopeKey == projectionName)
                {
                    hasGlobalScope = true;
                    stateScopeCount++;
                    globalOperationIds.Add(entry.OperationId);
                    continue;
                }

                if (scope.ScopeType != SymbolProjectionOperationScope ||
                    string.IsNullOrEmpty(scope.ScopeKey))
                {
                    throw new StorageException(
                        $"SecuritiesDb projection operation {entry.OperationId} has an invalid journal scope. " +
                        "No stale operation was cleared.");
                }

                if (!operationIdsBySymbol.TryGetValue(scope.ScopeKey, out var operationIds))
                {
                    operationIds = [];
                    operationIdsBySymbol.Add(scope.ScopeKey, operationIds);
                }
                stateScopeCount++;
                operationIds.Add(entry.OperationId);
            }
            if (!hasGlobalScope ||
                expectedStateScopeCount is null ||
                stateScopeCount != expectedStateScopeCount.Value)
            {
                throw new StorageException(
                    $"SecuritiesDb projection operation {entry.OperationId} has an incomplete journal. " +
                    "No stale operation was cleared.");
            }
        }

        // Exact collection-element tombstones are idempotent and commutative. Unlike an
        // UPDATE, DELETE cannot manufacture a partially-null state row when activation
        // was journaled but the process died before its first state write.
        if (globalOperationIds.Count > 0)
        {
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)}", SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)
                .SetParameters(globalOperationIds.Select(operationId =>
                    new RemoveSecuritiesProjectionOperationV3(
                        operationId,
                        projectionName)))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (operationIdsBySymbol.Count > 0)
        {
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)
                .SetParameters(operationIdsBySymbol.SelectMany(entry =>
                    entry.Value.Select(operationId =>
                        new RemoveSecuritiesSymbolProjectionOperationV3(
                            operationId,
                            projectionName,
                            entry.Key))))
                .ExecuteCommandAsync(cancellationToken);
        }

        // Delete exact journal partitions only after every recorded state scope has been
        // cleaned. Any interruption leaves the catalog entry available for an idempotent retry.
        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)
            .SetParameters(staleEntries.Select(entry =>
                new DeleteSecuritiesProjectionOperationScopesV3(
                    projectionName,
                    entry.OperationId)))
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)
            .SetParameters(staleEntries.Select(entry =>
                new DeleteSecuritiesProjectionOperationV3(
                    projectionName,
                    entry.OperationId)))
            .ExecuteCommandAsync(cancellationToken);

        static DateTime AsUtc(DateTime value)
            => value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    /// <summary>
    /// Streams canonical and projected primary keys and reports missing or unexpected projection rows.
    /// </summary>
    public async Task<SecuritiesProjectionReconciliationResult> ReconcileSymbolProjectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var inventory = await ReadProjectionInventoryAsync(_dbFactory.SecuritiesDb, cancellationToken);
        return new SecuritiesProjectionReconciliationResult(
            inventory.FuturesContractSourceRows,
            inventory.FuturesContractTargetRows,
            CountMissing(inventory.FuturesContractSourceKeys, inventory.FuturesContractTargetKeys),
            CountMissing(inventory.FuturesContractTargetKeys, inventory.FuturesContractSourceKeys),
            inventory.FuturesOptionContractSourceRows,
            inventory.FuturesOptionContractTargetRows,
            CountMissing(inventory.FuturesOptionContractSourceKeys, inventory.FuturesOptionContractTargetKeys),
            CountMissing(inventory.FuturesOptionContractTargetKeys, inventory.FuturesOptionContractSourceKeys));
    }

    static async Task<ProjectionInventory> ReadProjectionInventoryAsync(
        IObjectRepository db,
        CancellationToken cancellationToken)
    {
        var futuresContractSourceKeys = new HashSet<FuturesContractProjectionKey>();
        var futuresContractProjectionKeys = new HashSet<FuturesContractProjectionKey>();
        var futuresOptionContractSourceKeys = new HashSet<FuturesOptionContractProjectionKey>();
        var futuresOptionContractProjectionKeys = new HashSet<FuturesOptionContractProjectionKey>();
        var futuresContractsById = new Dictionary<string, FuturesContractProjectionKey>(StringComparer.Ordinal);
        var futuresOptionContractsById = new Dictionary<string, FuturesOptionContractProjectionKey>(StringComparer.Ordinal);
        var futuresContractSourceRows = 0;
        var futuresContractProjectionRows = 0;
        var futuresOptionContractSourceRows = 0;
        var futuresOptionContractProjectionRows = 0;

        await foreach (var key in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractProjectionSourceKeys)}", SecuritiesDbCql.GetFuturesContractProjectionSourceKeys)
            .ExecuteStreamAsync(MapToFuturesContractProjectionKey!, cancellationToken))
        {
            futuresContractSourceRows++;
            futuresContractSourceKeys.Add(key);
            if (futuresContractsById.TryGetValue(key.ContractId, out var existing) && existing != key)
            {
                throw new StorageException(
                    $"SecuritiesDb canonical futures contract '{key.ContractId}' maps to multiple symbol/date keys " +
                    $"('{existing.Symbol}'/{existing.LastTradeDate:yyyy-MM-dd} and " +
                    $"'{key.Symbol}'/{key.LastTradeDate:yyyy-MM-dd}). Resolve the ambiguous contract before cutover.");
            }
            futuresContractsById[key.ContractId] = key;
        }
        await foreach (var key in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractProjectionTargetKeys)}", SecuritiesDbCql.GetFuturesContractProjectionTargetKeys)
            .ExecuteStreamAsync(MapToFuturesContractProjectionKey!, cancellationToken))
        {
            futuresContractProjectionRows++;
            futuresContractProjectionKeys.Add(key);
        }
        await foreach (var key in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractProjectionSourceKeys)}", SecuritiesDbCql.GetFuturesOptionContractProjectionSourceKeys)
            .ExecuteStreamAsync(MapToFuturesOptionContractProjectionKey!, cancellationToken))
        {
            futuresOptionContractSourceRows++;
            futuresOptionContractSourceKeys.Add(key);
            if (futuresOptionContractsById.TryGetValue(key.ContractId, out var existing) && existing != key)
            {
                throw new StorageException(
                    $"SecuritiesDb canonical futures-option contract '{key.ContractId}' maps to multiple symbol/contract keys " +
                    $"('{existing.Symbol}'/{existing.ContractMonth:yyyy-MM-dd} and " +
                    $"'{key.Symbol}'/{key.ContractMonth:yyyy-MM-dd}). Resolve the ambiguous contract before cutover.");
            }
            futuresOptionContractsById[key.ContractId] = key;
        }
        await foreach (var key in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractProjectionTargetKeys)}", SecuritiesDbCql.GetFuturesOptionContractProjectionTargetKeys)
            .ExecuteStreamAsync(MapToFuturesOptionContractProjectionKey!, cancellationToken))
        {
            futuresOptionContractProjectionRows++;
            futuresOptionContractProjectionKeys.Add(key);
        }

        return new ProjectionInventory(
            futuresContractSourceKeys,
            futuresContractProjectionKeys,
            futuresOptionContractSourceKeys,
            futuresOptionContractProjectionKeys,
            futuresContractSourceRows,
            futuresContractProjectionRows,
            futuresOptionContractSourceRows,
            futuresOptionContractProjectionRows);
    }

    static int CountMissing<TKey>(HashSet<TKey> expected, HashSet<TKey> actual)
        where TKey : notnull
    {
        var count = 0;
        foreach (var key in expected)
        {
            if (!actual.Contains(key))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Insert a new futures contract into SecuritiesDb
    /// </summary>
    /// <param name="futuresContract">The futures contract to insert</param>
    /// <returns></returns>
    public async Task InsertFuturesContractAsync(FuturesContractV2ReadModel futuresContract)
    {
        var db = _dbFactory.SecuritiesDb;
        var parameters = ToInsertParameters(futuresContract);
        List<object> queuedCommands =
        [
            // The opposite status is a different clustering key, so this cannot
            // tombstone the replacement row at the logged batch timestamp.
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                .SetParameters(new DeleteFuturesContractBySymbolV2(
                    futuresContract.Symbol,
                    !futuresContract.CurrentlyTraded,
                    futuresContract.LastTradeDate,
                    futuresContract.ContractId))
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(parameters)
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesContractBySymbolV2)
                .SetParameters(parameters)
                .QueueCommand()
        ];
        await ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            [futuresContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
    }

    /// <summary>
    /// Asynchronously inserts a collection of futures contracts into the database.
    /// </summary>
    /// <remarks>This method uses the database factory to execute an insert command for each futures contract
    /// in the provided collection. Ensure that the collection is not null and contains valid futures contract data to
    /// avoid exceptions during execution.</remarks>
    /// <param name="futuresContracts">A collection of <see cref="FuturesContractV2ReadModel"/> objects representing the futures contracts to be
    /// inserted. Each contract must have valid properties set, such as contract ID, description, symbol, and other
    /// relevant details.</param>
    /// <returns></returns>
    public async Task InsertFuturesContractsAsync(ICollection<FuturesContractV2ReadModel> futuresContracts)
    {
        if (futuresContracts.Count == 0)
            return;

        var db = _dbFactory.SecuritiesDb;
        EnsureDistinctFuturesContractWrites(futuresContracts);
        var operation = await BeginProjectionOperationAsync(
            db,
            FuturesContractSymbolProjection,
            futuresContracts.Select(static contract => contract.Symbol));
        var targetMutationSubmissionStarted = false;
        try
        {
            targetMutationSubmissionStarted = true;
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                .SetParameters(futuresContracts.Select(static contract =>
                    new DeleteFuturesContractBySymbolV2(
                        contract.Symbol,
                        !contract.CurrentlyTraded,
                        contract.LastTradeDate,
                        contract.ContractId)))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(futuresContracts.Select(ToInsertParameters))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesContractBySymbolV2)
                .SetParameters(futuresContracts.Select(ToInsertParameters))
                .ExecuteCommandAsync();
            await CompleteProjectionOperationAsync(
                db,
                operation,
                completeGlobal: false,
                completeAllSymbols: false);
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                await EndProjectionOperationAsync(db, operation, CancellationToken.None).ConfigureAwait(false);
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
    public async Task UpdateFuturesContractAsync(FuturesContractId e, FuturesContractV2ReadModel futuresContract)
    {
        var db = _dbFactory.SecuritiesDb;
        var originalContract = await GetFuturesContractAsync(e);
        var replacementProjectionKey = ToProjectionKey(futuresContract);
        List<object> queuedCommands = [];

        if (originalContract is not null)
        {
            var originalProjectionKey = ToProjectionKey(originalContract);
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
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                    .SetParameters(new DeleteFuturesContractBySymbolV2(
                        originalContract.Symbol,
                        originalContract.CurrentlyTraded,
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
            foreach (var currentlyTraded in new[] { false, true })
            {
                var candidate = new FuturesContractProjectionKey(
                    e.Symbol,
                    currentlyTraded,
                    e.MaturityDate,
                    e.ContractId);
                if (candidate == replacementProjectionKey)
                    continue;

                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                    .SetParameters(new DeleteFuturesContractBySymbolV2(
                        candidate.Symbol,
                        candidate.CurrentlyTraded,
                        candidate.LastTradeDate,
                        candidate.ContractId))
                    .QueueCommand());
            }
        }

        queuedCommands.AddRange([
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(ToInsertParameters(futuresContract))
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesContractBySymbolV2)
                .SetParameters(ToInsertParameters(futuresContract))
                .QueueCommand()]);
        await ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            [e.Symbol, futuresContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
    }

    /// Delete a futures contract from SecuritiesDb by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to delete</param>
    /// <returns></returns>
    public async Task DeleteFuturesContractAsync(string contractId)
    {
        var db = _dbFactory.SecuritiesDb;
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
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                .SetParameters(new DeleteFuturesContractBySymbolV2(
                    contract.Symbol,
                    contract.CurrentlyTraded,
                    contract.LastTradeDate,
                    contract.ContractId))
                .QueueCommand()));
        await ExecuteProjectionMutationAsync(
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
        var db = _dbFactory.SecuritiesDb;
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", SecuritiesDbCql.DeleteFuturesContractById)
                .SetParameters(new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                .SetParameters(new DeleteFuturesContractBySymbolV2(e.Symbol, false, e.MaturityDate, e.ContractId))
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                .SetParameters(new DeleteFuturesContractBySymbolV2(e.Symbol, true, e.MaturityDate, e.ContractId))
                .QueueCommand()
        ];
        await ExecuteProjectionMutationAsync(
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
    public async Task DeleteCurrentlyTradedFuturesContractAsync(string symbol)
    {
        var fc = await GetCurrentlyTradedFuturesContractAsync(symbol);
        if (fc is not null)
            await DeleteFuturesContractAsync(fc.Id);
    }

/// <summary>
    /// Get currently traded futures contract from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<FuturesContractV2ReadModel?> GetCurrentlyTradedFuturesContractAsync(string symbol)
    {
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync(
            db,
            FuturesContractSymbolProjection,
            symbol,
            () => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetCurrentlyTradeFuturesContract)}", SecuritiesDbCql.GetCurrentlyTradeFuturesContract)
                .SetParameters(new GetCurrentlyTradeFuturesContract(symbol))
                .ExecuteSingleAsync(MapToFuturesContract!),
            async () => (await LoadAndPopulateFuturesContractsBySymbolAsync(symbol))
                .FirstOrDefault(static candidate => candidate.CurrentlyTraded));
    }

    public async Task<FuturesContractV2ReadModel?> GetCurrentlyTradedFuturesContractAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync(
            db,
            FuturesContractSymbolProjection,
            symbol,
            cancellationToken,
            token => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetCurrentlyTradeFuturesContract)}", SecuritiesDbCql.GetCurrentlyTradeFuturesContract)
                .SetParameters(new GetCurrentlyTradeFuturesContract(symbol))
                .ExecuteSingleAsync(MapToFuturesContract!, token),
            async token => (await LoadAndPopulateFuturesContractsBySymbolAsync(symbol, token))
                .FirstOrDefault(static candidate => candidate.CurrentlyTraded));
    }

    /// <summary>
    /// Get currently traded futures contracts from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractsAsync(string symbol)
    {
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync<ICollection<FuturesContractV2ReadModel>>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            () => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetCurrentlyTradeFuturesContracts)}", SecuritiesDbCql.GetCurrentlyTradeFuturesContracts)
                .SetParameters(new GetCurrentlyTradeFuturesContracts(symbol))
                .ExecuteQueryAsync(MapToFuturesContract!),
            async () => (await LoadAndPopulateFuturesContractsBySymbolAsync(symbol))
                .Where(static contract => contract.CurrentlyTraded)
                .ToArray());
    }

    public async Task<ICollection<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync<ICollection<FuturesContractV2ReadModel>>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            cancellationToken,
            token => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetCurrentlyTradeFuturesContracts)}", SecuritiesDbCql.GetCurrentlyTradeFuturesContracts)
                .SetParameters(new GetCurrentlyTradeFuturesContracts(symbol))
                .ExecuteQueryAsync(MapToFuturesContract!, token),
            async token => (await LoadAndPopulateFuturesContractsBySymbolAsync(symbol, token))
                .Where(static contract => contract.CurrentlyTraded)
                .ToArray());
    }

    /// <summary>
    /// Get a futures contract from the database by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to retrieve</param>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(string contractId)
    {
        var contracts = await _dbFactory.SecuritiesDb
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

    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken)
    {
        var contracts = await _dbFactory.SecuritiesDb
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
    /// <returns>A <see cref="FuturesContractV2ReadModel"/> representing the futures contract if found; otherwise, <see
    /// langword="null"/>.</returns>
    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(FuturesContractId e)
        => await _dbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractById)}", SecuritiesDbCql.GetFuturesContractById)
                .SetParameters(new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                .ExecuteSingleAsync(MapToFuturesContract!);

    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        FuturesContractId e,
        CancellationToken cancellationToken)
        => await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractById)}", SecuritiesDbCql.GetFuturesContractById)
            .SetParameters(new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
            .ExecuteSingleAsync(MapToFuturesContract!, cancellationToken);

    /// <summary>
    /// Get all futures contracts from the database
    /// </summary>
    /// <returns>A list of all futures contracts</returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsAsync()
        => await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
            .ExecuteQueryAsync(MapToFuturesContract!);

    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsAsync(
        CancellationToken cancellationToken)
        => await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken);

    /// <summary>
    /// Insert a new futures option contract into SecuritiesDb
    /// </summary>
    /// <param name="futuresOptionContract"></param>
    /// <returns></returns>
    public async Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract)
    {
        var db = _dbFactory.SecuritiesDb;
        var parameters = ToInsertParameters(futuresOptionContract);
        List<object> queuedCommands =
        [
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(parameters)
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(parameters)
                .QueueCommand()
        ];
        await ExecuteProjectionMutationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            [futuresOptionContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
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

        var db = _dbFactory.SecuritiesDb;
        EnsureDistinctFuturesOptionContractWrites(futuresOptionContract);
        var operation = await BeginProjectionOperationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            futuresOptionContract.Select(static contract => contract.Symbol));
        var targetMutationSubmissionStarted = false;
        try
        {
            targetMutationSubmissionStarted = true;
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(futuresOptionContract.Select(ToInsertParameters))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(futuresOptionContract.Select(ToInsertParameters))
                .ExecuteCommandAsync();
            await CompleteProjectionOperationAsync(
                db,
                operation,
                completeGlobal: false,
                completeAllSymbols: false);
        }
        catch
        {
            if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted))
            {
                await EndProjectionOperationAsync(db, operation, CancellationToken.None).ConfigureAwait(false);
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
        var db = _dbFactory.SecuritiesDb;
        var originalContract = await GetFuturesOptionContractAsync(originalContractId);
        var replacementProjectionKey = ToProjectionKey(futuresOptionContract);
        List<object> queuedCommands = [];
        if (originalContract is not null)
        {
            var originalProjectionKey = ToProjectionKey(originalContract);
            if (originalProjectionKey != replacementProjectionKey)
            {
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractById)}", SecuritiesDbCql.DeleteFuturesOptionContractById)
                    .SetParameters(new DeleteFuturesOptionContractById(
                        originalContract.ContractId,
                        originalContract.ContractMonth,
                        originalContract.Symbol,
                        originalContract.OptionType,
                        originalContract.StrikePrice))
                    .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)
                    .SetParameters(new DeleteFuturesOptionContractBySymbolV2(
                        originalContract.Symbol,
                        originalContract.ContractMonth,
                        originalContract.OptionType,
                        originalContract.StrikePrice,
                        originalContract.ContractId))
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
                .SetParameters(ToInsertParameters(futuresOptionContract))
                .QueueCommand(),
            db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(ToInsertParameters(futuresOptionContract))
                .QueueCommand()]);
        await ExecuteProjectionMutationAsync(
            db,
            FuturesOptionContractSymbolProjection,
            originalContract is null
                ? [futuresOptionContract.Symbol]
                : [originalContract.Symbol, futuresOptionContract.Symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
    }

    /// <summary>
    /// Delete a futures option contract from SecuritiesDb by its ID
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task DeleteFuturesOptionContractAsync(string contractId)
    {
        var db = _dbFactory.SecuritiesDb;
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
                .SetParameters(new DeleteFuturesOptionContractBySymbolV2(
                    contract.Symbol,
                    contract.ContractMonth,
                    contract.OptionType,
                    contract.StrikePrice,
                    contract.ContractId))
                .QueueCommand()));
        await ExecuteProjectionMutationAsync(
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
        var contracts = await _dbFactory.SecuritiesDb
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
        var contracts = await _dbFactory.SecuritiesDb
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
        return await _dbFactory.SecuritiesDb
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
        return await _dbFactory.SecuritiesDb
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
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync<FuturesOptionContractReadModel[]>(
            db,
            FuturesOptionContractSymbolProjection,
            symbol,
            () => LoadFuturesOptionContractProjectionAsync(symbol),
            () => LoadAndPopulateFuturesOptionContractsBySymbolAsync(symbol));
    }

    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync<FuturesOptionContractReadModel[]>(
            db,
            FuturesOptionContractSymbolProjection,
            symbol,
            cancellationToken,
            token => LoadFuturesOptionContractProjectionAsync(symbol, token),
            token => LoadAndPopulateFuturesOptionContractsBySymbolAsync(symbol, token));
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
        => await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", SecuritiesDbCql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!);

    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(
        CancellationToken cancellationToken)
        => await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", SecuritiesDbCql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!, cancellationToken);

    /// <summary>
    /// Get futures contracts from the database by a list of contract IDs by symbol
    /// </summary>
    /// <param name="contractIds">The list of contract IDs to retrieve</param>
    /// <param name="symbol">The symbol of the futures contracts to retrieve</param>
    /// <returns>A list of futures contracts with the specified IDs</returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol)
        =>  await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractsByIds)}", SecuritiesDbCql.GetFuturesContractsByIds)
            .SetParameters(new GetFuturesContractsByIds(contractIds, symbol))
            .ExecuteQueryAsync(MapToFuturesContract!);

    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsByIdsAsync(
        ICollection<string> contractIds,
        string symbol,
        CancellationToken cancellationToken)
        => await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractsByIds)}", SecuritiesDbCql.GetFuturesContractsByIds)
            .SetParameters(new GetFuturesContractsByIds(contractIds, symbol))
            .ExecuteQueryAsync(MapToFuturesContract!, cancellationToken);

    /// <summary>
    /// Get futures contracts from the database by symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsBySymbolAsync(string symbol)
    {
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync<FuturesContractV2ReadModel[]>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            () => LoadFuturesContractProjectionAsync(symbol),
            () => LoadAndPopulateFuturesContractsBySymbolAsync(symbol));
    }

    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.SecuritiesDb;
        return await ReadProjectionOrFallbackAsync<FuturesContractV2ReadModel[]>(
            db,
            FuturesContractSymbolProjection,
            symbol,
            cancellationToken,
            token => LoadFuturesContractProjectionAsync(symbol, token),
            token => LoadAndPopulateFuturesContractsBySymbolAsync(symbol, token));
    }

    public async Task EnsureFuturesContractRolloverRowsAsync(
        IReadOnlyCollection<string> symbols,
        DateTime createdOnUtc,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var normalized = symbols
            .Select(static symbol => symbol?.Trim().ToUpperInvariant())
            .Where(static symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
            throw new ArgumentException("At least one futures symbol is required.", nameof(symbols));

        foreach (var symbol in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _dbFactory.SecuritiesDb
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
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractRollover)}", SecuritiesDbCql.GetFuturesContractRollover)
            .SetParameters(new GetFuturesContractRollover(symbol.Trim().ToUpperInvariant()))
            .ExecuteSingleAsync(MapToFuturesContractRollover!, cancellationToken);
    }

    public Task<FuturesContractV2ReadModel?> GetPersistedFuturesContractAsync(
        string contractId,
        CancellationToken cancellationToken = default)
        => GetFuturesContractAsync(contractId, cancellationToken);

    public async Task<IReadOnlyCollection<FuturesContractRolloverReadModel>> GetFuturesContractRolloversAsync(
        CancellationToken cancellationToken = default)
        => (await _dbFactory.SecuritiesDb
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractRollovers)}", SecuritiesDbCql.GetFuturesContractRollovers)
            .ExecuteQueryAsync(MapToFuturesContractRollover!, cancellationToken))
            .ToArray();

    public async Task ReplaceCurrentlyTradedFuturesContractAsync(
        FuturesContractRolloverReadModel rollover,
        FuturesContractV2ReadModel contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rollover);
        ArgumentNullException.ThrowIfNull(contract);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(rollover.ContractId)
            || rollover.NextRolloverDate is null
            || rollover.UpdatedOn is null
            || string.IsNullOrWhiteSpace(rollover.UpdatedBy))
        {
            throw new ArgumentException("The replacement rollover row must be fully resolved.", nameof(rollover));
        }
        if (!string.Equals(rollover.Symbol, contract.Symbol, StringComparison.Ordinal)
            || !string.Equals(rollover.ContractId, contract.ContractId, StringComparison.Ordinal)
            || !contract.CurrentlyTraded)
        {
            throw new ArgumentException(
                "The rollover row and currently traded contract must identify the same symbol and contract.",
                nameof(contract));
        }

        var symbol = rollover.Symbol.Trim().ToUpperInvariant();
        var existing = await GetCurrentlyTradedFuturesContractsAsync(symbol, cancellationToken);
        var db = _dbFactory.SecuritiesDb;
        List<object> queuedCommands = [];
        foreach (var current in existing)
        {
            if (string.Equals(current.ContractId, contract.ContractId, StringComparison.Ordinal)
                && current.LastTradeDate == contract.LastTradeDate)
                continue;
            queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", SecuritiesDbCql.DeleteFuturesContractById)
                .SetParameters(new DeleteFuturesContractById(
                    current.ContractId, current.Symbol, current.LastTradeDate))
                .QueueCommand());
            queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
                .SetParameters(new DeleteFuturesContractBySymbolV2(
                    current.Symbol, true, current.LastTradeDate, current.ContractId))
                .QueueCommand());
        }

        var insert = ToInsertParameters(contract);
        queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV2)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV2)
            .SetParameters(new DeleteFuturesContractBySymbolV2(
                contract.Symbol, false, contract.LastTradeDate, contract.ContractId))
            .QueueCommand());
        queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
            .SetParameters(insert)
            .QueueCommand());
        queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesContractBySymbolV2)
            .SetParameters(insert)
            .QueueCommand());
        queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.UpdateFuturesContractRollover)}", SecuritiesDbCql.UpdateFuturesContractRollover)
            .SetParameters(new UpdateFuturesContractRollover(
                rollover.ContractId,
                rollover.NextRolloverDate.Value,
                rollover.UpdatedOn.Value,
                rollover.UpdatedBy,
                symbol))
            .QueueCommand());

        await ExecuteProjectionMutationAsync(
            db,
            FuturesContractSymbolProjection,
            [symbol],
            () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
        cancellationToken.ThrowIfCancellationRequested();
    }

}
