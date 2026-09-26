using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal static class SecuritiesDbContextExtensions
{
    static readonly byte[] OptionPageSigningKey = RandomNumberGenerator.GetBytes(32);

    extension(SecuritiesDbContext context)
    {
        /// <summary>
        /// Performs the ToProjectionKey Securities persistence helper operation.
        /// </summary>
        internal FuturesOptionContractProjectionKey ToProjectionKey(FuturesOptionContractReadModel contract)
            => new(contract.Symbol, contract.ContractMonth, contract.OptionType, contract.StrikePrice, contract.ContractId);

        /// <summary>
        /// Performs the ToProjectionKey Securities persistence helper operation.
        /// </summary>
        internal FuturesContractProjectionKey ToProjectionKey(FuturesContractV3ReadModel contract)
            => new(contract.Symbol, contract.Rollover, contract.OnTheRun, contract.LastTradeDate, contract.ContractId);

        /// <summary>
        /// Performs the OtherProjectionKeys Securities persistence helper operation.
        /// </summary>
        internal IEnumerable<FuturesContractProjectionKey> OtherProjectionKeys(
            FuturesContractV3ReadModel contract)
        {
            foreach (var rollover in new[] { false, true })
                foreach (var onTheRun in new[] { false, true })
                {
                    var candidate = new FuturesContractProjectionKey(
                        contract.Symbol, rollover, onTheRun, contract.LastTradeDate, contract.ContractId);
                    if (candidate != context.ToProjectionKey(contract))
                        yield return candidate;
                }
        }

        /// <summary>
        /// Performs the ToInsertParameters Securities persistence helper operation.
        /// </summary>
        internal InsertFuturesOptionContract ToInsertParameters(FuturesOptionContractReadModel contract)
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
                contract.OptionType,
                ReferencePayloadCodec.Write(contract));

        /// <summary>
        /// Performs the ToInsertParameters Securities persistence helper operation.
        /// </summary>
        internal InsertFuturesContract ToInsertParameters(FuturesContractV3ReadModel contract)
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
                contract.OnTheRun,
                contract.Rollover,
                ReferencePayloadCodec.Write(contract));

        /// <summary>
        /// Performs the EnsureDistinctFuturesContractWrites Securities persistence helper operation.
        /// </summary>
        internal void EnsureDistinctFuturesContractWrites(
            IEnumerable<FuturesContractV3ReadModel> contracts)
        {
            var keysByContractId = new Dictionary<string, FuturesContractProjectionKey>(StringComparer.Ordinal);
            foreach (var contract in contracts)
            {
                if (contract.OnTheRun && !contract.Rollover)
                    throw new ArgumentException(
                        $"Futures contract '{contract.ContractId}' cannot be on-the-run without belonging to the rollover set.",
                        nameof(contracts));
                var key = context.ToProjectionKey(contract);
                if (!keysByContractId.TryAdd(contract.ContractId, key))
                {
                    throw new ArgumentException(
                        $"The futures-contract write contains duplicate or ambiguous contractId '{contract.ContractId}'.",
                        nameof(contracts));
                }
            }
        }

        /// <summary>
        /// Performs the EnsureDistinctFuturesOptionContractWrites Securities persistence helper operation.
        /// </summary>
        internal void EnsureDistinctFuturesOptionContractWrites(
            IEnumerable<FuturesOptionContractReadModel> contracts)
        {
            var keysByContractId = new Dictionary<string, FuturesOptionContractProjectionKey>(StringComparer.Ordinal);
            foreach (var contract in contracts)
            {
                var key = context.ToProjectionKey(contract);
                if (!keysByContractId.TryAdd(contract.ContractId, key))
                {
                    throw new ArgumentException(
                        $"The futures-option write contains duplicate or ambiguous contractId '{contract.ContractId}'.",
                        nameof(contracts));
                }
            }
        }

        /// <summary>
        /// Performs the ReadProjectionStateAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<ProjectionState?> ReadProjectionStateAsync(
            IObjectRepository db,
            string projectionName,
            CancellationToken cancellationToken = default)
        {
            var states = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionStateV3)}", SecuritiesDbCql.GetSecuritiesProjectionStateV3)
                .SetParameters(new GetSecuritiesProjectionStateV3(projectionName))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToProjectionState!, cancellationToken);
            return states.Count == 1 ? states.First() : null;
        }

        /// <summary>
        /// Performs the ReadSymbolProjectionStateAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<ProjectionState?> ReadSymbolProjectionStateAsync(
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

        /// <summary>
        /// Performs the InvalidateGlobalProjectionStateAsync Securities persistence helper operation.
        /// </summary>
        internal async Task InvalidateGlobalProjectionStateAsync(
            IObjectRepository db,
            string projectionName,
            CancellationToken cancellationToken = default)
            => await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3)}", SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3)
                .SetParameters(new InvalidateSecuritiesProjectionStateV3(Guid.NewGuid(), projectionName))
                .ExecuteCommandAsync(cancellationToken);

        /// <summary>
        /// Performs the GetProjectionReadStampAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<ProjectionReadStamp?> GetProjectionReadStampAsync(
            IObjectRepository db,
            string projectionName,
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var global = await context.ReadProjectionStateAsync(db, projectionName, cancellationToken)
                .ConfigureAwait(false);
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

            var symbolState = await context.ReadSymbolProjectionStateAsync(db, projectionName, symbol, cancellationToken)
                .ConfigureAwait(false);
            return symbolState is { IsComplete: true, HasNoActiveOperations: true }
                ? new ProjectionReadStamp(
                    projectionName,
                    symbol,
                    symbolState.Value.Generation,
                    IsGlobal: false,
                    GlobalGeneration: global?.Generation)
                : null;
        }

        /// <summary>
        /// Performs the IsProjectionReadStampCurrentAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<bool> IsProjectionReadStampCurrentAsync(
            IObjectRepository db,
            ProjectionReadStamp stamp,
            CancellationToken cancellationToken = default)
        {
            var global = await context.ReadProjectionStateAsync(db, stamp.ProjectionName, cancellationToken)
                .ConfigureAwait(false);
            if (stamp.IsGlobal)
            {
                return global is { IsComplete: true, HasNoActiveOperations: true } &&
                    global.Value.Generation == stamp.Generation;
            }

            var symbol = await context.ReadSymbolProjectionStateAsync(
                db,
                stamp.ProjectionName,
                stamp.Symbol,
                cancellationToken)
                    .ConfigureAwait(false);
            return context.IsSymbolProjectionReadFenceCurrent(
                stamp.GlobalGeneration,
                global?.Generation,
                global?.IsComplete ?? false,
                global?.HasNoActiveOperations ?? true,
                stamp.Generation,
                symbol?.Generation,
                symbol?.IsComplete ?? false,
                symbol?.HasNoActiveOperations ?? false);
        }

        /// <summary>
        /// Performs the IsSymbolProjectionReadFenceCurrent Securities persistence helper operation.
        /// </summary>
        internal bool IsSymbolProjectionReadFenceCurrent(
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

        /// <summary>
        /// Performs the ReadProjectionOrFallbackAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<TResult> ReadProjectionOrFallbackAsync<TResult>(
            IObjectRepository db,
            string projectionName,
            string symbol,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<TResult>> readProjection,
            Func<CancellationToken, Task<TResult>> readFallback)
        {
            var stamp = await context.GetProjectionReadStampAsync(
                db,
                projectionName,
                symbol,
                cancellationToken)
                    .ConfigureAwait(false);
            if (stamp is null)
                return await readFallback(cancellationToken)
                    .ConfigureAwait(false);

            var projected = await readProjection(cancellationToken)
                .ConfigureAwait(false);
            return await context.IsProjectionReadStampCurrentAsync(
                db,
                stamp.Value,
                cancellationToken)
                    .ConfigureAwait(false)
                ? projected
                : await readFallback(cancellationToken)
                    .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the ReadProjectionOrFallbackAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<TResult> ReadProjectionOrFallbackAsync<TResult>(
            IObjectRepository db,
            string projectionName,
            string symbol,
            Func<Task<TResult>> readProjection,
            Func<Task<TResult>> readFallback)
        {
            var stamp = await context.GetProjectionReadStampAsync(db, projectionName, symbol)
                .ConfigureAwait(false);
            if (stamp is null)
                return await readFallback()
                    .ConfigureAwait(false);

            var projected = await readProjection()
                .ConfigureAwait(false);
            return await context.IsProjectionReadStampCurrentAsync(db, stamp.Value)
                .ConfigureAwait(false)
                ? projected
                : await readFallback()
                    .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the BeginProjectionOperationAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<ProjectionOperation> BeginProjectionOperationAsync(
            IObjectRepository db,
            string projectionName,
            IEnumerable<string> affectedSymbols,
            CancellationToken cancellationToken = default)
        {
            var symbols = affectedSymbols
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var globalWasComplete = (await context.ReadProjectionStateAsync(db, projectionName, cancellationToken)
                .ConfigureAwait(false))
                is { IsComplete: true, HasNoActiveOperations: true };
            var completedSymbols = new HashSet<string>(StringComparer.Ordinal);
            foreach (var symbolBatch in symbols.Chunk(SecuritiesDbContext.CompletionStateLookupBatchSize))
            {
                var states = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesSymbolProjectionStatesV3)}", SecuritiesDbCql.GetSecuritiesSymbolProjectionStatesV3)
                    .SetParameters(new GetSecuritiesSymbolProjectionStatesV3(projectionName, symbolBatch))
                    .ExecuteQueryAsync(SecuritiesDbContext.MapToSymbolProjectionState!, cancellationToken);
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
                                SecuritiesDbContext.GlobalProjectionOperationScope,
                                projectionName),
                            new InsertSecuritiesProjectionOperationScopeV3(
                                projectionName,
                                operationId,
                                SecuritiesDbContext.ProjectionOperationScopeCount,
                                (symbols.Length + 1).ToString(CultureInfo.InvariantCulture))
                        }.Concat(symbols.Select(symbol =>
                            new InsertSecuritiesProjectionOperationScopeV3(
                                projectionName,
                                operationId,
                                SecuritiesDbContext.SymbolProjectionOperationScope,
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
                    .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!, cancellationToken);
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
                if (false.CanRemoveProjectionMutationJournalAfterFailure(
                    activationResponseConfirmed: !activationResponseUnknown))
                {
                    if (journalActivated)
                        await context.EndProjectionOperationAsync(db, operation, CancellationToken.None)
                            .ConfigureAwait(false);
                    else
                        await context.DeleteProjectionOperationJournalAsync(db, operation, CancellationToken.None)
                            .ConfigureAwait(false);
                }
                // An activation/set-add request can apply after a timeout. Preserve its
                // original journal when the response is unknown; only explicit stale
                // recovery may classify it after writers have drained.
                throw;
            }
        }

        /// <summary>
        /// Performs the EndProjectionOperationAsync Securities persistence helper operation.
        /// </summary>
        internal async Task EndProjectionOperationAsync(
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
            await context.DeleteProjectionOperationJournalAsync(db, operation, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the DeleteProjectionOperationJournalAsync Securities persistence helper operation.
        /// </summary>
        internal async Task DeleteProjectionOperationJournalAsync(
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
                .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!);
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

        /// <summary>
        /// Performs the CompleteProjectionOperationAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<bool> CompleteProjectionOperationAsync(
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
                    await EndSymbolOperationAsync(symbol)
                        .ConfigureAwait(false);
                    continue;
                }

                var applied = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(new CompleteSecuritiesSymbolProjectionOperationV3(
                        activeOperations,
                        operation.ProjectionName,
                        symbol,
                        operation.OperationId,
                        activeOperations))
                    .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!);
                if (applied == true)
                    continue;

                allCompleted = false;
                await EndSymbolOperationAsync(symbol)
                    .ConfigureAwait(false);
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
                    .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!);
                if (applied == true)
                {
                    await context.DeleteProjectionOperationJournalAsync(
                        db,
                        operation,
                        CancellationToken.None)
                            .ConfigureAwait(false);
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
            await context.DeleteProjectionOperationJournalAsync(
                db,
                operation,
                CancellationToken.None)
                    .ConfigureAwait(false);
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

        /// <summary>
        /// Performs the ExecuteProjectionMutationAsync Securities persistence helper operation.
        /// </summary>
        internal async Task ExecuteProjectionMutationAsync(
            IObjectRepository db,
            string projectionName,
            IEnumerable<string> affectedSymbols,
            Func<Task> mutation)
        {
            var operation = await context.BeginProjectionOperationAsync(db, projectionName, affectedSymbols);
            // Invoking the mutation begins the submission boundary. It may have applied
            // even when invocation or its returned task fails (for example, a driver
            // timeout), so leave the journal and active state for stale-cutoff recovery.
            var mutationTask = mutation();
            await mutationTask.ConfigureAwait(false);
            await context.CompleteProjectionOperationAsync(
                db,
                operation,
                completeGlobal: false,
                completeAllSymbols: false)
                    .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the PopulateFuturesContractSymbolProjectionAsync Securities persistence helper operation.
        /// </summary>
        internal async Task PopulateFuturesContractSymbolProjectionAsync(
            ICollection<FuturesContractV3ReadModel> contracts,
            CancellationToken cancellationToken = default)
        {
            if (contracts.Count == 0)
                return;

            await context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                .SetParameters(contracts.Select(contract => context.ToInsertParameters(contract)))
                .ExecuteCommandAsync(cancellationToken);
        }

        /// <summary>
        /// Performs the PopulateFuturesOptionContractSymbolProjectionAsync Securities persistence helper operation.
        /// </summary>
        internal async Task PopulateFuturesOptionContractSymbolProjectionAsync(
            ICollection<FuturesOptionContractReadModel> contracts,
            CancellationToken cancellationToken = default)
        {
            if (contracts.Count == 0)
                return;

            await context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(contracts.Select(contract => context.ToInsertParameters(contract)))
                .ExecuteCommandAsync(cancellationToken);
        }

        /// <summary>
        /// Performs the LoadFuturesContractProjectionAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<FuturesContractV3ReadModel[]> LoadFuturesContractProjectionAsync(
            string symbol,
            CancellationToken cancellationToken = default)
            => [.. (await context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractsBySymbol)}", SecuritiesDbCql.GetFuturesContractsBySymbol)
                .SetParameters(new GetFuturesContractsBySymbol(symbol))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesContract!, cancellationToken))];

        /// <summary>
        /// Performs the LoadFuturesOptionContractProjectionAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<FuturesOptionContractReadModel[]> LoadFuturesOptionContractProjectionAsync(
            string symbol,
            CancellationToken cancellationToken = default)
            => [.. (await context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsBySymbol)}", SecuritiesDbCql.GetFuturesOptionContractsBySymbol)
                .SetParameters(new GetFuturesOptionContractsBySymbol(symbol))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesOptionContract!, cancellationToken))];

        /// <summary>
        /// Performs the LoadAndPopulateFuturesContractsBySymbolAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<FuturesContractV3ReadModel[]> LoadAndPopulateFuturesContractsBySymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var db = context.DbFactory.SecuritiesDb;
            var operation = await context.BeginProjectionOperationAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                [symbol],
                cancellationToken);
            var targetMutationSubmissionStarted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                targetMutationSubmissionStarted = true;
                await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3Partition)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3Partition)
                    .SetParameters(new DeleteFuturesContractBySymbolV3Partition(symbol))
                    .ExecuteCommandAsync(CancellationToken.None);

                var matchingContracts = new List<FuturesContractV3ReadModel>();
                await foreach (var contract in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", SecuritiesDbCql.GetFuturesContracts)
                    .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesContract!, CancellationToken.None))
                {
                    if (string.Equals(contract.Symbol, symbol, StringComparison.Ordinal))
                        matchingContracts.Add(contract);
                }
                var contracts = matchingContracts
                    .OrderByDescending(contract => contract.OnTheRun)
                    .ThenByDescending(contract => contract.LastTradeDate)
                    .ThenBy(contract => contract.ContractId, StringComparer.Ordinal)
                    .ToArray();
                await context.PopulateFuturesContractSymbolProjectionAsync(contracts, CancellationToken.None);

                var projectedContracts = await context.LoadFuturesContractProjectionAsync(symbol, CancellationToken.None);
                if (!context.HasExactFuturesContractKeys(contracts, projectedContracts))
                {
                    throw new StorageException(
                        $"SecuritiesDb could not reconcile the '{symbol}' futures-contract symbol projection; completion was not recorded.");
                }

                await context.CompleteProjectionOperationAsync(
                    db,
                    operation,
                    completeGlobal: false,
                    completeAllSymbols: true,
                    cancellationToken: CancellationToken.None);
                return contracts;
            }
            catch
            {
                if (targetMutationSubmissionStarted.CanRemoveProjectionMutationJournalAfterFailure())
                {
                    await context.EndProjectionOperationAsync(db, operation, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                throw;
            }
        }

        /// <summary>
        /// Performs the LoadAndPopulateFuturesOptionContractsBySymbolAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<FuturesOptionContractReadModel[]> LoadAndPopulateFuturesOptionContractsBySymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var db = context.DbFactory.SecuritiesDb;
            var operation = await context.BeginProjectionOperationAsync(
                db,
                SecuritiesDbContext.FuturesOptionContractSymbolProjection,
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
                    .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesOptionContract!, CancellationToken.None))
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
                await context.PopulateFuturesOptionContractSymbolProjectionAsync(contracts, CancellationToken.None);

                var projectedContracts = await context.LoadFuturesOptionContractProjectionAsync(symbol, CancellationToken.None);
                if (!context.HasExactFuturesOptionContractKeys(contracts, projectedContracts))
                {
                    throw new StorageException(
                        $"SecuritiesDb could not reconcile the '{symbol}' futures-option symbol projection; completion was not recorded.");
                }

                await context.CompleteProjectionOperationAsync(
                    db,
                    operation,
                    completeGlobal: false,
                    completeAllSymbols: true,
                    cancellationToken: CancellationToken.None);
                return contracts;
            }
            catch
            {
                if (targetMutationSubmissionStarted.CanRemoveProjectionMutationJournalAfterFailure())
                {
                    await context.EndProjectionOperationAsync(db, operation, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                throw;
            }
        }

        /// <summary>
        /// Performs the HasExactFuturesContractKeys Securities persistence helper operation.
        /// </summary>
        internal bool HasExactFuturesContractKeys(
            IReadOnlyCollection<FuturesContractV3ReadModel> source,
            IReadOnlyCollection<FuturesContractV3ReadModel> target)
            => source.Count == target.Count
                && source.Select(contract => context.ToProjectionKey(contract)).ToHashSet()
                    .SetEquals(target.Select(contract => context.ToProjectionKey(contract)));

        /// <summary>
        /// Performs the HasExactFuturesOptionContractKeys Securities persistence helper operation.
        /// </summary>
        internal bool HasExactFuturesOptionContractKeys(
            IReadOnlyCollection<FuturesOptionContractReadModel> source,
            IReadOnlyCollection<FuturesOptionContractReadModel> target)
            => source.Count == target.Count
                && source.Select(contract => context.ToProjectionKey(contract)).ToHashSet()
                    .SetEquals(target.Select(contract => context.ToProjectionKey(contract)));

        /// <summary>
        /// Performs the RecoverVerifiedInactiveProjectionOperationsAsync Securities persistence helper operation.
        /// </summary>
        internal async Task RecoverVerifiedInactiveProjectionOperationsAsync(
            IObjectRepository db,
            string projectionName,
            DateTime staleOperationCutoffUtc,
            CancellationToken cancellationToken)
        {
            var journalEntries = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)}", SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)
                .SetParameters(new GetSecuritiesProjectionOperationsV3(projectionName))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToProjectionOperationJournalEntry!);
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
                    .ExecuteQueryAsync(SecuritiesDbContext.MapToProjectionOperationScope!);
                var hasGlobalScope = false;
                var stateScopeCount = 0;
                int? expectedStateScopeCount = null;
                foreach (var scope in scopes)
                {
                    if (scope.ScopeType == SecuritiesDbContext.ProjectionOperationScopeCount)
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

                    if (scope.ScopeType == SecuritiesDbContext.GlobalProjectionOperationScope &&
                        scope.ScopeKey == projectionName)
                    {
                        hasGlobalScope = true;
                        stateScopeCount++;
                        globalOperationIds.Add(entry.OperationId);
                        continue;
                    }

                    if (scope.ScopeType != SecuritiesDbContext.SymbolProjectionOperationScope ||
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
        /// Performs the ReadProjectionInventoryAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<ProjectionInventory> ReadProjectionInventoryAsync(
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
                .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesContractProjectionKey!, cancellationToken))
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
                .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesContractProjectionKey!, cancellationToken))
            {
                futuresContractProjectionRows++;
                futuresContractProjectionKeys.Add(key);
            }
            await foreach (var key in db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractProjectionSourceKeys)}", SecuritiesDbCql.GetFuturesOptionContractProjectionSourceKeys)
                .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesOptionContractProjectionKey!, cancellationToken))
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
                .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesOptionContractProjectionKey!, cancellationToken))
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

        /// <summary>
        /// Performs the CountMissing Securities persistence helper operation.
        /// </summary>
        internal int CountMissing<TKey>(HashSet<TKey> expected, HashSet<TKey> actual)
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
        /// Performs the ReplaceFuturesRolloverSetCoreAsync Securities persistence helper operation.
        /// </summary>
        internal async Task ReplaceFuturesRolloverSetCoreAsync(
            FuturesContractRolloverReadModel rollover,
            IReadOnlyCollection<FuturesContractV3ReadModel> contracts,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rollover);
            ArgumentNullException.ThrowIfNull(contracts);
            cancellationToken.ThrowIfCancellationRequested();
            var replacements = contracts.ToArray();
            if (replacements.Length == 0)
                throw new ArgumentException("At least one rollover-set contract is required.", nameof(contracts));
            if (string.IsNullOrWhiteSpace(rollover.ContractId)
                || rollover.NextRolloverDate is null
                || rollover.UpdatedOn is null
                || string.IsNullOrWhiteSpace(rollover.UpdatedBy))
            {
                throw new ArgumentException("The replacement rollover row must be fully resolved.", nameof(rollover));
            }
            var symbol = rollover.Symbol.Trim().ToUpperInvariant();
            var orderedReplacements = replacements
                .OrderBy(static contract => contract.LastTradeDate)
                .ThenBy(static contract => contract.ContractId, StringComparer.Ordinal)
                .ToArray();
            if (replacements.Any(contract =>
                    !string.Equals(symbol, contract.Symbol, StringComparison.Ordinal)
                    || !contract.Rollover)
                || replacements.Count(static contract => contract.OnTheRun) != 1
                || !orderedReplacements[0].OnTheRun
                || (symbol == "ES" && replacements.Length != 1)
                || (symbol == "VX" && replacements.Length != 2)
                || replacements.Select(static contract => contract.ContractId)
                    .Distinct(StringComparer.Ordinal).Count() != replacements.Length
                || !replacements.Any(contract =>
                    string.Equals(rollover.ContractId, contract.ContractId, StringComparison.Ordinal)
                    && contract.OnTheRun
                    && rollover.NextRolloverDate == contract.LastTradeDate))
            {
                throw new ArgumentException(
                    "The rollover row must identify the one on-the-run contract in a distinct rollover set for the same symbol.",
                    nameof(contracts));
            }

            var existing = await context.GetFuturesRolloverSetAsync(symbol, cancellationToken);
            var replacementKeys = replacements
                .Select(static contract => (contract.ContractId, contract.LastTradeDate))
                .ToHashSet();
            var db = context.DbFactory.SecuritiesDb;
            List<object> queuedCommands = [];
            foreach (var current in existing)
            {
                if (replacementKeys.Contains((current.ContractId, current.LastTradeDate)))
                    continue;
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                    .SetParameters(new DeleteFuturesContractBySymbolV3(
                        current.Symbol, current.Rollover, current.OnTheRun,
                        current.LastTradeDate, current.ContractId))
                    .QueueCommand());
                var retired = current with { OnTheRun = false, Rollover = false };
                var retiredInsert = context.ToInsertParameters(retired);
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                    .SetParameters(retiredInsert)
                    .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                    .SetParameters(retiredInsert)
                    .QueueCommand());
            }

            foreach (var contract in orderedReplacements)
            {
                var insert = context.ToInsertParameters(contract);
                queuedCommands.AddRange(context.OtherProjectionKeys(contract).Select(key =>
                    db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                        .SetParameters(new DeleteFuturesContractBySymbolV3(
                            key.Symbol, key.Rollover, key.OnTheRun,
                            key.LastTradeDate, key.ContractId))
                        .QueueCommand()));
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", SecuritiesDbCql.InsertFuturesContract)
                    .SetParameters(insert)
                    .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                    .SetParameters(insert)
                    .QueueCommand());
            }
            queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.UpdateFuturesContractRollover)}", SecuritiesDbCql.UpdateFuturesContractRollover)
                .SetParameters(new UpdateFuturesContractRollover(
                    rollover.ContractId,
                    rollover.NextRolloverDate.Value,
                    rollover.UpdatedOn.Value,
                    rollover.UpdatedBy,
                    symbol))
                .QueueCommand());

            await context.ExecuteProjectionMutationAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                [symbol],
                () => db.ExecuteQueuedCommandsAsync(queuedCommands, true));
            cancellationToken.ThrowIfCancellationRequested();

            var persistedSet = (await context.GetFuturesRolloverSetAsync(symbol, cancellationToken))
                .OrderBy(static contract => contract.LastTradeDate)
                .ThenBy(static contract => contract.ContractId, StringComparer.Ordinal)
                .ToArray();
            var persistedPointer = await context.GetFuturesContractRolloverAsync(symbol, cancellationToken);
            if (persistedSet.Length != orderedReplacements.Length
                || !persistedSet.Select(static contract => (
                        contract.ContractId, contract.OnTheRun, contract.Rollover))
                    .SequenceEqual(orderedReplacements.Select(static contract => (
                        contract.ContractId, contract.OnTheRun, contract.Rollover)))
                || persistedPointer?.ContractId != rollover.ContractId
                || persistedPointer.NextRolloverDate != rollover.NextRolloverDate)
            {
                throw new StorageException(
                    $"The '{symbol}' futures rollover set did not pass durable post-write verification.");
            }
        }

        /// <summary>
        /// Performs the StageReferenceAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<PendingReferenceVersion?> StageReferenceAsync(FuturesOptionContractReadModel value, string? originalId = null)
        {
            var previous = await context.GetFuturesOptionContractAsync(originalId ?? value.ContractId);
            if (value.SchemaVersion == 0)
            {
                if (previous?.SchemaVersion > 0) throw new InvalidOperationException("A provider-bound contract cannot lose its reference metadata.");
                return null;
            }
            if (originalId is not null && originalId != value.ContractId
                || previous is not null && (previous.Symbol != value.Symbol || previous.ContractMonth != value.ContractMonth
                    || previous.OptionType != value.OptionType || previous.GetExactStrikePrice() != value.GetExactStrikePrice()))
                throw new InvalidOperationException("Contract identity changes require Add, not replacement.");
            if (previous?.ReviewState == ReferenceReviewState.Reviewed && value.ReviewState != ReferenceReviewState.Reviewed)
                throw new InvalidOperationException("A reviewed reference cannot be replaced by an unqualified draft.");
            if (string.IsNullOrWhiteSpace(value.UnderlyingContractId))
                throw new InvalidOperationException("An exact existing underlying futures contract is required.");
            var underlying = await context.GetFuturesContractAsync(value.UnderlyingContractId);
            if (underlying is null || underlying.SecurityType != "FUT" || underlying.SchemaVersion != 1
                || underlying.Dataset != value.Dataset || underlying.Symbol != value.Symbol
                || value.UnderlyingInstrumentId is { } providerId && providerId != underlying.InstrumentId
                || value.UnderlyingPublisherId is { } publisherId && publisherId != underlying.PublisherId
                || value.ExpirationUtc is not null && (underlying.ExpirationUtc is null || underlying.ExpirationUtc < value.ExpirationUtc)
                || value.ReviewState == ReferenceReviewState.Reviewed && FuturesReferenceQualification.Errors(underlying).Count != 0)
                throw new InvalidOperationException("The exact underlying future is missing or incompatible with the option.");
            if (value.ReviewState == ReferenceReviewState.Reviewed
                && await context.GetReferenceVersionAsync(underlying.ContractId, underlying.MappingVersion!) is null)
                throw new InvalidOperationException("The underlying future's reviewed version has not completed publication.");
            return await context.StageReferenceVersionAsync(value);
        }

        /// <summary>
        /// Performs the StageReferenceAsync Securities persistence helper operation.
        /// </summary>
        internal async Task<PendingReferenceVersion?> StageReferenceAsync(FuturesContractV3ReadModel value, string? originalId = null)
        {
            var previous = await context.GetFuturesContractAsync(originalId ?? value.ContractId);
            if (value.SchemaVersion == 0)
            {
                if (previous?.SchemaVersion > 0) throw new InvalidOperationException("A provider-bound contract cannot lose its reference metadata.");
                return null;
            }
            if (originalId is not null && originalId != value.ContractId
                || previous is not null && (previous.Symbol != value.Symbol || previous.LastTradeDate != value.LastTradeDate))
                throw new InvalidOperationException("Contract identity changes require Add, not replacement.");
            if (previous?.ReviewState == ReferenceReviewState.Reviewed && value.ReviewState != ReferenceReviewState.Reviewed)
                throw new InvalidOperationException("A reviewed reference cannot be replaced by an unqualified draft.");
            return await context.StageReferenceVersionAsync(value);
        }

        /// <summary>
        /// Performs the CommitReferenceAsync Securities persistence helper operation.
        /// </summary>
        internal Task CommitReferenceAsync(PendingReferenceVersion? value) =>
            value is null ? Task.CompletedTask : context.CommitReferenceVersionAsync(value);

        /// <summary>
        /// Performs the EncodeOptionCursor Securities persistence helper operation.
        /// </summary>
        internal string EncodeOptionCursor(OptionPageCursor cursor)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(cursor);
            var signature = HMACSHA256.HashData(OptionPageSigningKey, payload);
            return Convert.ToBase64String(payload) + "." + Convert.ToBase64String(signature);
        }

        /// <summary>
        /// Performs the DecodeOptionCursor Securities persistence helper operation.
        /// </summary>
        internal OptionPageCursor DecodeOptionCursor(GetFuturesOptionContractsPageParameter request)
        {
            try
            {
                var parts = request.ContinuationToken!.Split('.');
                if (parts.Length != 2) throw new FormatException();
                var payload = Convert.FromBase64String(parts[0]);
                var signature = Convert.FromBase64String(parts[1]);
                if (!CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(OptionPageSigningKey, payload)))
                    throw new FormatException();
                var cursor = JsonSerializer.Deserialize<OptionPageCursor>(payload);
                if (cursor is null || cursor.Symbol != request.Symbol || cursor.PageSize != request.PageSize || cursor.State.Length == 0)
                    throw new FormatException();
                return cursor;
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                throw new ArgumentException("Invalid or expired contract continuation token. Refresh the list to restart paging.", nameof(request), ex);
            }
        }

        /// <summary>Validates projection backfill control parameters.</summary>
        internal void ValidateBackfillParameters(int batchSize, DateTime? staleOperationCutoffUtc)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
            if (staleOperationCutoffUtc is not { } cutoff) return;
            if (cutoff.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The stale-operation cutoff must have DateTimeKind.Utc.", nameof(staleOperationCutoffUtc));
            if (cutoff > DateTime.UtcNow)
                throw new ArgumentOutOfRangeException(nameof(staleOperationCutoffUtc), cutoff, "The stale-operation cutoff cannot be in the future.");
        }

        /// <summary>Validates and normalizes futures symbols used to seed rollover rows.</summary>
        internal string[] ValidateAndNormalizeRolloverSymbols(IReadOnlyCollection<string> symbols, string createdBy)
        {
            ArgumentNullException.ThrowIfNull(symbols);
            ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
            var normalized = symbols.Select(static symbol => symbol?.Trim().ToUpperInvariant())
                .Where(static symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct(StringComparer.Ordinal)
                .Select(static symbol => symbol!)
                .ToArray();
            if (normalized.Length == 0)
                throw new ArgumentException("At least one futures symbol is required.", nameof(symbols));
            return normalized;
        }

        /// <summary>Validates and normalizes a futures root symbol.</summary>
        internal string ValidateAndNormalizeSymbol(string symbol)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            return symbol.Trim().ToUpperInvariant();
        }

        /// <summary>Validates a futures rollover assignment.</summary>
        internal void ValidateRollover(FuturesContractRolloverReadModel rollover)
            => ArgumentNullException.ThrowIfNull(rollover);

        /// <summary>Validates an option-contract paging request.</summary>
        internal void ValidatePagingRequest(GetFuturesOptionContractsPageParameter request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();
        }

        /// <summary>Validates an option expiry range and returns its normalized symbol.</summary>
        internal string ValidateAndNormalizeExpiryRange(string symbol, DateOnly fromExpiry, DateOnly throughExpiry)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            if (throughExpiry < fromExpiry) throw new ArgumentOutOfRangeException(nameof(throughExpiry));
            return symbol.Trim().ToUpperInvariant();
        }

        /// <summary>Validates a cached option-definition query and returns its normalized symbol.</summary>
        internal string ValidateAndNormalizeCachedDefinitionQuery(string symbol, string underlyingContractId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            ArgumentException.ThrowIfNullOrWhiteSpace(underlyingContractId);
            return symbol.Trim().ToUpperInvariant();
        }

        /// <summary>Validates cached option definitions and returns their normalized symbol.</summary>
        internal string ValidateAndNormalizeOptionDefinitions(string symbol, DateOnly coverageFrom, DateOnly coverageThrough, IReadOnlyCollection<CachedOptionContractDefinitionReadModel> definitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            ArgumentNullException.ThrowIfNull(definitions);
            if (coverageThrough < coverageFrom) throw new ArgumentOutOfRangeException(nameof(coverageThrough));
            var normalized = symbol.Trim().ToUpperInvariant();
            if (definitions.Any(row => !string.Equals(row.Symbol, normalized, StringComparison.OrdinalIgnoreCase)
                                    || row.ExpiryDate < coverageFrom || row.ExpiryDate > coverageThrough
                                    || string.IsNullOrWhiteSpace(row.UnderlyingContractId)
                                    || string.IsNullOrWhiteSpace(row.ProviderRoot)
                                    || string.IsNullOrWhiteSpace(row.Definition.ContractId)))
                throw new ArgumentException("Cached option definitions must be complete and within the published coverage.", nameof(definitions));
            return normalized;
        }

        /// <summary>Stages an immutable futures reference version and reserves its identities.</summary>
        internal Task<PendingReferenceVersion> StageReferenceVersionCoreAsync(
            FuturesContractV3ReadModel value,
            CancellationToken cancellationToken)
        {
            _ = ReferencePayloadCodec.Write(value);
            var identity = JsonSerializer.Serialize(new
            {
                value.ContractId,
                value.Dataset,
                value.PublisherId,
                value.InstrumentId,
                value.Symbol,
                value.LastTradeDate,
                value.SecurityType
            });
            return context.StageReferenceVersionCoreAsync(
                value.ContractId,
                value.Dataset,
                value.PublisherId,
                value.InstrumentId,
                value.MappingVersion,
                value.ReviewState,
                identity,
                new(value with { OnTheRun = false, Rollover = false }, null, null),
                cancellationToken);
        }

        /// <summary>Stages an immutable futures-option reference version and reserves its identities.</summary>
        internal Task<PendingReferenceVersion> StageReferenceVersionCoreAsync(
            FuturesOptionContractReadModel value,
            CancellationToken cancellationToken)
        {
            _ = ReferencePayloadCodec.Write(value);
            var identity = JsonSerializer.Serialize(new
            {
                value.ContractId,
                value.Dataset,
                value.PublisherId,
                value.InstrumentId,
                value.Symbol,
                value.ContractMonth,
                value.OptionType,
                Strike = value.GetExactStrikePrice(),
                value.UnderlyingContractId,
                value.UnderlyingInstrumentId,
                value.UnderlyingPublisherId
            });
            var convention = value.ReviewState == ReferenceReviewState.Reviewed
                ? ReviewedOptionConvention.From(value)
                : null;
            return context.StageReferenceVersionCoreAsync(
                value.ContractId,
                value.Dataset,
                value.PublisherId,
                value.InstrumentId,
                value.MappingVersion,
                value.ReviewState,
                identity,
                new(null, value, convention),
                cancellationToken);
        }

        /// <summary>Stages the serialized immutable reference-version envelope.</summary>
        internal async Task<PendingReferenceVersion> StageReferenceVersionCoreAsync(
            string contractId,
            string? dataset,
            ushort? publisherId,
            uint? instrumentId,
            string? version,
            ReferenceReviewState reviewState,
            string identity,
            ReferenceContractVersion value,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128
                || string.IsNullOrWhiteSpace(dataset) || dataset.Length > 32
                || publisherId is null or 0 || instrumentId is null or 0)
                throw new ArgumentException("An exact bounded IFM/provider identity is required.");
            if (MessagePackBinarySerializer.MeasureContent(value) > 131072)
                throw new ArgumentException("Reference version is oversized.");
            var digest = value.CalculateReferenceVersionDigest();
            version = reviewState == ReferenceReviewState.Reviewed ? version : "draft/" + digest;
            if (string.IsNullOrWhiteSpace(version) || version.Length > 128)
                throw new ArgumentException("A bounded mapping version is required.");

            await context.ClaimReferenceIdentityAsync("ifm:" + contractId, identity, cancellationToken)
                .ConfigureAwait(false);
            await context.ClaimReferenceIdentityAsync(
                    FormattableString.Invariant($"provider:{dataset}:{publisherId}:{instrumentId}"),
                    identity,
                    cancellationToken)
                .ConfigureAwait(false);
            var payload = MessagePackBinarySerializer.Shared.Serialize(value)!;
            await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.StageReferenceVersion)}", SecuritiesDbCql.StageReferenceVersion)
                .SetParameters(new StageReferenceVersion(contractId, version, digest, payload))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            var stored = await context.ReadReferenceVersionAsync(contractId, version, cancellationToken)
                .ConfigureAwait(false);
            if (stored is null || stored.Value.Digest != digest)
                throw new InvalidOperationException("Mapping version already contains different content. Publish a new reviewed version.");
            return new(contractId, version, digest);
        }

        /// <summary>Claims an immutable IFM or provider identity binding.</summary>
        internal async Task ClaimReferenceIdentityAsync(
            string identityKey,
            string binding,
            CancellationToken cancellationToken)
        {
            await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.ClaimReferenceIdentity)}", SecuritiesDbCql.ClaimReferenceIdentity)
                .SetParameters(new ClaimReferenceIdentity(identityKey, binding))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            var bindings = await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetReferenceIdentityClaim)}", SecuritiesDbCql.GetReferenceIdentityClaim)
                .SetParameters(new GetReferenceIdentityClaim(identityKey))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToReferenceIdentityBinding!, cancellationToken)
                .ConfigureAwait(false);
            if (bindings.SingleOrDefault() != binding)
                throw new InvalidOperationException("Provider/IFM identity collision. Existing contract identities cannot be overwritten.");
        }

        /// <summary>Publishes a previously staged immutable reference version.</summary>
        internal async Task CommitReferenceVersionCoreAsync(
            PendingReferenceVersion value,
            CancellationToken cancellationToken)
        {
            await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.CommitReferenceVersion)}", SecuritiesDbCql.CommitReferenceVersion)
                .SetParameters(new CommitReferenceVersion(value.ContractId, value.Version, value.Digest))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            var stored = await context.ReadReferenceVersionAsync(value.ContractId, value.Version, cancellationToken)
                .ConfigureAwait(false);
            if (stored is null || !stored.Value.Published || stored.Value.Digest != value.Digest)
                throw new InvalidOperationException("Reference publication did not complete.");
        }

        /// <summary>Gets a published immutable reference version.</summary>
        internal async Task<ReferenceContractVersion?> GetReferenceVersionCoreAsync(
            string contractId,
            string version,
            CancellationToken cancellationToken)
        {
            var row = await context.ReadReferenceVersionAsync(contractId, version, cancellationToken)
                .ConfigureAwait(false);
            if (row is null || !row.Value.Published) return null;
            var value = row.Value.Value;
            if ((value.Option?.ContractId ?? value.Future?.ContractId) != contractId
                || value.Convention is not null && value.Convention != ReviewedOptionConvention.From(value.Option!))
                throw new InvalidDataException("Reference version and pricing convention disagree.");
            return value;
        }

        /// <summary>Gets whether a staged or published immutable reference version exists.</summary>
        internal async Task<bool> ContainsReferenceVersionCoreAsync(
            string contractId,
            string version,
            CancellationToken cancellationToken)
            => await context.ReadReferenceVersionAsync(contractId, version, cancellationToken)
                .ConfigureAwait(false) is not null;

        /// <summary>Gets a published reference version effective at a UTC instant.</summary>
        internal async Task<ReferenceContractVersion?> GetEffectiveReferenceVersionCoreAsync(
            string contractId,
            string version,
            DateTimeOffset effectiveAtUtc,
            CancellationToken cancellationToken)
        {
            if (effectiveAtUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("Historical valuation time must be UTC.", nameof(effectiveAtUtc));
            var value = await context.GetReferenceVersionAsync(contractId, version, cancellationToken)
                .ConfigureAwait(false);
            var from = value?.Option?.EffectiveFromUtc ?? value?.Future?.EffectiveFromUtc;
            var until = value?.Option?.EffectiveUntilUtc ?? value?.Future?.EffectiveUntilUtc;
            return from <= effectiveAtUtc && until > effectiveAtUtc ? value : null;
        }

        /// <summary>Lists a bounded page of staged and published reference versions.</summary>
        internal async Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsCoreAsync(
            string contractId,
            string afterVersion,
            int limit,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128
                || afterVersion.Length > 128 || limit is < 1 or > 200)
                throw new ArgumentException("Invalid bounded version-history query.");
            return (await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetReferenceVersionHistory)}", SecuritiesDbCql.GetReferenceVersionHistory)
                .SetParameters(new GetReferenceVersionHistory(contractId, afterVersion, limit))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToReferenceVersionSummary!, cancellationToken)
                .ConfigureAwait(false)).ToArray();
        }

        /// <summary>Reads and verifies one staged or published reference-version row.</summary>
        internal async Task<ReferenceVersionStorageRow?> ReadReferenceVersionAsync(
            string contractId,
            string version,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128
                || string.IsNullOrWhiteSpace(version) || version.Length > 128)
                throw new ArgumentException("Exact reference version is required.");
            var rows = (await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetReferenceVersion)}", SecuritiesDbCql.GetReferenceVersion)
                .SetParameters(new GetReferenceVersion(contractId, version))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToReferenceVersionStorageRow!, cancellationToken)
                .ConfigureAwait(false)).ToArray();
            if (rows.Length == 0) return null;
            var row = rows.Single();
            if (row.Value.CalculateReferenceVersionDigest() != row.Digest)
                throw new InvalidDataException("Reference version checksum mismatch.");
            return row;
        }

    }

    extension(bool targetMutationSubmissionStarted)
    {
        /// <summary>Determines whether a failed projection mutation journal can be removed without hiding an ambiguous write.</summary>
        /// <param name="ownershipReleaseOrAbsenceConfirmed">Whether ownership is confirmed released or absent.</param>
        /// <param name="activationResponseConfirmed">Whether any activation response is known rather than ambiguous.</param>
        /// <returns><see langword="true"/> when cleanup cannot conceal a submitted or ambiguously applied mutation.</returns>
        internal bool CanRemoveProjectionMutationJournalAfterFailure(
            bool ownershipReleaseOrAbsenceConfirmed = true,
            bool activationResponseConfirmed = true)
            => !targetMutationSubmissionStarted &&
                ownershipReleaseOrAbsenceConfirmed &&
                activationResponseConfirmed;
    }

    extension(ReferenceContractVersion value)
    {
        /// <summary>Calculates the stable content digest for an immutable reference version.</summary>
        internal string CalculateReferenceVersionDigest()
            => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    }
}
