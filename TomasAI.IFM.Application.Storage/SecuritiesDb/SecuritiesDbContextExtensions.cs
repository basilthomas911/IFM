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
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal static class SecuritiesDbContextExtensions
{
    static readonly byte[] OptionPageSigningKey = RandomNumberGenerator.GetBytes(32);

    extension(SecuritiesDbContext context)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ProjectFuturesContractAsync operation.</summary>
        internal async Task ProjectFuturesContractAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, FuturesContractV3ReadModel contract, string? originalContractId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (!string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                throw new InvalidOperationException("Enlisted securities materialization requires the PostgreSQL provider.");
            ArgumentNullException.ThrowIfNull(contract);
            var parameters = context.ToInsertParameters(contract);
            if (contract.SchemaVersion > 0)
            {
                var reference = await context.StageReferenceVersionCoreAsync(contract, token, transaction)
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
        internal async Task ProjectOptionContractAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, FuturesOptionContractReadModel contract, string? originalContractId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (!string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                throw new InvalidOperationException("Enlisted securities materialization requires the PostgreSQL provider.");
            ArgumentNullException.ThrowIfNull(contract);
            var parameters = context.ToInsertParameters(contract);
            if (contract.SchemaVersion > 0)
            {
                var reference = await context.StageReferenceVersionCoreAsync(contract, token, transaction)
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
            if (!string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                throw new InvalidOperationException("Enlisted securities materialization requires the PostgreSQL provider.");
            ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
            await transaction.ExecuteAsync("DELETE FROM securities.futures_contract_by_symbol WHERE contractId=$1;", [contractId], token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync("DELETE FROM securities.futures_contract WHERE contractId=$1;", [contractId], token)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the RemoveOptionContractAsync operation.</summary>
        internal async Task RemoveOptionContractAsyncCoreBinding(EventSourceDb.IEnlistedPostgresTransaction transaction, string contractId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            if (!string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                throw new InvalidOperationException("Enlisted securities materialization requires the PostgreSQL provider.");
            ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
            await transaction.ExecuteAsync("DELETE FROM securities.futures_option_contract_by_symbol WHERE contractId=$1;", [contractId], token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync("DELETE FROM securities.futures_option_contract WHERE contractId=$1;", [contractId], token)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the StageReferenceVersionAsync operation.</summary>
        internal Task<PendingReferenceVersion> StageReferenceVersionAsyncCoreBinding(FuturesContractV3ReadModel value, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(value);
            return context.StageReferenceVersionCoreAsync(value, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the StageReferenceVersionAsync operation.</summary>
        internal Task<PendingReferenceVersion> StageReferenceVersionAsyncCoreBinding(FuturesOptionContractReadModel value, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(value);
            return context.StageReferenceVersionCoreAsync(value, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the CommitReferenceVersionAsync operation.</summary>
        internal Task CommitReferenceVersionAsyncCoreBinding(PendingReferenceVersion value, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(value);
            return context.CommitReferenceVersionCoreAsync(value, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ListReferenceVersionsAsync operation.</summary>
        internal Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsAsyncCoreBinding(string contractId, string afterVersion = "", int limit = 100, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(afterVersion);
            return context.ListReferenceVersionsCoreAsync(contractId, afterVersion, limit, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertFuturesContractAsync operation.</summary>
        internal async Task InsertFuturesContractAsyncCoreBinding(FuturesContractV3ReadModel futuresContract)
        {
            ArgumentNullException.ThrowIfNull(futuresContract);
            var reference = await (context.StageReferenceAsync(futuresContract))
                .ConfigureAwait(false);
            var db = context.DbFactory.SecuritiesDb;
            var parameters = context.ToInsertParameters(futuresContract);
            context.EnsureDistinctFuturesContractWrites([futuresContract]);
            List<object> queuedCommands = context.OtherProjectionKeys(futuresContract).Select(key => db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3 : SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(new DeleteFuturesContractBySymbolV3(key.Symbol, key.Rollover, key.OnTheRun, key.LastTradeDate, key.ContractId))
                .QueueCommand()).ToList();
            queuedCommands.AddRange([db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContract : SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(parameters)
                .QueueCommand(), db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContractBySymbolV3 : SecuritiesDbCql.InsertFuturesContractBySymbolV3)
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
            ArgumentNullException.ThrowIfNull(futuresContracts);
            if (futuresContracts.Count == 0)
                return;
            var db = context.DbFactory.SecuritiesDb;
            context.EnsureDistinctFuturesContractWrites(futuresContracts);
            var references = new List<PendingReferenceVersion?>();
            foreach (var contract in futuresContracts)
                references.Add(await (context.StageReferenceAsync(contract))
                .ConfigureAwait(false));
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
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
        
            var operation = await (context.BeginProjectionOperationAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, futuresContracts.Select(static contract => contract.Symbol)))
                .ConfigureAwait(false);
            var targetMutationSubmissionStarted = false;
            try
            {
                targetMutationSubmissionStarted = true;
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3 : SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(futuresContracts.SelectMany(context.OtherProjectionKeys).Select(key => new DeleteFuturesContractBySymbolV3(key.Symbol, key.Rollover, key.OnTheRun, key.LastTradeDate, key.ContractId)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContract : SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(futuresContracts.Select(context.ToInsertParameters))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContractBySymbolV3 : SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                .SetParameters(futuresContracts.Select(context.ToInsertParameters))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                await (context.CompleteProjectionOperationAsync(db, operation, completeGlobal: false, completeAllSymbols: false))
                .ConfigureAwait(false);
                foreach (var reference in references)
                    await (context.CommitReferenceAsync(reference))
                .ConfigureAwait(false);
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
            ArgumentNullException.ThrowIfNull(e);
            ArgumentNullException.ThrowIfNull(futuresContract);
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
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractById : SecuritiesDbCql.DeleteFuturesContractById)
                .SetParameters(new DeleteFuturesContractById(originalContract.ContractId, originalContract.Symbol, originalContract.LastTradeDate))
                .QueueCommand());
                }
        
                if (originalProjectionKey != replacementProjectionKey)
                {
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3 : SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(new DeleteFuturesContractBySymbolV3(originalContract.Symbol, originalContract.Rollover, originalContract.OnTheRun, originalContract.LastTradeDate, originalContract.ContractId))
                .QueueCommand());
                }
            }
            else
            {
                if (!string.Equals(e.ContractId, futuresContract.ContractId, StringComparison.Ordinal) || !string.Equals(e.Symbol, futuresContract.Symbol, StringComparison.Ordinal) || e.MaturityDate != futuresContract.LastTradeDate)
                {
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractById : SecuritiesDbCql.DeleteFuturesContractById)
                .SetParameters(new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
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
                        queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3 : SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(new DeleteFuturesContractBySymbolV3(candidate.Symbol, candidate.Rollover, candidate.OnTheRun, candidate.LastTradeDate, candidate.ContractId))
                .QueueCommand());
                    }
            }
        
            queuedCommands.AddRange([db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContract : SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(context.ToInsertParameters(futuresContract))
                .QueueCommand(), db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContractBySymbolV3 : SecuritiesDbCql.InsertFuturesContractBySymbolV3)
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
            ArgumentNullException.ThrowIfNull(e);
            var db = context.DbFactory.SecuritiesDb;
            List<object> queuedCommands = [db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractById)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractById : SecuritiesDbCql.DeleteFuturesContractById)
                .SetParameters(new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
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
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3 : SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                .SetParameters(new DeleteFuturesContractBySymbolV3(e.Symbol, rollover, onTheRun, e.MaturityDate, e.ContractId))
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
            ArgumentNullException.ThrowIfNull(e);
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractById)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesContractById : SecuritiesDbCql.GetFuturesContractById)
                .SetParameters(new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
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
            ArgumentNullException.ThrowIfNull(e);
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractById)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesContractById : SecuritiesDbCql.GetFuturesContractById)
                .SetParameters(new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                .ExecuteSingleAsync(SecuritiesDbContext.MapToFuturesContract!, cancellationToken))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertFuturesOptionContractAsync operation.</summary>
        internal async Task InsertFuturesOptionContractAsyncCoreBinding(FuturesOptionContractReadModel futuresOptionContract)
        {
            ArgumentNullException.ThrowIfNull(futuresOptionContract);
            var reference = await (context.StageReferenceAsync(futuresOptionContract))
                .ConfigureAwait(false);
            var db = context.DbFactory.SecuritiesDb;
            var parameters = context.ToInsertParameters(futuresOptionContract);
            List<object> queuedCommands = [db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesOptionContract : SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(parameters)
                .QueueCommand(), db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2 : SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
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
            ArgumentNullException.ThrowIfNull(futuresOptionContract);
            if (futuresOptionContract.Count == 0)
                return;
            var db = context.DbFactory.SecuritiesDb;
            context.EnsureDistinctFuturesOptionContractWrites(futuresOptionContract);
            var references = new List<PendingReferenceVersion?>();
            foreach (var contract in futuresOptionContract)
                references.Add(await (context.StageReferenceAsync(contract))
                .ConfigureAwait(false));
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                List<object> commands = [db.Use("NativeSecurities.InsertOptions", SecuritiesDbSql.InsertFuturesOptionContract)
                .SetParameters(futuresOptionContract.Select(context.ToInsertParameters))
                .QueueCommand(), db.Use("NativeSecurities.InsertSymbolOptions", SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(futuresOptionContract.Select(context.ToInsertParameters))
                .QueueCommand()];
                await db.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
                .ConfigureAwait(false);
                foreach (var reference in references)
                    await context.CommitReferenceAsync(reference)
                .ConfigureAwait(false);
                return;
            }
        
            var operation = await (context.BeginProjectionOperationAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection, futuresOptionContract.Select(static contract => contract.Symbol)))
                .ConfigureAwait(false);
            var targetMutationSubmissionStarted = false;
            try
            {
                targetMutationSubmissionStarted = true;
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesOptionContract : SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(futuresOptionContract.Select(context.ToInsertParameters))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2 : SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(futuresOptionContract.Select(context.ToInsertParameters))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                await (context.CompleteProjectionOperationAsync(db, operation, completeGlobal: false, completeAllSymbols: false))
                .ConfigureAwait(false);
                foreach (var reference in references)
                    await (context.CommitReferenceAsync(reference))
                .ConfigureAwait(false);
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpdateFuturesOptionContractAsync operation.</summary>
        internal async Task UpdateFuturesOptionContractAsyncCoreBinding(string originalContractId, FuturesOptionContractReadModel futuresOptionContract)
        {
            ArgumentNullException.ThrowIfNull(futuresOptionContract);
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
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractById)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesOptionContractById : SecuritiesDbCql.DeleteFuturesOptionContractById)
                .SetParameters(new DeleteFuturesOptionContractById(originalContract.ContractId, originalContract.ContractMonth, originalContract.Symbol, originalContract.OptionType, originalContract.StrikePrice))
                .QueueCommand());
                    queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesOptionContractBySymbolV2 : SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2)
                .SetParameters(new DeleteFuturesOptionContractBySymbolV2(originalContract.Symbol, originalContract.ContractMonth, originalContract.OptionType, originalContract.StrikePrice, originalContract.ContractId))
                .QueueCommand());
                }
            }
            else if (!string.Equals(originalContractId, futuresOptionContract.ContractId, StringComparison.Ordinal))
            {
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesOptionContract : SecuritiesDbCql.DeleteFuturesOptionContract)
                .SetParameters(new DeleteFuturesOptionContract(originalContractId))
                .QueueCommand());
            }
        
            queuedCommands.AddRange([db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesOptionContract : SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(context.ToInsertParameters(futuresOptionContract))
                .QueueCommand(), db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2 : SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
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
            ArgumentNullException.ThrowIfNull(contractIds);
            if (contractIds.Count == 0)
                return[];
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsByIds)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesOptionContractsByIds : SecuritiesDbCql.GetFuturesOptionContractsByIds)
                .SetParameters(new GetFuturesOptionContractsByIds(contractIds))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesOptionContract!))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetFuturesOptionContractsByIdsAsync operation.</summary>
        internal async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsyncCoreBinding(ICollection<string> contractIds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contractIds);
            if (contractIds.Count == 0)
                return[];
            return await (context.DbFactory.SecuritiesDb.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsByIds)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesOptionContractsByIds : SecuritiesDbCql.GetFuturesOptionContractsByIds)
                .SetParameters(new GetFuturesOptionContractsByIds(contractIds))
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
        internal async Task<FuturesOptionContractPageReadModel> ReadNativeOptionPageAsync(GetFuturesOptionContractsPageParameter request, CancellationToken token)
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
        
            var rows = await context.DbFactory.SecuritiesDb.Use("Securities.NativeOptionPage", SecuritiesDbSql.GetNativeOptionContractsPage)
                .SetParameters(new NativeOptionPageParameters(request.Symbol, cursor?.Month, cursor?.OptionType, cursor?.Strike, cursor?.ContractId, request.PageSize + 1))
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
            var published = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiryCalendarState)}",
                string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? SecuritiesDbSql.GetOptionContractExpiryCalendarState : SecuritiesDbCql.GetOptionContractExpiryCalendarState)
                .SetParameters(new GetOptionContractExpiryCalendarState(identity.Symbol))
                .ExecuteSingleAsync(SecuritiesDbContext.MapToOptionExpiryCalendarState!, cancellationToken)
                .ConfigureAwait(false);
            if (published is null || expiry < published.CoverageFrom || expiry > published.CoverageThrough) return null;
            var definitions = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetCachedOptionContractDefinitions)}",
                string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) ? SecuritiesDbSql.GetCachedOptionContractDefinitions : SecuritiesDbCql.GetCachedOptionContractDefinitions)
                .SetParameters(new GetCachedOptionContractDefinitions(identity.Symbol, published.Generation, expiry))
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
            if(string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
                return await (context.NativeTransaction().ExecuteOnceAsync(async(db,ct) =>
                    (await (db.ScalarAsync("SELECT payload FROM securities.option_pricing_reference_bundle WHERE bundle_id=$1;",[bundleId],ct))
                .ConfigureAwait(false)) as string, cancellationToken))
                .ConfigureAwait(false);
            var rows = await context.Use(nameof(SecuritiesDbCql.GetOptionPricingReferenceBundle), SecuritiesDbCql.GetOptionPricingReferenceBundle)
                .SetParameters(new GetOptionPricingReferenceBundle(bundleId))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToOptionPricingReferenceBundlePayload, cancellationToken)
                .ConfigureAwait(false);
            return rows.SingleOrDefault();
        }

        /// <summary>Writes one immutable pricing bundle to SecuritiesDb.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task InsertOptionPricingReferenceBundleCoreAsync(string bundleId, string payload, CancellationToken cancellationToken)
        {
            if(string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async(db,ct) => { await (context.NativeReferences.InsertBundleAsync(db,bundleId,payload,ct))
                .ConfigureAwait(false); return true; },cancellationToken))
                .ConfigureAwait(false);
                return;
            }
            await context.Use(nameof(SecuritiesDbCql.InsertOptionPricingReferenceBundle), SecuritiesDbCql.InsertOptionPricingReferenceBundle)
                .SetParameters(new InsertOptionPricingReferenceBundle(bundleId, payload))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
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
        /// Performs the EnsureDistinctFuturesContractWrites Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ProjectionState?> ReadProjectionStateAsync(
            IObjectRepository db,
            string projectionName,
            CancellationToken cancellationToken = default)
        {
            var states = await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionStateV3)}", SecuritiesDbCql.GetSecuritiesProjectionStateV3)
                .SetParameters(new GetSecuritiesProjectionStateV3(projectionName))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToProjectionState!, cancellationToken))
                .ConfigureAwait(false);
            return states.Count == 1 ? states.First() : null;
        }

        /// <summary>
        /// Performs the ReadSymbolProjectionStateAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ProjectionState?> ReadSymbolProjectionStateAsync(
            IObjectRepository db,
            string projectionName,
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var states = await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3)}", SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3)
                .SetParameters(new GetSecuritiesSymbolProjectionStateV3(projectionName, symbol))
                .ExecuteQueryAsync(static row => new ProjectionState(
                    row.GetGuid(0),
                    row.GetBool(1),
                    row.IsCollectionEmpty(2)), cancellationToken))
                .ConfigureAwait(false);
            return states.Count == 1 ? states.First() : null;
        }

        /// <summary>
        /// Performs the InvalidateGlobalProjectionStateAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task InvalidateGlobalProjectionStateAsync(
            IObjectRepository db,
            string projectionName,
            CancellationToken cancellationToken = default)
            => await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3)}", SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3)
                .SetParameters(new InvalidateSecuritiesProjectionStateV3(Guid.NewGuid(), projectionName))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);

        /// <summary>
        /// Performs the GetProjectionReadStampAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<TResult> ReadProjectionOrFallbackAsync<TResult>(
            IObjectRepository db,
            string projectionName,
            string symbol,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<TResult>> readProjection,
            Func<CancellationToken, Task<TResult>> readFallback)
        {
            if (string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
                return await readProjection(cancellationToken)
                .ConfigureAwait(false);

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<TResult> ReadProjectionOrFallbackAsync<TResult>(
            IObjectRepository db,
            string projectionName,
            string symbol,
            Func<Task<TResult>> readProjection,
            Func<Task<TResult>> readFallback)
        {
            if (string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
                return await readProjection()
                .ConfigureAwait(false);

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1854", Justification = "This assignment is observed in the catch path after a failed awaited write; it determines whether activation can safely be undone.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
                var states = await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesSymbolProjectionStatesV3)}", SecuritiesDbCql.GetSecuritiesSymbolProjectionStatesV3)
                    .SetParameters(new GetSecuritiesSymbolProjectionStatesV3(projectionName, symbolBatch))
                    .ExecuteQueryAsync(SecuritiesDbContext.MapToSymbolProjectionState!, cancellationToken))
                .ConfigureAwait(false);
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
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertSecuritiesProjectionOperationV3)}", SecuritiesDbCql.InsertSecuritiesProjectionOperationV3)
                    .SetParameters(new InsertSecuritiesProjectionOperationV3(
                        projectionName,
                        operationId,
                        DateTime.UtcNow))
                    .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertSecuritiesProjectionOperationScopeV3)}", SecuritiesDbCql.InsertSecuritiesProjectionOperationScopeV3)
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
                    .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
                activationResponseUnknown = true;
                var journalActivationApplied = await (db
                    .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)}", SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)
                    .SetParameters(new SetSecuritiesProjectionOperationStateMayBeActiveV3(
                        true,
                        projectionName,
                        operationId,
                        false))
                    .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!, cancellationToken))
                .ConfigureAwait(false);
                activationResponseUnknown = false;
                if (!(journalActivationApplied))
                {
                    throw new StorageException(
                        $"SecuritiesDb could not activate projection operation {operationId}; no data was changed.");
                }
                journalActivated = true;

                activationResponseUnknown = true;
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.BeginSecuritiesProjectionOperationV3)}", SecuritiesDbCql.BeginSecuritiesProjectionOperationV3)
                    .SetParameters(new BeginSecuritiesProjectionOperationV3(
                        operationId,
                        activeOperations,
                        projectionName))
                    .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
                activationResponseUnknown = false;
                if (symbols.Length > 0)
                {
                    activationResponseUnknown = true;
                    await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3)
                        .SetParameters(symbols.Select(symbol =>
                            new BeginSecuritiesSymbolProjectionOperationV3(
                                operationId,
                                activeOperations,
                                projectionName,
                                symbol)))
                        .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task EndProjectionOperationAsync(
            IObjectRepository db,
            ProjectionOperation operation,
            CancellationToken cancellationToken = default)
        {
            var endGeneration = Guid.NewGuid();
            var activeOperations = new HashSet<Guid> { operation.OperationId };
            if (operation.AffectedSymbols.Length > 0)
            {
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(operation.AffectedSymbols.Select(symbol =>
                        new EndSecuritiesSymbolProjectionOperationV3(
                            endGeneration,
                            activeOperations,
                            operation.ProjectionName,
                            symbol)))
                    .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
            }
            await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.EndSecuritiesProjectionOperationV3)}", SecuritiesDbCql.EndSecuritiesProjectionOperationV3)
                .SetParameters(new EndSecuritiesProjectionOperationV3(
                    endGeneration,
                    activeOperations,
                    operation.ProjectionName))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
            await context.DeleteProjectionOperationJournalAsync(db, operation, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the DeleteProjectionOperationJournalAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task DeleteProjectionOperationJournalAsync(
            IObjectRepository db,
            ProjectionOperation operation,
            CancellationToken cancellationToken = default)
        {
            // State is cleaned first by the caller. Move the journal to an inert phase before
            // deleting scopes so a crash between deletes is distinguishable from live work.
            await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)}", SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)
                .SetParameters(new SetSecuritiesProjectionOperationStateMayBeActiveV3(
                    false,
                    operation.ProjectionName,
                    operation.OperationId,
                    true))
                .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!))
                .ConfigureAwait(false);
            await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)
                .SetParameters(new DeleteSecuritiesProjectionOperationScopesV3(
                    operation.ProjectionName,
                    operation.OperationId))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
            await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)
                .SetParameters(new DeleteSecuritiesProjectionOperationV3(
                    operation.ProjectionName,
                    operation.OperationId))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the CompleteProjectionOperationAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
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

                var applied = await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(new CompleteSecuritiesSymbolProjectionOperationV3(
                        activeOperations,
                        operation.ProjectionName,
                        symbol,
                        operation.OperationId,
                        activeOperations))
                    .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!))
                .ConfigureAwait(false);
                if (applied)
                    continue;

                allCompleted = false;
                await EndSymbolOperationAsync(symbol)
                    .ConfigureAwait(false);
            }

            var shouldCompleteGlobal = completeGlobal || operation.GlobalWasComplete;
            if (shouldCompleteGlobal && (!completeGlobal || allCompleted))
            {
                var applied = await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.CompleteSecuritiesProjectionOperationV3)}", SecuritiesDbCql.CompleteSecuritiesProjectionOperationV3)
                    .SetParameters(new CompleteSecuritiesProjectionOperationV3(
                        activeOperations,
                        operation.ProjectionName,
                        operation.OperationId,
                        activeOperations))
                    .ExecuteSingleAsync(SecuritiesDbContext.MapToBoolean!))
                .ConfigureAwait(false);
                if (applied)
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

            await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.EndSecuritiesProjectionOperationV3)}", SecuritiesDbCql.EndSecuritiesProjectionOperationV3)
                .SetParameters(new EndSecuritiesProjectionOperationV3(
                    Guid.NewGuid(),
                    activeOperations,
                    operation.ProjectionName))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task ExecuteProjectionMutationAsync(
            IObjectRepository db,
            string projectionName,
            IEnumerable<string> affectedSymbols,
            Func<Task> mutation)
        {
            if (string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            {
                await mutation()
                .ConfigureAwait(false);
                return;
            }

            var operation = await (context.BeginProjectionOperationAsync(db, projectionName, affectedSymbols))
                .ConfigureAwait(false);
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task PopulateFuturesContractSymbolProjectionAsync(
            ICollection<FuturesContractV3ReadModel> contracts,
            CancellationToken cancellationToken = default)
        {
            if (contracts.Count == 0)
                return;

            await (context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContractBySymbolV3 : SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                .SetParameters(contracts.Select(contract => context.ToInsertParameters(contract)))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the PopulateFuturesOptionContractSymbolProjectionAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task PopulateFuturesOptionContractSymbolProjectionAsync(
            ICollection<FuturesOptionContractReadModel> contracts,
            CancellationToken cancellationToken = default)
        {
            if (contracts.Count == 0)
                return;

            await (context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesOptionContractBySymbolV2 : SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(contracts.Select(contract => context.ToInsertParameters(contract)))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the LoadFuturesContractProjectionAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<FuturesContractV3ReadModel[]> LoadFuturesContractProjectionAsync(
            string symbol,
            CancellationToken cancellationToken = default)
            => [.. (await (context.DbFactory.SecuritiesDb
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractsBySymbol)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesContractsBySymbol : SecuritiesDbCql.GetFuturesContractsBySymbol)
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
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsBySymbol)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesOptionContractsBySymbol : SecuritiesDbCql.GetFuturesOptionContractsBySymbol)
                .SetParameters(new GetFuturesOptionContractsBySymbol(symbol))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToFuturesOptionContract!, cancellationToken))
                .ConfigureAwait(false))];

        /// <summary>
        /// Performs the LoadAndPopulateFuturesContractsBySymbolAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<FuturesContractV3ReadModel[]> LoadAndPopulateFuturesContractsBySymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var db = context.DbFactory.SecuritiesDb;
            var operation = await (context.BeginProjectionOperationAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                [symbol],
                cancellationToken))
                .ConfigureAwait(false);
            var targetMutationSubmissionStarted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                targetMutationSubmissionStarted = true;
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3Partition)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3Partition : SecuritiesDbCql.DeleteFuturesContractBySymbolV3Partition)
                    .SetParameters(new DeleteFuturesContractBySymbolV3Partition(symbol))
                    .ExecuteCommandAsync(CancellationToken.None))
                .ConfigureAwait(false);

                var matchingContracts = new List<FuturesContractV3ReadModel>();
                await foreach (var contract in (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContracts)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesContracts : SecuritiesDbCql.GetFuturesContracts)
                    .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesContract!, CancellationToken.None))
                .ConfigureAwait(false))
                {
                    if (string.Equals(contract.Symbol, symbol, StringComparison.Ordinal))
                        matchingContracts.Add(contract);
                }
                var contracts = matchingContracts
                    .OrderByDescending(contract => contract.OnTheRun)
                    .ThenByDescending(contract => contract.LastTradeDate)
                    .ThenBy(contract => contract.ContractId, StringComparer.Ordinal)
                    .ToArray();
                await (context.PopulateFuturesContractSymbolProjectionAsync(contracts, CancellationToken.None))
                .ConfigureAwait(false);

                var projectedContracts = await (context.LoadFuturesContractProjectionAsync(symbol, CancellationToken.None))
                .ConfigureAwait(false);
                if (!context.HasExactFuturesContractKeys(contracts, projectedContracts))
                {
                    throw new StorageException(
                        $"SecuritiesDb could not reconcile the '{symbol}' futures-contract symbol projection; completion was not recorded.");
                }

                await (context.CompleteProjectionOperationAsync(
                    db,
                    operation,
                    completeGlobal: false,
                    completeAllSymbols: true,
                    cancellationToken: CancellationToken.None))
                .ConfigureAwait(false);
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "MA0051", Justification = "Native and transitional provider paths implement one compatibility operation; retain its transaction and failure ordering during migration.")]
        internal async Task<FuturesOptionContractReadModel[]> LoadAndPopulateFuturesOptionContractsBySymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var db = context.DbFactory.SecuritiesDb;
            var operation = await (context.BeginProjectionOperationAsync(
                db,
                SecuritiesDbContext.FuturesOptionContractSymbolProjection,
                [symbol],
                cancellationToken))
                .ConfigureAwait(false);
            var targetMutationSubmissionStarted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                targetMutationSubmissionStarted = true;
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesOptionContractBySymbolV2Partition : SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)
                    .SetParameters(new DeleteFuturesOptionContractBySymbolV2Partition(symbol))
                    .ExecuteCommandAsync(CancellationToken.None))
                .ConfigureAwait(false);

                var matchingContracts = new List<FuturesOptionContractReadModel>();
                await foreach (var contract in (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContracts)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.GetFuturesOptionContracts : SecuritiesDbCql.GetFuturesOptionContracts)
                    .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesOptionContract!, CancellationToken.None))
                .ConfigureAwait(false))
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
                await (context.PopulateFuturesOptionContractSymbolProjectionAsync(contracts, CancellationToken.None))
                .ConfigureAwait(false);

                var projectedContracts = await (context.LoadFuturesOptionContractProjectionAsync(symbol, CancellationToken.None))
                .ConfigureAwait(false);
                if (!context.HasExactFuturesOptionContractKeys(contracts, projectedContracts))
                {
                    throw new StorageException(
                        $"SecuritiesDb could not reconcile the '{symbol}' futures-option symbol projection; completion was not recorded.");
                }

                await (context.CompleteProjectionOperationAsync(
                    db,
                    operation,
                    completeGlobal: false,
                    completeAllSymbols: true,
                    cancellationToken: CancellationToken.None))
                .ConfigureAwait(false);
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool HasExactFuturesContractKeys(
            IReadOnlyCollection<FuturesContractV3ReadModel> source,
            IReadOnlyCollection<FuturesContractV3ReadModel> target)
            => source.Count == target.Count
                && source.Select(contract => context.ToProjectionKey(contract)).ToHashSet()
                    .SetEquals(target.Select(contract => context.ToProjectionKey(contract)));

        /// <summary>
        /// Performs the HasExactFuturesOptionContractKeys Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool HasExactFuturesOptionContractKeys(
            IReadOnlyCollection<FuturesOptionContractReadModel> source,
            IReadOnlyCollection<FuturesOptionContractReadModel> target)
            => source.Count == target.Count
                && source.Select(contract => context.ToProjectionKey(contract)).ToHashSet()
                    .SetEquals(target.Select(contract => context.ToProjectionKey(contract)));

        /// <summary>
        /// Performs the RecoverVerifiedInactiveProjectionOperationsAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task RecoverVerifiedInactiveProjectionOperationsAsync(
            IObjectRepository db,
            string projectionName,
            DateTime staleOperationCutoffUtc,
            CancellationToken cancellationToken)
        {
            var journalEntries = await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)}", SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)
                .SetParameters(new GetSecuritiesProjectionOperationsV3(projectionName))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToProjectionOperationJournalEntry!))
                .ConfigureAwait(false);
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

                var scopes = await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.GetSecuritiesProjectionOperationScopesV3)
                    .SetParameters(new GetSecuritiesProjectionOperationScopesV3(
                        projectionName,
                        entry.OperationId))
                    .ExecuteQueryAsync(SecuritiesDbContext.MapToProjectionOperationScope!))
                .ConfigureAwait(false);
                var hasGlobalScope = false;
                var stateScopeCount = 0;
                int? expectedStateScopeCount = null;
                foreach (var scope in scopes)
                {
                    if (string.Equals(scope.ScopeType,SecuritiesDbContext.ProjectionOperationScopeCount,StringComparison.Ordinal))
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

                    if (string.Equals(scope.ScopeType,SecuritiesDbContext.GlobalProjectionOperationScope,StringComparison.Ordinal) &&
                        string.Equals(scope.ScopeKey,projectionName,StringComparison.Ordinal))
                    {
                        hasGlobalScope = true;
                        stateScopeCount++;
                        globalOperationIds.Add(entry.OperationId);
                        continue;
                    }

                    if (!string.Equals(scope.ScopeType,SecuritiesDbContext.SymbolProjectionOperationScope,StringComparison.Ordinal) ||
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
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)}", SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)
                    .SetParameters(globalOperationIds.Select(operationId =>
                        new RemoveSecuritiesProjectionOperationV3(
                            operationId,
                            projectionName)))
                    .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
            }
            if (operationIdsBySymbol.Count > 0)
            {
                await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(operationIdsBySymbol.SelectMany(entry =>
                        entry.Value.Select(operationId =>
                            new RemoveSecuritiesSymbolProjectionOperationV3(
                                operationId,
                                projectionName,
                                entry.Key))))
                    .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
            }

            // Delete exact journal partitions only after every recorded state scope has been
            // cleaned. Any interruption leaves the catalog entry available for an idempotent retry.
            await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)
                .SetParameters(staleEntries.Select(entry =>
                    new DeleteSecuritiesProjectionOperationScopesV3(
                        projectionName,
                        entry.OperationId)))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);
            await (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)
                .SetParameters(staleEntries.Select(entry =>
                    new DeleteSecuritiesProjectionOperationV3(
                        projectionName,
                        entry.OperationId)))
                .ExecuteCommandAsync(cancellationToken))
                .ConfigureAwait(false);

            static DateTime AsUtc(DateTime value)
                => value.Kind == DateTimeKind.Utc
                    ? value
                    : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

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

            await foreach (var key in (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractProjectionSourceKeys)}", SecuritiesDbCql.GetFuturesContractProjectionSourceKeys)
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
            await foreach (var key in (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesContractProjectionTargetKeys)}", SecuritiesDbCql.GetFuturesContractProjectionTargetKeys)
                .ExecuteStreamAsync(SecuritiesDbContext.MapToFuturesContractProjectionKey!, cancellationToken))
                .ConfigureAwait(false))
            {
                futuresContractProjectionRows++;
                futuresContractProjectionKeys.Add(key);
            }
            await foreach (var key in (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractProjectionSourceKeys)}", SecuritiesDbCql.GetFuturesOptionContractProjectionSourceKeys)
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
            await foreach (var key in (db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractProjectionTargetKeys)}", SecuritiesDbCql.GetFuturesOptionContractProjectionTargetKeys)
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
        /// Performs the ReplaceFuturesRolloverSetCoreAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
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
                || (string.Equals(symbol,"ES",StringComparison.Ordinal) && replacements.Length != 1)
                || (string.Equals(symbol,"VX",StringComparison.Ordinal) && replacements.Length != 2)
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

            var existing = await (context.GetFuturesRolloverSetAsync(symbol, cancellationToken))
                .ConfigureAwait(false);
            var replacementKeys = replacements
                .Select(static contract => (contract.ContractId, contract.LastTradeDate))
                .ToHashSet();
            var db = context.DbFactory.SecuritiesDb;
            List<object> queuedCommands = [];
            foreach (var current in existing)
            {
                if (replacementKeys.Contains((current.ContractId, current.LastTradeDate)))
                    continue;
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3 : SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                    .SetParameters(new DeleteFuturesContractBySymbolV3(
                        current.Symbol, current.Rollover, current.OnTheRun,
                        current.LastTradeDate, current.ContractId))
                    .QueueCommand());
                var retired = current with { OnTheRun = false, Rollover = false };
                var retiredInsert = context.ToInsertParameters(retired);
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContract : SecuritiesDbCql.InsertFuturesContract)
                    .SetParameters(retiredInsert)
                    .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContractBySymbolV3 : SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                    .SetParameters(retiredInsert)
                    .QueueCommand());
            }

            foreach (var contract in orderedReplacements)
            {
                var insert = context.ToInsertParameters(contract);
                queuedCommands.AddRange(context.OtherProjectionKeys(contract).Select(key =>
                    db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.DeleteFuturesContractBySymbolV3 : SecuritiesDbCql.DeleteFuturesContractBySymbolV3)
                        .SetParameters(new DeleteFuturesContractBySymbolV3(
                            key.Symbol, key.Rollover, key.OnTheRun,
                            key.LastTradeDate, key.ContractId))
                        .QueueCommand()));
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContract)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContract : SecuritiesDbCql.InsertFuturesContract)
                    .SetParameters(insert)
                    .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertFuturesContractBySymbolV3)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.InsertFuturesContractBySymbolV3 : SecuritiesDbCql.InsertFuturesContractBySymbolV3)
                    .SetParameters(insert)
                    .QueueCommand());
            }
            queuedCommands.Add(db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.UpdateFuturesContractRollover)}", string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? SecuritiesDbSql.UpdateFuturesContractRollover : SecuritiesDbCql.UpdateFuturesContractRollover)
                .SetParameters(new UpdateFuturesContractRollover(
                    rollover.ContractId,
                    rollover.NextRolloverDate.Value,
                    rollover.UpdatedOn.Value,
                    rollover.UpdatedBy,
                    symbol))
                .QueueCommand());

            await (context.ExecuteProjectionMutationAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                [symbol],
                () => db.ExecuteQueuedCommandsAsync(queuedCommands, true)))
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var persistedSet = (await (context.GetFuturesRolloverSetAsync(symbol, cancellationToken))
                .ConfigureAwait(false))
                .OrderBy(static contract => contract.LastTradeDate)
                .ThenBy(static contract => contract.ContractId, StringComparer.Ordinal)
                .ToArray();
            var persistedPointer = await (context.GetFuturesContractRolloverAsync(symbol, cancellationToken))
                .ConfigureAwait(false);
            if (persistedSet.Length != orderedReplacements.Length
                || !persistedSet.Select(static contract => (
                        contract.ContractId, contract.OnTheRun, contract.Rollover))
                    .SequenceEqual(orderedReplacements.Select(static contract => (
                        contract.ContractId, contract.OnTheRun, contract.Rollover)))
                || !string.Equals(persistedPointer?.ContractId,rollover.ContractId,StringComparison.Ordinal)
                || persistedPointer.NextRolloverDate != rollover.NextRolloverDate)
            {
                throw new StorageException(
                    $"The '{symbol}' futures rollover set did not pass durable post-write verification.");
            }
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
                && await (context.GetReferenceVersionAsync(underlying.ContractId, underlying.MappingVersion!))
                .ConfigureAwait(false) is null)
                throw new InvalidOperationException("The underlying future's reviewed version has not completed publication.");
            return await (context.StageReferenceVersionAsync(value))
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
            return await (context.StageReferenceVersionAsync(value))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the CommitReferenceAsync Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task CommitReferenceAsync(PendingReferenceVersion? value) =>
            value is null ? Task.CompletedTask : context.CommitReferenceVersionAsync(value);

        /// <summary>
        /// Performs the EncodeOptionCursor Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string EncodeOptionCursor(OptionPageCursor cursor)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(cursor);
            var signature = HMACSHA256.HashData(OptionPageSigningKey, payload);
            return Convert.ToBase64String(payload) + "." + Convert.ToBase64String(signature);
        }

        /// <summary>
        /// Performs the DecodeOptionCursor Securities persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
                if (cursor is null || !string.Equals(cursor.Symbol,request.Symbol,StringComparison.Ordinal) || cursor.PageSize != request.PageSize || cursor.State.Length == 0)
                    throw new FormatException();
                return cursor;
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                throw new ArgumentException("Invalid or expired contract continuation token. Refresh the list to restart paging.", nameof(request), ex);
            }
        }

        /// <summary>Validates projection backfill control parameters.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string ValidateAndNormalizeSymbol(string symbol)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            return symbol.Trim().ToUpperInvariant();
        }

        /// <summary>Validates a futures rollover assignment.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidateRollover(FuturesContractRolloverReadModel rollover)
            => ArgumentNullException.ThrowIfNull(rollover);

        /// <summary>Validates an option-contract paging request.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidatePagingRequest(GetFuturesOptionContractsPageParameter request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();
        }

        /// <summary>Validates an option expiry range and returns its normalized symbol.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string ValidateAndNormalizeExpiryRange(string symbol, DateOnly fromExpiry, DateOnly throughExpiry)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            ArgumentOutOfRangeException.ThrowIfLessThan(throughExpiry, fromExpiry);
            return symbol.Trim().ToUpperInvariant();
        }

        /// <summary>Validates a cached option-definition query and returns its normalized symbol.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string ValidateAndNormalizeCachedDefinitionQuery(string symbol, string underlyingContractId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            ArgumentException.ThrowIfNullOrWhiteSpace(underlyingContractId);
            return symbol.Trim().ToUpperInvariant();
        }

        /// <summary>Validates cached option definitions and returns their normalized symbol.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string ValidateAndNormalizeOptionDefinitions(string symbol, DateOnly coverageFrom, DateOnly coverageThrough, IReadOnlyCollection<CachedOptionContractDefinitionReadModel> definitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
            ArgumentNullException.ThrowIfNull(definitions);
            ArgumentOutOfRangeException.ThrowIfLessThan(coverageThrough, coverageFrom);
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
            CancellationToken cancellationToken,
            TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction? enlisted = null)
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
                cancellationToken,enlisted);
        }

        /// <summary>Stages an immutable futures-option reference version and reserves its identities.</summary>
        internal Task<PendingReferenceVersion> StageReferenceVersionCoreAsync(
            FuturesOptionContractReadModel value,
            CancellationToken cancellationToken,
            TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction? enlisted = null)
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
                cancellationToken,enlisted);
        }

        /// <summary>Stages the serialized immutable reference-version envelope.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<PendingReferenceVersion> StageReferenceVersionCoreAsync(
            string contractId,
            string? dataset,
            ushort? publisherId,
            uint? instrumentId,
            string? version,
            ReferenceReviewState reviewState,
            string identity,
            ReferenceContractVersion value,
            CancellationToken cancellationToken,
            TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction? enlisted = null)
        {
            if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128
                || string.IsNullOrWhiteSpace(dataset) || dataset.Length > 32
                || publisherId is null or 0 || instrumentId is null or 0)
                throw new ArgumentException("An exact bounded IFM/provider identity is required.",nameof(contractId));
            if (MessagePackBinarySerializer.MeasureContent(value) > 131072)
                throw new ArgumentException("Reference version is oversized.",nameof(value));
            var digest = value.CalculateReferenceVersionDigest();
            version = reviewState == ReferenceReviewState.Reviewed ? version : "draft/" + digest;
            if (string.IsNullOrWhiteSpace(version) || version.Length > 128)
                throw new ArgumentException("A bounded mapping version is required.",nameof(version));

            if(string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            {
                if (enlisted is not null)
                {
                    await context.NativeReferences.StageAsync(enlisted,contractId,
                        string.Create(System.Globalization.CultureInfo.InvariantCulture,$"provider:{dataset}:{publisherId}:{instrumentId}"),
                        identity,version,digest,MessagePackBinarySerializer.Shared.Serialize(value)!,cancellationToken)
                .ConfigureAwait(false);
                    return new(contractId,version,digest);
                }
                await (context.NativeTransaction().ExecuteOnceAsync(async(db,ct) =>
                {
                    await (context.NativeReferences.StageAsync(db,contractId,
                        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"provider:{dataset}:{publisherId}:{instrumentId}"),identity,
                        version,digest,MessagePackBinarySerializer.Shared.Serialize(value)!,ct))
                .ConfigureAwait(false);
                    return true;
                },cancellationToken))
                .ConfigureAwait(false);
                return new(contractId,version,digest);
            }
            await context.ClaimReferenceIdentityAsync("ifm:" + contractId, identity, cancellationToken)
                .ConfigureAwait(false);
            await context.ClaimReferenceIdentityAsync(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"provider:{dataset}:{publisherId}:{instrumentId}"),
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
            if (stored is null || !string.Equals(stored.Value.Digest,digest,StringComparison.Ordinal))
                throw new InvalidOperationException("Mapping version already contains different content. Publish a new reviewed version.");
            return new(contractId, version, digest);
        }

        /// <summary>Claims an immutable IFM or provider identity binding.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
            if (!string.Equals(bindings.SingleOrDefault(),binding,StringComparison.Ordinal))
                throw new InvalidOperationException("Provider/IFM identity collision. Existing contract identities cannot be overwritten.");
        }

        /// <summary>Publishes a previously staged immutable reference version.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task CommitReferenceVersionCoreAsync(
            PendingReferenceVersion value,
            CancellationToken cancellationToken,
            TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction? enlisted = null)
        {
            if(string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
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
            await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.CommitReferenceVersion)}", SecuritiesDbCql.CommitReferenceVersion)
                .SetParameters(new CommitReferenceVersion(value.ContractId, value.Version, value.Digest))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            var stored = await context.ReadReferenceVersionAsync(value.ContractId, value.Version, cancellationToken)
                .ConfigureAwait(false);
            if (stored is null || !stored.Value.Published || !string.Equals(stored.Value.Digest,value.Digest,StringComparison.Ordinal))
                throw new InvalidOperationException("Reference publication did not complete.");
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
            if (row is null || !row.Value.Published) return null;
            var value = row.Value.Value;
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

        /// <summary>Gets a published reference version effective at a UTC instant.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsCoreAsync(
            string contractId,
            string afterVersion,
            int limit,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128
                || afterVersion.Length > 128 || limit is < 1 or > 200)
                throw new ArgumentException("Invalid bounded version-history query.",nameof(contractId));
            if(string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
                return await (context.NativeTransaction().ExecuteOnceAsync((db,ct) => db.QueryAsync(
                    "SELECT version,published FROM securities.securities_reference_version WHERE contract_id=$1 AND version>$2 ORDER BY version COLLATE \"C\" LIMIT $3;",
                    [contractId,afterVersion,limit],r=>new ReferenceContractVersionSummaryReadModel(r.GetString(0),r.GetBoolean(1)),ct),cancellationToken))
                .ConfigureAwait(false);
            return (await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetReferenceVersionHistory)}", SecuritiesDbCql.GetReferenceVersionHistory)
                .SetParameters(new GetReferenceVersionHistory(contractId, afterVersion, limit))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToReferenceVersionSummary!, cancellationToken)
                .ConfigureAwait(false)).ToArray();
        }

        /// <summary>Reads and verifies one staged or published reference-version row.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ReferenceVersionStorageRow?> ReadReferenceVersionAsync(
            string contractId,
            string version,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128
                || string.IsNullOrWhiteSpace(version) || version.Length > 128)
                throw new ArgumentException("Exact reference version is required.",nameof(contractId));
            if(string.Equals(context.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            {
                var stored = await (context.NativeTransaction().ExecuteOnceAsync((db,ct) => db.QueryAsync(
                    "SELECT digest,published,payload FROM securities.securities_reference_version WHERE contract_id=$1 AND version=$2;",
                    [contractId,version],r=>(Digest:r.GetString(0),Published:r.GetBoolean(1),Payload:r.GetFieldValue<byte[]>(2)),ct),cancellationToken))
                .ConfigureAwait(false);
                if(stored.Count==0) return null;
                var item=stored.Single();
                if(item.Payload.Length is 0 or > 131072) throw new InvalidDataException("Reference version payload exceeds its size bound.");
                var value=MessagePackBinarySerializer.Shared.Deserialize<ReferenceContractVersion>(item.Payload)
                    ?? throw new InvalidDataException("Reference version payload is missing.");
                if(!string.Equals(value.CalculateReferenceVersionDigest(),item.Digest,StringComparison.Ordinal)) throw new InvalidDataException("Reference version checksum mismatch.");
                return new(item.Digest,item.Published,value);
            }
            var rows = (await context.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetReferenceVersion)}", SecuritiesDbCql.GetReferenceVersion)
                .SetParameters(new GetReferenceVersion(contractId, version))
                .ExecuteQueryAsync(SecuritiesDbContext.MapToReferenceVersionStorageRow!, cancellationToken)
                .ConfigureAwait(false)).ToArray();
            if (rows.Length == 0) return null;
            var row = rows.Single();
            if (!string.Equals(row.Value.CalculateReferenceVersionDigest(),row.Digest,StringComparison.Ordinal))
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string CalculateReferenceVersionDigest()
            => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    }
}
