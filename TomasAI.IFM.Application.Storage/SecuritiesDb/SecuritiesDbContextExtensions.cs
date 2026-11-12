using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TomasAI.IFM.Shared.Util;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal static class SecuritiesDbContextExtensions
{
    static readonly byte[] OptionPageSigningKey = RandomNumberGenerator.GetBytes(32);

    extension(SecuritiesDbContext context)
    {
        /// <summary>Persists only the complete catalog rows selected by the caller, retaining immutable reference versions.</summary>
        internal async Task WriteFuturesCatalogRowsCoreAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, IReadOnlyList<FuturesContractV3ReadModel> contracts, CancellationToken token)
        {
            foreach (var contract in contracts)
            {
                var parameters = (object?[])context.ToInsertParameters(contract).Bind();
                await context.RemoveFuturesContractAsync(transaction, contract.ContractId, token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContract, parameters, token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContractBySymbolV3, parameters, token)
            .ConfigureAwait(false);
            }
        }

        /// <summary>Writes exact supplied pointer values without changing stream ownership or choosing a front contract.</summary>
        internal async Task WriteFuturesRolloverPointerCoreAsync(EventSourceDb.IEnlistedPostgresTransaction transaction, FuturesContractRolloverReadModel rollover, CancellationToken token)
        {
            await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContractRolloverIfMissing,
                (object?[])new CatalogRolloverSeed(rollover.Symbol, rollover.CreatedOn, rollover.CreatedBy).Bind(), token)
            .ConfigureAwait(false);
            if (rollover.ContractId is null || rollover.NextRolloverDate is not { } date
                || rollover.UpdatedOn is not { } updated || rollover.UpdatedBy is not { } updatedBy)
                return;
            var pointer = new UpdateFuturesContractRollover(rollover.ContractId, date, updated, updatedBy, rollover.Symbol);
            if (await transaction.ExecuteAsync(SecuritiesDbSql.UpdateFuturesContractRollover, (object?[])pointer.Bind(), token)
            .ConfigureAwait(false) != 1)
                throw new InvalidOperationException($"The {rollover.Symbol} rollover pointer does not exist.");
        }

        /// <summary>Executes the caller-selected PostgreSQL projection query.</summary>
        internal Task<TResult> ReadProjectionAsync<TResult>(Func<CancellationToken, Task<TResult>> read, CancellationToken token) => read(token);
        /// <summary>Executes the caller-selected PostgreSQL projection query.</summary>
        internal Task<TResult> ReadProjectionAsync<TResult>(Func<Task<TResult>> read) => read();

        /// <summary>Copies both PostgreSQL symbol projections from canonical rows under one table lock.</summary>
        internal Task<SecuritiesProjectionBackfillResult> ReplaceSymbolProjectionRowsFromCanonicalCoreAsync(CancellationToken cancellationToken)
            => context.NativeTransaction().ExecuteOnceAsync(async (transaction, token) =>
            {
                await transaction.ExecuteAsync(SecuritiesDbSql.LockSymbolProjectionRebuild, [], token)
            .ConfigureAwait(false);
                if (await transaction.ScalarAsync(SecuritiesDbSql.HasAmbiguousCanonicalContractIds, [], token)
            .ConfigureAwait(false) is true)
                    throw new InvalidDataException("Canonical securities contain ambiguous contract IDs; neither symbol projection was changed.");
                await transaction.ExecuteAsync(SecuritiesDbSql.ClearSymbolProjections, [], token)
            .ConfigureAwait(false);
                var futures = await transaction.ExecuteAsync(SecuritiesDbSql.RebuildFuturesSymbolProjection, [], token)
            .ConfigureAwait(false);
                var options = await transaction.ExecuteAsync(SecuritiesDbSql.RebuildOptionSymbolProjection, [], token)
            .ConfigureAwait(false);
                return new SecuritiesProjectionBackfillResult(futures, options);
            }, cancellationToken);

        /// <summary>Reads the published expiry generation.</summary>
        internal Task<OptionExpiryCalendarState?> ReadExpiryStateAsync(string symbol, CancellationToken token)
            => context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetOptionContractExpiryCalendarState)}", SecuritiesDbSql.GetOptionContractExpiryCalendarState)
            .SetParameters(new GetOptionContractExpiryCalendarState(symbol))
            .ExecuteSingleAsync(SecuritiesDbContext.MapToOptionExpiryCalendarState!, token);

        /// <summary>Reads expiries and the nearest following maturity from the published generation.</summary>
        internal async Task<IReadOnlyList<OptionContractExpiryReadModel>> ReadExpiriesAsync(
            string symbol, DateOnly fromExpiry, DateOnly throughExpiry, OptionExpiryCalendarState? state, CancellationToken cancellationToken)
        {
            if (state is null || fromExpiry > state.CoverageThrough || throughExpiry < state.CoverageFrom)
                return [];
            var effectiveFromExpiry = fromExpiry < state.CoverageFrom ? state.CoverageFrom : fromExpiry;
            var effectiveThroughExpiry = throughExpiry > state.CoverageThrough
                ? state.CoverageThrough
                : throughExpiry;
            var parameters = new GetOptionContractExpiries(symbol, state.Generation, effectiveFromExpiry, state.CoverageThrough);
            var rows = (await (context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetOptionContractExpiries)}", SecuritiesDbSql.GetOptionContractExpiries)
            .SetParameters(parameters)
            .ExecuteQueryAsync(SecuritiesDbContext.MapToOptionContractExpiry!, cancellationToken))
                    .ConfigureAwait(false)).ToArray();
            var nearestAfter = rows.Where(row => row.ExpiryDate > effectiveThroughExpiry)
                .Select(row => row.ExpiryDate).OrderBy(date => date).FirstOrDefault();
            return rows.Where(row => !string.IsNullOrWhiteSpace(row.ContractId)
                                     && (row.ExpiryDate <= effectiveThroughExpiry
                                     || nearestAfter != default && row.ExpiryDate == nearestAfter))
                .DistinctBy(row => (row.ContractId, row.ExpiryDate, row.ProviderRoot)).ToArray();
        }

        /// <summary>Streams the requested immutable definition partitions.</summary>
        internal async IAsyncEnumerable<CachedOptionContractDefinitionReadModel> ReadDefinitionPartitionsAsync(
            string symbol, Guid generation, DateOnly[] expiries,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            if (expiries.Length == 0) yield break;
            await foreach (var row in context.DbFactory.SecuritiesDb
                .Use(nameof(SecuritiesDbSql.GetCachedOptionDefinitionPartitions), SecuritiesDbSql.GetCachedOptionDefinitionPartitions)
            .SetParameters(new GetCachedOptionDefinitionPartitions(symbol, generation, expiries))
            .ExecuteStreamAsync(SecuritiesDbContext.MapToCachedOptionContractDefinition!, token)
            .ConfigureAwait(false))
            {
                if (row is not null) yield return row;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ProjectFuturesContractAsync operation.</summary>
        internal async Task ProjectFuturesContractAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, PreparedFuturesProjection projection, string? originalContractId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var contract = projection.Contract;
            var parameters = context.ToInsertParameters(contract);
            if (projection.Reference is { } qualified)
            {
                var reference = await context.StageReferenceVersionCoreAsync(qualified, token, transaction)
            .ConfigureAwait(false);
                await context.CommitReferenceVersionCoreAsync(reference, token, transaction)
            .ConfigureAwait(false);
            }

            await context.RemoveFuturesContractAsync(transaction, originalContractId ?? contract.ContractId, token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContract, (object?[])parameters.Bind(), token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContractBySymbolV3, (object?[])parameters.Bind(), token)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ProjectOptionContractAsync operation.</summary>
        internal async Task ProjectOptionContractAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, PreparedOptionProjection projection, string? originalContractId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var contract = projection.Contract;
            var parameters = context.ToInsertParameters(contract);
            if (projection.Reference is { } qualified)
            {
                var reference = await context.StageReferenceVersionCoreAsync(qualified, token, transaction)
            .ConfigureAwait(false);
                await context.CommitReferenceVersionCoreAsync(reference, token, transaction)
            .ConfigureAwait(false);
            }

            await context.RemoveOptionContractAsync(transaction, originalContractId ?? contract.ContractId, token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesOptionContract, (object?[])parameters.Bind(), token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2, (object?[])parameters.Bind(), token)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the RemoveFuturesContractAsync operation.</summary>
        internal async Task RemoveFuturesContractAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, string contractId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            await transaction.ExecuteAsync(SecuritiesDbSql.DbContextExtensions_RemoveFuturesContractAsyncCoreBinding_1, [contractId], token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.DbContextExtensions_RemoveFuturesContractAsyncCoreBinding_2, [contractId], token)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the RemoveOptionContractAsync operation.</summary>
        internal async Task RemoveOptionContractAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, string contractId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            await transaction.ExecuteAsync(SecuritiesDbSql.DbContextExtensions_RemoveOptionContractAsyncCoreBinding_1, [contractId], token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.DbContextExtensions_RemoveOptionContractAsyncCoreBinding_2, [contractId], token)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the CommitReferenceVersionAsync operation.</summary>
        internal Task CommitReferenceVersionAsyncCoreBinding(PendingReferenceVersion value, CancellationToken cancellationToken = default)
        {
            return context.CommitReferenceVersionCoreAsync(value, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ListReferenceVersionsAsync operation.</summary>
        internal Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsAsyncCoreBinding(string contractId, string afterVersion = "", int limit = 100, CancellationToken cancellationToken = default)
        {
            return context.ListReferenceVersionsCoreAsync(contractId, afterVersion, limit, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertFuturesContractAsync operation.</summary>
        internal async Task InsertFuturesContractAsyncCoreBinding(FuturesContractV3ReadModel futuresContract)
        {
            var reference = await (context.StageReferenceAsync(futuresContract))
                .ConfigureAwait(false);
            var db = context.DbFactory.SecuritiesDb;
            var parameters = context.ToInsertParameters(futuresContract);
            List<object> queuedCommands = context.OtherProjectionKeys(futuresContract).Select(key => db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbSql.DeleteFuturesContractBySymbolV3)
            .SetParameters(new DeleteFuturesContractBySymbolV3(key.Symbol, key.Rollover, key.OnTheRun, key.LastTradeDate, key.ContractId))
            .QueueCommand()).ToList();
            queuedCommands.AddRange([db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesContract)}", SecuritiesDbSql.InsertFuturesContract)
            .SetParameters(parameters)
            .QueueCommand(),
                db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesContractBySymbolV3)}", SecuritiesDbSql.InsertFuturesContractBySymbolV3)
            .SetParameters(parameters)
            .QueueCommand()]);
            await (context.ExecuteProjectionMutationAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, [futuresContract.Symbol], () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
            await (context.CommitReferenceAsync(reference))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertFuturesContractsAsync operation.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "MA0051", Justification = "Native and transitional provider paths implement one compatibility operation; retain its transaction and failure ordering during migration.")]
        internal async Task InsertFuturesContractsAsyncCoreBinding(ICollection<FuturesContractV3ReadModel> futuresContracts)
        {
            if (futuresContracts.Count == 0)
                return;
            var db = context.DbFactory.SecuritiesDb;
            var references = new List<PendingReferenceVersion?>();
            foreach (var contract in futuresContracts)
                references.Add(await (context.StageReferenceAsync(contract))
                .ConfigureAwait(false));
            {
                var commands = futuresContracts.SelectMany(context.OtherProjectionKeys).Select(key => db.Use("NativeSecurities.DeletePreviousSymbolKey", SecuritiesDbSql.DeleteFuturesContractBySymbolV3)
            .SetParameters(new DeleteFuturesContractBySymbolV3(key.Symbol, key.Rollover, key.OnTheRun, key.LastTradeDate, key.ContractId))
            .QueueCommand()).ToList();
                commands.Add(db.Use("NativeSecurities.InsertContracts", SecuritiesDbSql.InsertFuturesContract)
            .SetParameters(futuresContracts.Select(context.ToInsertParameters))
            .QueueCommand());
                commands.Add(db.Use("NativeSecurities.InsertSymbolContracts", SecuritiesDbSql.InsertFuturesContractBySymbolV3)
            .SetParameters(futuresContracts.Select(context.ToInsertParameters))
            .QueueCommand());
                await db.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
            .ConfigureAwait(false);
                foreach (var reference in references)
                    await context.CommitReferenceAsync(reference)
            .ConfigureAwait(false);
                return;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Update an existing futures contract in SecuritiesDb
        /// </summary>
        /// <param name = "e">The ID of the futures contract to update</param>
        /// <param name = "futuresContract">The futures contract to update</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task UpdateFuturesContractAsyncCoreBinding(FuturesContractId e, FuturesContractV3ReadModel futuresContract)
        {
            var reference = await (context.StageReferenceAsync(futuresContract, e.ContractId))
                .ConfigureAwait(false);
            var db = context.DbFactory.SecuritiesDb;
            var originalContract = await (context.GetFuturesContractAsync(e))
                .ConfigureAwait(false);
            var replacementProjectionKey = context.ToProjectionKey(futuresContract);
            List<object> queuedCommands = [];
            if (originalContract is not null)
            {
                var originalProjectionKey = context.ToProjectionKey(originalContract);
                if (!string.Equals(originalContract.ContractId, futuresContract.ContractId, StringComparison.Ordinal) || !string.Equals(originalContract.Symbol, futuresContract.Symbol, StringComparison.Ordinal) || originalContract.LastTradeDate != futuresContract.LastTradeDate)
                {
        var preparedParameters1 = new DeleteFuturesContractById(originalContract.ContractId, originalContract.Symbol, originalContract.LastTradeDate);
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractById)}", SecuritiesDbSql.DeleteFuturesContractById)
            .SetParameters(preparedParameters1)
            .QueueCommand());
                }

                if (originalProjectionKey != replacementProjectionKey)
                {
        var preparedParameters2 = new DeleteFuturesContractBySymbolV3(originalContract.Symbol, originalContract.Rollover, originalContract.OnTheRun, originalContract.LastTradeDate, originalContract.ContractId);
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbSql.DeleteFuturesContractBySymbolV3)
            .SetParameters(preparedParameters2)
            .QueueCommand());
                }
            }
            else
            {
                if (!string.Equals(e.ContractId, futuresContract.ContractId, StringComparison.Ordinal) || !string.Equals(e.Symbol, futuresContract.Symbol, StringComparison.Ordinal) || e.MaturityDate != futuresContract.LastTradeDate)
                {
        var preparedParameters3 = new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate);
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractById)}", SecuritiesDbSql.DeleteFuturesContractById)
            .SetParameters(preparedParameters3)
            .QueueCommand());
                }

                foreach (var rollover in new[]
                {
                    false,
                    true
                }

                )
                    foreach (var onTheRun in new[]
                    {
                        false,
                        true
                    }

                    )
                    {
                        var candidate = new FuturesContractProjectionKey(e.Symbol, rollover, onTheRun, e.MaturityDate, e.ContractId);
                        if (candidate == replacementProjectionKey)
                            continue;
        var preparedParameters4 = new DeleteFuturesContractBySymbolV3(candidate.Symbol, candidate.Rollover, candidate.OnTheRun, candidate.LastTradeDate, candidate.ContractId);
                        queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbSql.DeleteFuturesContractBySymbolV3)
            .SetParameters(preparedParameters4)
            .QueueCommand());
                    }
            }

            queuedCommands.AddRange([db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesContract)}", SecuritiesDbSql.InsertFuturesContract)
            .SetParameters(context.ToInsertParameters(futuresContract))
            .QueueCommand(),
                db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesContractBySymbolV3)}", SecuritiesDbSql.InsertFuturesContractBySymbolV3)
            .SetParameters(context.ToInsertParameters(futuresContract))
            .QueueCommand()]);
            await (context.ExecuteProjectionMutationAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, [e.Symbol, futuresContract.Symbol], () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
            await (context.CommitReferenceAsync(reference))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Deletes a futures contract from the database asynchronously.
        /// </summary>
        /// <remarks>This method removes the specified futures contract from the database. Ensure that the
        /// provided  <paramref name = "e"/> contains valid and complete information for the contract to be deleted.</remarks>
        /// <param name = "e">The identifier of the futures contract to delete, including the contract ID, symbol, and maturity date.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task DeleteFuturesContractAsyncCoreBinding(FuturesContractId e)
        {
            var db = context.DbFactory.SecuritiesDb;
        var preparedParameters6 = new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate);
            List<object> queuedCommands = [db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractById)}", SecuritiesDbSql.DeleteFuturesContractById)
            .SetParameters(preparedParameters6)
            .QueueCommand(), ];
            foreach (var rollover in new[]
            {
                false,
                true
            }

            )
                foreach (var onTheRun in new[]
                {
                    false,
                    true
                }

                )
                {
        var preparedParameters5 = new DeleteFuturesContractBySymbolV3(e.Symbol, rollover, onTheRun, e.MaturityDate, e.ContractId);
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesContractBySymbolV3)}", SecuritiesDbSql.DeleteFuturesContractBySymbolV3)
            .SetParameters(preparedParameters5)
            .QueueCommand());
                }

            await (context.ExecuteProjectionMutationAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, [e.Symbol], () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Retrieves a futures contract based on the specified contract identifier.
        /// </summary>
        /// <remarks>This method queries the database to retrieve details of a specific futures contract. Ensure
        /// that the provided  <paramref name = "e"/> contains valid and complete information for the query to
        /// succeed.</remarks>
        /// <param name = "e">The identifier of the futures contract, including the contract ID, symbol, and maturity date.</param>
        /// <returns>A <see cref = "FuturesContractV3ReadModel"/> representing the futures contract if found; otherwise, <see 
        ///langword="null"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task<FuturesContractV3ReadModel?> GetFuturesContractAsyncCoreBinding(FuturesContractId e)
        {
        var preparedParameters7 = new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate);
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractById)}", SecuritiesDbSql.GetFuturesContractById)
            .SetParameters(preparedParameters7)
            .ExecuteSingleAsync(SecuritiesDbContext.MapToFuturesContract!))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetFuturesContractAsync operation.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task<FuturesContractV3ReadModel?> GetFuturesContractAsyncCoreBinding(FuturesContractId e, CancellationToken cancellationToken)
        {
        var preparedParameters8 = new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate);
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractById)}", SecuritiesDbSql.GetFuturesContractById)
            .SetParameters(preparedParameters8)
            .ExecuteSingleAsync(SecuritiesDbContext.MapToFuturesContract!, cancellationToken))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertFuturesOptionContractAsync operation.</summary>
        internal async Task InsertFuturesOptionContractAsyncCoreBinding(FuturesOptionContractReadModel futuresOptionContract)
        {
            var reference = await (context.StageReferenceAsync(futuresOptionContract))
                .ConfigureAwait(false);
            var db = context.DbFactory.SecuritiesDb;
            var parameters = context.ToInsertParameters(futuresOptionContract);
            List<object> queuedCommands = [db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesOptionContract)}", SecuritiesDbSql.InsertFuturesOptionContract)
            .SetParameters(parameters)
            .QueueCommand(),
                db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2)
            .SetParameters(parameters)
            .QueueCommand()];
            await (context.ExecuteProjectionMutationAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection, [futuresOptionContract.Symbol], () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
            await (context.CommitReferenceAsync(reference))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Asynchronously inserts a collection of futures option contracts into the database.
        /// </summary>
        /// <remarks>This method uses the database factory to execute an asynchronous command that inserts the
        /// provided futures option contracts. Ensure that each contract in the collection has all required fields populated
        /// to avoid database errors.</remarks>
        /// <param name = "futuresOptionContract">A collection of <see cref = "FuturesOptionContractReadModel"/> objects representing the futures option contracts
        /// to be inserted. Each object must contain valid contract details such as contract ID, description, symbol, and
        /// other relevant properties.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertFuturesOptionContractsAsyncCoreBinding(ICollection<FuturesOptionContractReadModel> futuresOptionContract)
        {
            if (futuresOptionContract.Count == 0)
                return;
            var db = context.DbFactory.SecuritiesDb;
            var references = new List<PendingReferenceVersion?>();
            foreach (var contract in futuresOptionContract)
                references.Add(await (context.StageReferenceAsync(contract))
                .ConfigureAwait(false));
            {
                List<object> commands = [db.Use("NativeSecurities.InsertOptions", SecuritiesDbSql.InsertFuturesOptionContract)
            .SetParameters(futuresOptionContract.Select(context.ToInsertParameters))
            .QueueCommand(),
                db.Use("NativeSecurities.InsertSymbolOptions", SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2)
            .SetParameters(futuresOptionContract.Select(context.ToInsertParameters))
            .QueueCommand()];
                await db.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
            .ConfigureAwait(false);
                foreach (var reference in references)
                    await context.CommitReferenceAsync(reference)
            .ConfigureAwait(false);
                return;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpdateFuturesOptionContractAsync operation.</summary>
        internal async Task UpdateFuturesOptionContractAsyncCoreBinding(string originalContractId, FuturesOptionContractReadModel futuresOptionContract)
        {
            var reference = await (context.StageReferenceAsync(futuresOptionContract, originalContractId))
                .ConfigureAwait(false);
            var db = context.DbFactory.SecuritiesDb;
            var originalContract = await (context.GetFuturesOptionContractAsync(originalContractId))
                .ConfigureAwait(false);
            var replacementProjectionKey = context.ToProjectionKey(futuresOptionContract);
            List<object> queuedCommands = [];
            if (originalContract is not null)
            {
                var originalProjectionKey = context.ToProjectionKey(originalContract);
                if (originalProjectionKey != replacementProjectionKey)
                {
        var preparedParameters9 = new DeleteFuturesOptionContractById(originalContract.ContractId, originalContract.ContractMonth, originalContract.Symbol, originalContract.OptionType, originalContract.StrikePrice);
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesOptionContractById)}", SecuritiesDbSql.DeleteFuturesOptionContractById)
            .SetParameters(preparedParameters9)
            .QueueCommand());
        var preparedParameters10 = new DeleteFuturesOptionContractBySymbolV2(originalContract.Symbol, originalContract.ContractMonth, originalContract.OptionType, originalContract.StrikePrice, originalContract.ContractId);
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesOptionContractBySymbolV2)}", SecuritiesDbSql.DeleteFuturesOptionContractBySymbolV2)
            .SetParameters(preparedParameters10)
            .QueueCommand());
                }
            }
            else if (!string.Equals(originalContractId, futuresOptionContract.ContractId, StringComparison.Ordinal))
            {
        var preparedParameters11 = new DeleteFuturesOptionContract(originalContractId);
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.DeleteFuturesOptionContract)}", SecuritiesDbSql.DeleteFuturesOptionContract)
            .SetParameters(preparedParameters11)
            .QueueCommand());
            }

            queuedCommands.AddRange([db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesOptionContract)}", SecuritiesDbSql.InsertFuturesOptionContract)
            .SetParameters(context.ToInsertParameters(futuresOptionContract))
            .QueueCommand(),
                db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2)}", SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2)
            .SetParameters(context.ToInsertParameters(futuresOptionContract))
            .QueueCommand()]);
            await (context.ExecuteProjectionMutationAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection, originalContract is null ? [futuresOptionContract.Symbol] : [originalContract.Symbol, futuresOptionContract.Symbol], () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
            await (context.CommitReferenceAsync(reference))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetFuturesOptionContractsByIdsAsync operation.</summary>
        internal async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsyncCoreBinding(ICollection<string> contractIds)
        {
            if (contractIds.Count == 0)
                return[];
        var preparedParameters12 = new GetFuturesOptionContractsByIds(contractIds);
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContractsByIds)}", SecuritiesDbSql.GetFuturesOptionContractsByIds)
            .SetParameters(preparedParameters12)
            .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesOptionContract!))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetFuturesOptionContractsByIdsAsync operation.</summary>
        internal async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsyncCoreBinding(ICollection<string> contractIds, CancellationToken cancellationToken)
        {
            if (contractIds.Count == 0)
                return[];
        var preparedParameters13 = new GetFuturesOptionContractsByIds(contractIds);
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContractsByIds)}", SecuritiesDbSql.GetFuturesOptionContractsByIds)
            .SetParameters(preparedParameters13)
            .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesOptionContract!, cancellationToken))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the NativeTransaction persistence helper.</summary>
        internal EventSourceDb.PostgresEventTransaction NativeTransaction() => new(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection, context.ConnectionString, "System.Data.Postgres"));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the ReadNativeOptionPageAsync persistence helper.</summary>
        internal async Task<FuturesOptionContractPageReadModel> ReadNativeOptionPageAsync(QualifiedOptionCatalogPage request, CancellationToken token)
        {
            SecuritiesDbContext.NativeOptionCursor? cursor = null;
            if (!string.IsNullOrEmpty(request.ContinuationToken))
            {
                try
                {
                    cursor = System.Text.Json.JsonSerializer.Deserialize<SecuritiesDbContext.NativeOptionCursor>(Convert.FromBase64String(request.ContinuationToken));
                }
                catch (Exception error)when (error is FormatException or System.Text.Json.JsonException)
                {
                    throw new ArgumentException("Invalid PostgreSQL option catalog cursor.", nameof(request), error);
                }

                if (cursor is null || !string.Equals(cursor.Symbol, request.Symbol, StringComparison.Ordinal) || cursor.PageSize != request.PageSize)
                    throw new ArgumentException("The option catalog cursor belongs to a different query.", nameof(request));
            }
        var preparedParameters14 = new NativeOptionPageParameters(request.Symbol, cursor?.Month, cursor?.OptionType, cursor?.Strike, cursor?.ContractId, request.PageSize + 1);

            var rows = await context.DbFactory.SecuritiesDb.Use("Securities.NativeOptionPage", SecuritiesDbSql.GetNativeOptionContractsPage)
            .SetParameters(preparedParameters14)
            .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesOptionContract!, token)
            .ConfigureAwait(false);
            var items = rows.Take(request.PageSize).ToArray();
            var last = items.LastOrDefault();
            var continuation = rows.Count > request.PageSize && last is not null ? Convert.ToBase64String(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new SecuritiesDbContext.NativeOptionCursor(request.Symbol, request.PageSize, last.ContractMonth, last.OptionType.ToString(), last.StrikePrice, last.ContractId))) : null;
            return new(items, continuation);
        }

        /// <summary>Resolves one established leg from its symbol/expiry partition in the active published definition generation.</summary>
        /// <param name="contractId">The exact canonical option ID; it is used only to locate and filter saved provider definitions.</param>
        /// <param name="cancellationToken">Cancels the bounded symbol/expiry read.</param>
        /// <returns>The stored provider definition; no reference data is synthesized or republished.</returns>
        internal async Task<FuturesOptionContractReadModel?> GetPublishedFuturesOptionContractAsync(
            string contractId, CancellationToken cancellationToken)
        {
            FuturesOptionContractId identity;
            try { identity = new FuturesOptionContractId(contractId); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return null; }
            var expiry = DateOnly.FromDateTime(identity.MaturityDate);
            var db = context.DbFactory.SecuritiesDb;
        var preparedParameters15 = new GetOptionContractExpiryCalendarState(identity.Symbol);
            var published = await db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetOptionContractExpiryCalendarState)}", SecuritiesDbSql.GetOptionContractExpiryCalendarState)
            .SetParameters(preparedParameters15)
            .ExecuteSingleAsync(SecuritiesDbContext.MapToOptionExpiryCalendarState!, cancellationToken)
            .ConfigureAwait(false);
            if (published is null || expiry < published.CoverageFrom || expiry > published.CoverageThrough) return null;
        var preparedParameters16 = new GetCachedOptionContractDefinitions(identity.Symbol, published.Generation, expiry);
            var definitions = await db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetCachedOptionContractDefinitions)}", SecuritiesDbSql.GetCachedOptionContractDefinitions)
            .SetParameters(preparedParameters16)
            .ExecuteQueryAsync(SecuritiesDbContext.MapToCachedOptionContractDefinition!, cancellationToken)
            .ConfigureAwait(false);
            var exact = definitions.Where(row => string.Equals(row?.Definition.ContractId,contractId,StringComparison.Ordinal))
                .Select(row => row!.Definition).ToArray();
            return exact.Length switch
            {
                0 => null,
                1 => exact[0],
                _ => throw new StorageException($"SecuritiesDb published futures-option contractId '{contractId}' is ambiguous across {exact.Length} definitions.")
            };
        }


        /// <summary>Reads one content-addressed pricing bundle from SecuritiesDb.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<string?> GetOptionPricingReferenceBundlePayloadCoreAsync(string bundleId, CancellationToken cancellationToken)
        {
            return await (context.NativeTransaction().ExecuteOnceAsync(async(db,ct) =>
                    (await (db.ScalarAsync(SecuritiesDbSql.DbContextExtensions_GetOptionPricingReferenceBundlePayloadCoreAsync_1, [bundleId], ct))
                .ConfigureAwait(false)) as string, cancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>Writes one immutable pricing bundle to SecuritiesDb.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task InsertOptionPricingReferenceBundleCoreAsync(string bundleId, string payload, CancellationToken cancellationToken)
        {
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async(db,ct) => { await (context.NativeReferences.InsertBundleAsync(db,bundleId,payload,ct))
                .ConfigureAwait(false); return true; },cancellationToken))
                .ConfigureAwait(false);
                return;
            }
        }

        /// <summary>
        /// Performs the ToProjectionKey Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal FuturesOptionContractProjectionKey ToProjectionKey(FuturesOptionContractReadModel contract)
            => new(contract.Symbol, contract.ContractMonth, contract.OptionType, contract.StrikePrice, contract.ContractId);

        /// <summary>
        /// Performs the ToProjectionKey Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal FuturesContractProjectionKey ToProjectionKey(FuturesContractV3ReadModel contract)
            => new(contract.Symbol, contract.Rollover, contract.OnTheRun, contract.LastTradeDate, contract.ContractId);

        /// <summary>
        /// Performs the OtherProjectionKeys Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        /// Performs the ExecuteProjectionMutationAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task ExecuteProjectionMutationAsync(
            IObjectRepository db,
            string projectionName,
            IEnumerable<string> affectedSymbols,
            Func<Task> mutation)
        {
            {
                await mutation()
            .ConfigureAwait(false);
                return;
            }
        }

        /// <summary>
        /// Performs the LoadFuturesContractProjectionAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<FuturesContractV3ReadModel[]> LoadFuturesContractProjectionAsync(
            string symbol,
            CancellationToken cancellationToken = default)
            => [.. (await (context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractsBySymbol)}", SecuritiesDbSql.GetFuturesContractsBySymbol)
            .SetParameters(new GetFuturesContractsBySymbol(symbol))
            .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesContract!, cancellationToken))
                .ConfigureAwait(false))];

        /// <summary>
        /// Performs the LoadFuturesOptionContractProjectionAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<FuturesOptionContractReadModel[]> LoadFuturesOptionContractProjectionAsync(
            string symbol,
            CancellationToken cancellationToken = default)
            => [.. (await (context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContractsBySymbol)}", SecuritiesDbSql.GetFuturesOptionContractsBySymbol)
            .SetParameters(new GetFuturesOptionContractsBySymbol(symbol))
            .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesOptionContract!, cancellationToken))
                .ConfigureAwait(false))];

        /// <summary>
        /// Performs the ReadProjectionInventoryAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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

            await foreach (var key in (db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractProjectionSourceKeys)}", SecuritiesDbSql.GetFuturesContractProjectionSourceKeys)
            .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesContractProjectionKey!, cancellationToken))
                .ConfigureAwait(false))
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
            await foreach (var key in (db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesContractProjectionTargetKeys)}", SecuritiesDbSql.GetFuturesContractProjectionTargetKeys)
            .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesContractProjectionKey!, cancellationToken))
                .ConfigureAwait(false))
            {
                futuresContractProjectionRows++;
                futuresContractProjectionKeys.Add(key);
            }
            await foreach (var key in (db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContractProjectionSourceKeys)}", SecuritiesDbSql.GetFuturesOptionContractProjectionSourceKeys)
            .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesOptionContractProjectionKey!, cancellationToken))
                .ConfigureAwait(false))
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
            await foreach (var key in (db.Use($"{nameof(SecuritiesDbSql)}.{nameof(SecuritiesDbSql.GetFuturesOptionContractProjectionTargetKeys)}", SecuritiesDbSql.GetFuturesOptionContractProjectionTargetKeys)
            .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesOptionContractProjectionKey!, cancellationToken))
                .ConfigureAwait(false))
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        /// Performs the StageReferenceAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<PendingReferenceVersion?> StageReferenceAsync(FuturesOptionContractReadModel value, string? originalId = null)
        {
            var previous = await (context.GetFuturesOptionContractAsync(originalId ?? value.ContractId))
                .ConfigureAwait(false);
            if (value.SchemaVersion == 0)
            {
                if (previous?.SchemaVersion > 0) throw new InvalidOperationException("A provider-bound contract cannot lose its reference metadata.");
                return null;
            }
            if (originalId is not null && !string.Equals(originalId,value.ContractId,StringComparison.Ordinal)
                || previous is not null && (!string.Equals(previous.Symbol,value.Symbol,StringComparison.Ordinal) || previous.ContractMonth != value.ContractMonth
                    || !string.Equals(previous.OptionType,value.OptionType,StringComparison.Ordinal) || previous.GetExactStrikePrice() != value.GetExactStrikePrice()))
                throw new InvalidOperationException("Contract identity changes require Add, not replacement.");
            if (previous?.ReviewState == ReferenceReviewState.Reviewed && value.ReviewState != ReferenceReviewState.Reviewed)
                throw new InvalidOperationException("A reviewed reference cannot be replaced by an unqualified draft.");
            if (string.IsNullOrWhiteSpace(value.UnderlyingContractId))
                throw new InvalidOperationException("An exact existing underlying futures contract is required.");
            var underlying = await (context.GetFuturesContractAsync(value.UnderlyingContractId))
                .ConfigureAwait(false);
            if (underlying is null || !string.Equals(underlying.SecurityType,"FUT",StringComparison.Ordinal) || underlying.SchemaVersion != 1
                || !string.Equals(underlying.Dataset,value.Dataset,StringComparison.Ordinal) || !string.Equals(underlying.Symbol,value.Symbol,StringComparison.Ordinal)
                || value.UnderlyingInstrumentId is { } providerId && providerId != underlying.InstrumentId
                || value.UnderlyingPublisherId is { } publisherId && publisherId != underlying.PublisherId
                || value.ExpirationUtc is not null && (underlying.ExpirationUtc is null || underlying.ExpirationUtc < value.ExpirationUtc)
                || value.ReviewState == ReferenceReviewState.Reviewed && FuturesReferenceQualification.Errors(underlying).Count != 0)
                throw new InvalidOperationException("The exact underlying future is missing or incompatible with the option.");
            if (value.ReviewState == ReferenceReviewState.Reviewed
                && await (context.GetReferenceVersionAsync(new ExactReferenceVersion(underlying.ContractId, underlying.MappingVersion!)))
                .ConfigureAwait(false) is null)
                throw new InvalidOperationException("The underlying future's reviewed version has not completed publication.");
            return await (context.StageReferenceVersionAsync(PreparedReferenceVersion.Prepare(value)))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the StageReferenceAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<PendingReferenceVersion?> StageReferenceAsync(FuturesContractV3ReadModel value, string? originalId = null)
        {
            var previous = await (context.GetFuturesContractAsync(originalId ?? value.ContractId))
                .ConfigureAwait(false);
            if (value.SchemaVersion == 0)
            {
                if (previous?.SchemaVersion > 0) throw new InvalidOperationException("A provider-bound contract cannot lose its reference metadata.");
                return null;
            }
            if (originalId is not null && !string.Equals(originalId,value.ContractId,StringComparison.Ordinal)
                || previous is not null && (!string.Equals(previous.Symbol,value.Symbol,StringComparison.Ordinal) || previous.LastTradeDate != value.LastTradeDate))
                throw new InvalidOperationException("Contract identity changes require Add, not replacement.");
            if (previous?.ReviewState == ReferenceReviewState.Reviewed && value.ReviewState != ReferenceReviewState.Reviewed)
                throw new InvalidOperationException("A reviewed reference cannot be replaced by an unqualified draft.");
            return await (context.StageReferenceVersionAsync(PreparedReferenceVersion.Prepare(value)))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the CommitReferenceAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task CommitReferenceAsync(PendingReferenceVersion? value) =>
            value is null ? Task.CompletedTask : context.CommitReferenceVersionAsync(value);



        /// <summary>Stages a qualified immutable version and reserves both exact identities.</summary>
        internal async Task<PendingReferenceVersion> StageReferenceVersionCoreAsync(PreparedReferenceVersion prepared,
            CancellationToken cancellationToken, EventSourceDb.IEnlistedPostgresTransaction? enlisted = null)
        {
            var reference = prepared.Reference;
            var payload = MessagePackBinarySerializer.Shared.Serialize(prepared.Value)!;
            if (enlisted is not null)
            {
                await context.NativeReferences.StageAsync(enlisted, reference.ContractId, prepared.ProviderKey,
                    prepared.Identity, reference.Version, reference.Digest, payload, cancellationToken, prepared.Value.Option)
                    .ConfigureAwait(false);
                return reference;
            }
            await context.NativeTransaction().ExecuteOnceAsync(async (db, token) =>
            {
                await context.NativeReferences.StageAsync(db, reference.ContractId, prepared.ProviderKey, prepared.Identity,
                    reference.Version, reference.Digest, payload, token, prepared.Value.Option)
                    .ConfigureAwait(false);
                return true;
            }, cancellationToken)
                .ConfigureAwait(false);
            return reference;
        }

        /// <summary>Resolves canonical option names against immutable recorded provider ownership before publishing a new generation.</summary>
        internal async Task<CachedOptionContractDefinitionReadModel[]> ResolveOptionReferenceIdentitiesCoreAsync(
            IReadOnlyList<CachedOptionContractDefinitionReadModel> rows, CancellationToken cancellationToken)
        {
            var keys = rows.SelectMany(row => new[] { OptionReferenceIdentity.ProviderKey(row.Definition), OptionReferenceIdentity.LegacyProviderKey(row.Definition), "ifm:" + row.Definition.ContractId,
                "ifm:" + FuturesOptionContractId.WithProviderIdentity(row.Definition.ContractId, row.Definition.PublisherId ?? 0, row.Definition.InstrumentId ?? 0) })
                .Distinct(StringComparer.Ordinal).ToArray();
            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var batch in keys.Chunk(256))
            {
        var preparedParameters17 = new GetReferenceIdentityClaims(JsonSerializer.Serialize(batch));
                var found = await context.Use("SecuritiesReference.ReadIdentityClaims", SecuritiesDbSql.DbContextExtensions_ResolveOptionReferenceIdentitiesCoreAsync_1)
            .SetParameters(preparedParameters17)
            .ExecuteQueryAsync(SecuritiesDbContext.MapToIdentityClaim, cancellationToken)
            .ConfigureAwait(false);
                foreach (var row in found) bindings.Add(row.Key, row.Binding);
            }
            var resolved = OptionReferenceIdentity.Resolve(rows.Select(row => row.Definition).ToArray(), bindings);
            return rows.Select((row, index) => row with { Definition = resolved[index] }).ToArray();
        }

        /// <summary>Publishes a previously staged immutable reference version.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task CommitReferenceVersionCoreAsync(
            PendingReferenceVersion value,
            CancellationToken cancellationToken,
            TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction? enlisted = null)
        {
            {
                if (enlisted is not null)
                {
                    await context.NativeReferences.PublishAsync(enlisted,value.ContractId,value.Version,value.Digest,cancellationToken)
            .ConfigureAwait(false);
                    return;
                }
                await (context.NativeTransaction().ExecuteOnceAsync(async(db,ct) => { await (context.NativeReferences.PublishAsync(db,value.ContractId,value.Version,value.Digest,ct))
                .ConfigureAwait(false); return true; },cancellationToken))
                .ConfigureAwait(false);
                return;
            }
        }

        /// <summary>Gets a published immutable reference version.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ReferenceContractVersion?> GetReferenceVersionCoreAsync(
            string contractId,
            string version,
            CancellationToken cancellationToken)
        {
            var row = await context.ReadReferenceVersionAsync(contractId, version, cancellationToken)
            .ConfigureAwait(false);
            return row is null ? null : context.ValidatePublishedReferenceVersion(contractId, row.Value);
        }

        internal ReferenceContractVersion? ValidatePublishedReferenceVersion(string contractId, ReferenceVersionStorageRow row)
        {
            if (!row.Published) return null;
            var value = row.Value;
            if (!string.Equals((value.Option?.ContractId ?? value.Future?.ContractId),contractId,StringComparison.Ordinal)
                || value.Convention is not null && value.Convention != ReviewedOptionConvention.From(value.Option!))
                throw new InvalidDataException("Reference version and pricing convention disagree.");
            return value;
        }

        /// <summary>Gets whether a staged or published immutable reference version exists.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<bool> ContainsReferenceVersionCoreAsync(
            string contractId,
            string version,
            CancellationToken cancellationToken)
            => await context.ReadReferenceVersionAsync(contractId, version, cancellationToken)
            .ConfigureAwait(false) is not null;

        /// <summary>Lists a bounded page of staged and published reference versions.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsCoreAsync(
            string contractId,
            string afterVersion,
            int limit,
            CancellationToken cancellationToken)
        {
            return await (context.NativeTransaction().ExecuteOnceAsync((db,ct) => db.QueryAsync(SecuritiesDbSql.DbContextExtensions_ListReferenceVersionsCoreAsync_1, [contractId, afterVersion, limit], SecuritiesDbContext.MapToReferenceVersionSummary, ct),cancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>Reads and verifies one staged or published reference-version row.</summary>
        internal async Task<ReferenceVersionStorageRow?> ReadReferenceVersionAsync(
            string contractId, string version, CancellationToken cancellationToken)
        {
            var rows = await context.NativeTransaction()
                .ExecuteOnceAsync((db, ct) => db.QueryAsync(SecuritiesDbSql.DbContextExtensions_ReadReferenceVersionAsync_1, [contractId, version], SecuritiesDbContext.MapToReferencePayload, ct), cancellationToken)
                .ConfigureAwait(false);
            if (rows.Count == 0) return null;
            var row = rows.Single();
            return context.DecodeReferenceVersion(row.Digest, row.Published, row.Payload);
        }

        internal ReferenceVersionStorageRow DecodeReferenceVersion(string digest, bool published, byte[] payload)
        {
            if (payload.Length is 0 or > 131072)
                throw new InvalidDataException("Reference version payload exceeds its size bound.");
            var value = MessagePackBinarySerializer.Shared.Deserialize<ReferenceContractVersion>(payload)
                ?? throw new InvalidDataException("Reference version payload is missing.");
            if (!string.Equals(value.CalculateReferenceVersionDigest(), digest, StringComparison.Ordinal))
                throw new InvalidDataException("Reference version checksum mismatch.");
            return new(digest, published, value);
        }

    }

    extension(ReferenceContractVersion value)
    {
        /// <summary>Calculates the stable content digest for an immutable reference version.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string CalculateReferenceVersionDigest()
            => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    }
    extension(IEnumerable<CachedOptionContractDefinitionReadModel?> rows)
    {
    internal IEnumerable<CachedOptionContractDefinitionReadModel> FilterDefinitions(string underlyingContractId, HashSet<string> roots)
        => rows.Where(row => row is not null
                            && string.Equals(row.UnderlyingContractId, underlyingContractId, StringComparison.OrdinalIgnoreCase)
                            && (roots.Count == 0 || roots.Contains(row.ProviderRoot)))
            .Select(row => row!).DistinctBy(row => row.Definition.ContractId);


    }

}

/// <summary>Persists immutable pricing and contract reference facts on a supplied PostgreSQL transaction.</summary>
public sealed class PostgresSecuritiesReferenceStore
{
    /// <summary>Stores a content-addressed bundle; the same identifier cannot acquire different content.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task InsertBundleAsync(IEnlistedPostgresTransaction db, string id, string payload, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_InsertBundleAsync_1, [id, payload], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Pricing bundle identifier contains different content.");
    }

    /// <summary>Reserves the exact immutable IFM/provider identity; conflicting claims roll back the caller's transaction.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ClaimIdentityAsync(IEnlistedPostgresTransaction db, string key, string binding, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_ClaimIdentityAsync_1, [key, binding], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException($"Provider/IFM identity collision. Existing contract identities cannot be overwritten. IdentityKey={key}");
    }

    /// <summary>Atomically claims both identities and stages one immutable mapping version without publishing it.</summary>
    public async Task StageAsync(IEnlistedPostgresTransaction db, string contractId, string providerKey, string identity,
        string version, string digest, byte[] payload, CancellationToken token = default, FuturesOptionContractReadModel? option = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (option is not null)
        {
            var legacyKey = OptionReferenceIdentity.LegacyProviderKey(option);
            var legacy = await db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_StageAsync_1, [legacyKey], token)
            .ConfigureAwait(false);
            if (legacy is string binding)
            {
                var existing = OptionReferenceIdentity.ExistingProviderBinding(option, new Dictionary<string, string> { [legacyKey] = binding });
                if (existing is not null && existing != identity)
                    throw new InvalidOperationException($"Provider/IFM identity collision. Existing contract identities cannot be overwritten. IdentityKey={legacyKey}");
            }
        }
        foreach(var key in new[]{"ifm:"+contractId,providerKey}.Order(StringComparer.Ordinal))
            await (ClaimIdentityAsync(db,key,identity,token))
            .ConfigureAwait(false);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_StageAsync_2, [contractId, version, digest, payload], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Mapping version already contains different content. Publish a new reviewed version.");
    }

    /// <summary>Publishes only the exact previously accepted mapping digest; missing or different versions are refused.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task PublishAsync(IEnlistedPostgresTransaction db, string contractId, string version, string digest, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_PublishAsync_1, [contractId, version, digest], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Reference publication did not complete.");
    }
    /// <summary>Writes a complete immutable option definition generation and its published pointer in one transaction.</summary>
    /// <remarks>Previous generations remain readable by requests that already captured their pointer; cleanup is separate maintenance.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task PublishDefinitionsAsync(IEnlistedPostgresTransaction db, string symbol, Guid generation,
        DateOnly coverageFrom, DateOnly coverageThrough, DateTime refreshedAtUtc,
        IReadOnlyList<(DateOnly Expiry, string Root, string ContractId, string UnderlyingId, string Family, byte[] Payload)> rows,
        CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var batch = rows.Count == 0 ? null :
            (object?[])new InsertOptionDefinitionGenerationParameters(symbol, generation, rows, refreshedAtUtc).Bind();
        await db.ExecuteAsync(SecuritiesDbSql.PostgresReferenceStore_PublishDefinitionsAsync_1, ["securities.option-definitions:" + symbol], token)
            .ConfigureAwait(false);
        if (batch is not null)
            await db.ExecuteAsync(SecuritiesDbSql.InsertOptionDefinitionGenerationRows, batch, token)
                .ConfigureAwait(false);
        await db.ExecuteAsync(SecuritiesDbSql.PostgresReferenceStore_PublishDefinitionsAsync_3, [symbol, generation, coverageFrom, coverageThrough, refreshedAtUtc], token)
            .ConfigureAwait(false);
    }

}

internal readonly record struct ExactReferenceVersionRow(
    (string ContractId, string Version) Key,
    (string Digest, bool Published, byte[] Payload) Value);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct FuturesContractProjectionKey(
    string Symbol,
    bool Rollover,
    bool OnTheRun,
    DateOnly LastTradeDate,
    string ContractId);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct FuturesOptionContractProjectionKey(
    string Symbol,
    DateOnly ContractMonth,
    string OptionType,
    double StrikePrice,
    string ContractId);

internal sealed record OptionExpiryCalendarState(
    Guid Generation,
    DateOnly CoverageFrom,
    DateOnly CoverageThrough,
    DateTime RefreshedAtUtc);

internal sealed record OptionPageCursor(string Symbol, int PageSize, ProjectionReadStamp Stamp, byte[] State);

internal sealed record ProjectionInventory(
    HashSet<FuturesContractProjectionKey> FuturesContractSourceKeys,
    HashSet<FuturesContractProjectionKey> FuturesContractTargetKeys,
    HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractSourceKeys,
    HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractTargetKeys,
    int FuturesContractSourceRows,
    int FuturesContractTargetRows,
    int FuturesOptionContractSourceRows,
    int FuturesOptionContractTargetRows);

internal sealed record ProjectionOperation(
    Guid OperationId,
    string ProjectionName,
    bool GlobalWasComplete,
    HashSet<string> CompletedSymbols,
    string[] AffectedSymbols);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProjectionOperationJournalEntry(
    Guid OperationId,
    DateTime StartedOn,
    bool StateMayBeActive);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProjectionOperationScope(string ScopeType, string ScopeKey);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProjectionReadStamp(
    string ProjectionName,
    string Symbol,
    Guid Generation,
    bool IsGlobal,
    Guid? GlobalGeneration);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProjectionState(Guid Generation, bool IsComplete, bool HasNoActiveOperations);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ReferenceVersionStorageRow(
    string Digest,
    bool Published,
    TomasAI.IFM.Domain.Reference.Shared.ViewModels.ReferenceContractVersion Value);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct SymbolProjectionState(
    string Symbol,
    Guid Generation,
    bool IsComplete,
    bool HasNoActiveOperations);
