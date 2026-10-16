using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using System.Collections.Immutable;
using Npgsql;
using MessagePack;




using static TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbContext;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;



using TomasAI.IFM.Application.Storage.EventSourceDb;

using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;





using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;


using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;

using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;




using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
public static class MarketDataDbContextExtensions
{
    extension(ImportDuplicatePolicy duplicatePolicy)
    {
        /// <summary>
        /// Performs the <c>ValidateImportPolicy</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="commandId">The originating command identifier.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidateImportPolicy(Guid commandId)
        {
            if (!Enum.IsDefined(duplicatePolicy))
                throw new ArgumentOutOfRangeException(nameof(duplicatePolicy));
            if (duplicatePolicy == ImportDuplicatePolicy.Reject && commandId == Guid.Empty)
                throw new ArgumentException("Reject imports require a durable command identity.", nameof(commandId));
        }
    }

    extension(MarketDataDbContext context)
    {
        /// <summary>Read Stored Futures Eod Data using the MarketDataDb persistence.</summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the ReadStoredFuturesEodDataAsync persistence helper.</summary>
        internal async Task<FuturesEodDataV2ReadModel?> ReadStoredFuturesEodDataAsync(string contractId, DateOnly valueDate)
        {
            var db = context._dbFactory.MarketDataDb;
            var futuresEodData = await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodData)}", MarketDataDbSql.GetFuturesEodData)
                .SetParameters(new GetFuturesEodData(contractId, valueDate) { PostgreSql = true })
                .ExecuteSingleAsync(MarketDataDbContext.MapToFuturesEodData!))
                .ConfigureAwait(false);
            futuresEodData ??= await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYesterdaysFuturesEodData)}", MarketDataDbSql.GetYesterdaysFuturesEodData)
                .SetParameters(new GetYesterdaysFuturesEodData(contractId, valueDate) { PostgreSql = true })
                .ExecuteSingleAsync(MarketDataDbContext.MapToFuturesEodData!))
                .ConfigureAwait(false);
            return futuresEodData;
        }

        /// <summary>Read Stored Vix Futures Eod Data using the MarketDataDb persistence.</summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the ReadStoredVixFuturesEodDataAsync persistence helper.</summary>
        internal async Task<VixFuturesEodDataReadModel?> ReadStoredVixFuturesEodDataAsync(string contractId, DateOnly valueDate) => await (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodData)}", MarketDataDbSql.GetVixFuturesEodData)
                .SetParameters(new GetVixFuturesEodData(contractId, valueDate) { PostgreSql = true })
                .ExecuteSingleAsync(MarketDataDbContext.MapToVixFuturesEodData))
                .ConfigureAwait(false);

        /// <summary>
        /// Performs the <c>EnsureImportOwnershipAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="dataset">The dataset used by this operation.</param>
        /// <param name="logicalKey">The logical key used by this operation.</param>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="logicalRowAlreadyExists">The logical row already exists used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task EnsureImportOwnershipAsync(string dataset, string logicalKey, Guid commandId, bool logicalRowAlreadyExists)
        {
            var db = context._dbFactory.MarketDataDb;
            var applied = await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.ClaimMarketDataImportOwnership)}", MarketDataDbCql.ClaimMarketDataImportOwnership)
                .SetParameters(new ClaimMarketDataImportOwnership(dataset, logicalKey, commandId, !logicalRowAlreadyExists, DateTime.UtcNow))
                .ExecuteScalarAsync(MapToBoolean!))
                .ConfigureAwait(false);
            if (applied && !logicalRowAlreadyExists)
                return;
            var owner = await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataImportOwnership)}", MarketDataDbCql.GetMarketDataImportOwnership)
                .SetParameters(new GetMarketDataImportOwnership(dataset, logicalKey))
                .ExecuteSingleAsync(MapToMarketDataImportOwnership!))
                .ConfigureAwait(false);
            if (owner is { CommandId: var ownerCommandId, MayWrite: true } && ownerCommandId == commandId)
                return;
            throw new MarketDataImportDuplicateException($"A {dataset} row with logical key '{logicalKey}' is already owned by another import command.");
        }

        /// <summary>
        /// Performs the <c>GetProjectionGuardScopeKeys</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string[] GetProjectionGuardScopeKeys() => Enumerable.Range(0, MarketDataDbContext.ProjectionGuardScopeCount).Select(bucket => string.Concat(MarketDataDbContext.ProjectionGuardScopePrefix, bucket.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        /// <summary>
        /// Performs the <c>GetProjectionStateAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<MarketDataProjectionStateReadModel?> GetProjectionStateAsync(string projectionName) => await (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionState)}", MarketDataDbCql.GetMarketDataProjectionState)
                .SetParameters(new GetMarketDataProjectionState(projectionName)).ExecuteSingleAsync<MarketDataProjectionStateReadModel?>(static row => MarketDataDbContext.MapToProjectionState(row)))
                .ConfigureAwait(false);
        /// <summary>
        /// Performs the <c>HasProjectionMutationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<bool> HasProjectionMutationAsync(string projectionName) => (await (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMutation)}", MarketDataDbCql.GetMarketDataProjectionMutation)
                .SetParameters(new GetMarketDataProjectionMutation(projectionName))
                .ExecuteQueryAsync(MarketDataDbContext.MapToGuid))
                .ConfigureAwait(false)).Count != 0;
        /// <summary>
        /// Performs the <c>GetProjectionReadGenerationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<Guid?> GetProjectionReadGenerationAsync(string projectionName)
        {
            var state = await (context.GetProjectionStateAsync(projectionName))
                .ConfigureAwait(false);
            if (state is null || !state.Value.IsReady || await (context.HasProjectionMutationAsync(projectionName))
                .ConfigureAwait(false))
                return null;
            return state.Value.Generation;
        }

        /// <summary>
        /// Performs the <c>IsProjectionReadGenerationValidAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <param name="generation">The generation used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<bool> IsProjectionReadGenerationValidAsync(string projectionName, Guid generation)
        {
            var state = await (context.GetProjectionStateAsync(projectionName))
                .ConfigureAwait(false);
            return state is { IsReady: true } && state.Value.Generation == generation && !await (context.HasProjectionMutationAsync(projectionName))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>GetProjectionScopeStatesAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <param name="scopeKeys">The scope keys used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<Dictionary<string, MarketDataProjectionScopeStateReadModel?>> GetProjectionScopeStatesAsync(string projectionName, IReadOnlyList<string> scopeKeys)
        {
            var states = new Dictionary<string, MarketDataProjectionScopeStateReadModel?>(scopeKeys.Count, StringComparer.Ordinal);
            for (var offset = 0; offset < scopeKeys.Count; offset += MarketDataDbContext.ProjectionScopeStateReadBatchSize)
            {
                var count = Math.Min(MarketDataDbContext.ProjectionScopeStateReadBatchSize, scopeKeys.Count - offset);
                var keys = scopeKeys.Skip(offset).Take(count).ToArray();
                foreach (var key in keys)
                    states.Add(key, null);
                var values = await (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)}", MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
                .SetParameters(new GetMarketDataProjectionScopeStatesV3(projectionName, keys))
                .ExecuteQueryAsync(MarketDataDbContext.MapToProjectionScopeState))
                .ConfigureAwait(false);
                foreach (var value in values)
                    states[value.ScopeKey] = value;
            }

            return states;
        }

        /// <summary>
        /// Performs the <c>GetProjectionScopeReadStampAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <param name="scopeKeys">The scope keys used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<MarketDataProjectionScopeReadStamp?> GetProjectionScopeReadStampAsync(string projectionName, IEnumerable<string> scopeKeys)
        {
            var scopes = MarketDataDbContextExtensions.AddProjectionGuardScopes(scopeKeys);
            var globalGeneration = await (context.GetProjectionReadGenerationAsync(projectionName))
                .ConfigureAwait(false);
            if (globalGeneration is null)
                return null;
            var states = await (context.GetProjectionScopeStatesAsync(projectionName, scopes))
                .ConfigureAwait(false);
            var generations = new MarketDataProjectionScopeGeneration[scopes.Length];
            for (var index = 0; index < scopes.Length; index++)
            {
                var state = states[scopes[index]];
                if (state is null)
                {
                    // A globally ready projection plus a stable ready guard is a valid
                    // negative cache entry. Any writer that creates this data scope also
                    // changes its guard, and validation below rejects an appeared scope.
                    if (MarketDataDbContextExtensions.IsProjectionGuardScopeKey(scopes[index]))
                        return null;
                    generations[index] = new(scopes[index], Guid.Empty, IsMissing: true);
                    continue;
                }

                if (!state.Value.CanRead)
                    return null;
                generations[index] = new(scopes[index], state.Value.Generation, IsMissing: false);
            }

            return new(projectionName, globalGeneration.Value, generations);
        }

        /// <summary>
        /// Performs the <c>IsProjectionScopeReadStampValidAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="stamp">The stamp used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<bool> IsProjectionScopeReadStampValidAsync(MarketDataProjectionScopeReadStamp stamp)
        {
            if (!await (context.IsProjectionReadGenerationValidAsync(stamp.ProjectionName, stamp.GlobalGeneration))
                .ConfigureAwait(false))
            {
                return false;
            }

            var states = await (context.GetProjectionScopeStatesAsync(stamp.ProjectionName, stamp.Scopes.Select(static scope => scope.ScopeKey).ToArray()))
                .ConfigureAwait(false);
            foreach (var scope in stamp.Scopes)
            {
                var state = states[scope.ScopeKey];
                if (scope.IsMissing)
                {
                    if (state is not null)
                        return false;
                    continue;
                }

                if (state is null || !state.Value.CanRead || state.Value.Generation != scope.Generation)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Performs the <c>ExecuteMaintainedProjectionMutationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <param name="scopeKeys">The scope keys used by this operation.</param>
        /// <param name="mutation">The mutation used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        internal async Task ExecuteMaintainedProjectionMutationAsync(string projectionName, IEnumerable<string> scopeKeys, Func<Task> mutation)
        {
            var scopes = MarketDataDbContextExtensions.AddProjectionGuardScopes(scopeKeys);
            if (scopes.Length == 0)
            {
                await (mutation())
                .ConfigureAwait(false);
                return;
            }

            var globalGeneration = await (context.GetProjectionReadGenerationAsync(projectionName))
                .ConfigureAwait(false);
            var initialStates = await (context.GetProjectionScopeStatesAsync(projectionName, scopes))
                .ConfigureAwait(false);
            var restorableScopes = globalGeneration is null ? new HashSet<string>(StringComparer.Ordinal) : scopes.Where(scope => initialStates[scope] is null || initialStates[scope]!.Value.CanRead).ToHashSet(StringComparer.Ordinal);
            var mutationId = Guid.NewGuid();
            var activeOperations = new HashSet<Guid>
            {
                mutationId
            };
            var db = context._dbFactory.MarketDataDb;
            var scopeActivationAcknowledged = false;
            var mutationSubmissionStarted = false;
            await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                .SetParameters(scopes.Select(scope => new InsertMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId, DateTime.UtcNow)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            try
            {
                async Task ActivateScopesAsync() => await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)
                .SetParameters(scopes.Select(scope => new BeginMarketDataProjectionScopeOperationV3(projectionName, scope, mutationId, activeOperations)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                if (context.MaintainedProjectionScopeActivationForTestingAsync is { } scopeActivation)
                    await (scopeActivation(ActivateScopesAsync))
                .ConfigureAwait(false);
                else
                    await (ActivateScopesAsync())
                .ConfigureAwait(false);
                scopeActivationAcknowledged = true;
                mutationSubmissionStarted = true;
                if (context.MaintainedProjectionMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await (mutationSubmitting())
                .ConfigureAwait(false);
                await (mutation())
                .ConfigureAwait(false);
                var globalStillValid = globalGeneration.HasValue && await (context.IsProjectionReadGenerationValidAsync(projectionName, globalGeneration.Value))
                .ConfigureAwait(false);
                var scopesToEnd = new List<string>();
                foreach (var scopeBatch in scopes.Chunk(MarketDataDbContext.ProjectionReadConcurrency))
                {
                    var completions = scopeBatch.Select(async scope =>
                    {
                        if (!globalStillValid || !restorableScopes.Contains(scope))
                            return (Scope: scope, Completed: false);
                        var completed = await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)
                .SetParameters(new CompleteMarketDataProjectionScopeOperationV3(projectionName, scope, mutationId, activeOperations, DateTime.UtcNow, activeOperations))
                .ExecuteSingleAsync(MarketDataDbContext.MapToBoolean))
                .ConfigureAwait(false);
                        return (Scope: scope, Completed: completed);
                    }).ToArray();
                    foreach (var completion in await (Task.WhenAll(completions))
                .ConfigureAwait(false))
                    {
                        if (!completion.Completed)
                            scopesToEnd.Add(completion.Scope);
                    }
                }

                await (MarketDataDbContextExtensions.EndProjectionScopeOperationsAsync(db, projectionName, scopesToEnd, activeOperations))
                .ConfigureAwait(false);
                await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                .SetParameters(scopes.Select(scope => new DeleteMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            }
            catch
            {
                if (!scopeActivationAcknowledged || mutationSubmissionStarted)
                {
                    // A Begin or target mutation may have reached Scylla even when its
                    // response is a timeout. Keep the original nonfailed journals and
                    // active data/guard IDs so only an explicit cutoff after writers are
                    // drained can recover without racing delayed server-side application.
                    throw;
                }

                try
                {
                    // A definitively acknowledged Begin can be classified without issuing
                    // a racing End. The active ID remains paired with its failed journal
                    // for exact removal by the next repair.
                    await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)
                .SetParameters(scopes.Select(scope => new FailMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId, DateTime.UnixEpoch)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                }
                catch
                {
                // An unclassified in-flight marker is never cleared automatically.
                }

                throw;
            }
        }

        /// <summary>
        /// Performs the <c>ExecuteAtomicTickWriteAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="scopeKey">The scope key used by this operation.</param>
        /// <param name="canonicalRows">The canonical rows used by this operation.</param>
        /// <param name="projectionRows">The projection rows used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task ExecuteAtomicTickWriteAsync(string scopeKey, IReadOnlyCollection<InsertFuturesTickData> canonicalRows, IReadOnlyCollection<InsertFuturesTickDataByTime> projectionRows)
        {
            if (canonicalRows.Count == 0)
                return;
            if (canonicalRows.Count != projectionRows.Count || canonicalRows.Count > MarketDataDbContext.TickAtomicBatchRowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(canonicalRows), canonicalRows.Count, $"Atomic tick writes require matching collections of at most {MarketDataDbContext.TickAtomicBatchRowCount} rows.");
            }

            await (context.ExecuteGuardedAtomicTickMutationAsync(scopeKey, db => [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickData)}", MarketDataDbCql.InsertFuturesTickData)
                .SetParameters(canonicalRows)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickDataByTime)}", MarketDataDbCql.InsertFuturesTickDataByTime)
                .SetParameters(projectionRows)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)}", MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)
                .SetParameters(new MarkMarketDataProjectionScopeAtomicWriteV3(MarketDataDbContext.FuturesTickByTimeProjection, scopeKey, Guid.NewGuid()))
                .QueueCommand(), ]))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ExecuteGuardedAtomicTickMutationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="scopeKey">The scope key used by this operation.</param>
        /// <param name="createDataCommands">The create data commands used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        internal async Task ExecuteGuardedAtomicTickMutationAsync(string scopeKey, Func<IObjectRepository, List<object>> createDataCommands)
        {
            var db = context._dbFactory.MarketDataDb;
            var guardScopeKey = MarketDataDbContextExtensions.GetProjectionGuardScopeKey(scopeKey);
            var operationId = Guid.NewGuid();
            var activeOperations = new HashSet<Guid>
            {
                operationId
            };
            // This registration is deliberately a separate request before the data batch.
            // Set additions commute with a backfill claim, so an already-in-flight tick
            // cannot be hidden by scalar last-write-wins timestamps on the guard row.
            List<object> registrationCommands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                .SetParameters(new InsertMarketDataProjectionScopeMutationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, operationId, DateTime.UtcNow))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RegisterMarketDataProjectionGuardOperationV3)}", MarketDataDbCql.RegisterMarketDataProjectionGuardOperationV3)
                .SetParameters(new RegisterMarketDataProjectionGuardOperationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, activeOperations))
                .QueueCommand(), ];
            try
            {
                async Task RegisterGuardAsync() => await (db.ExecuteQueuedCommandsAsync(registrationCommands, useTransaction: true))
                .ConfigureAwait(false);
                if (context.TickProjectionGuardRegistrationForTestingAsync is { } registration)
                    await (registration(RegisterGuardAsync))
                .ConfigureAwait(false);
                else
                    await (RegisterGuardAsync())
                .ConfigureAwait(false);
            }
            catch
            {
                // A timed-out logged registration batch may still be replayed server-side.
                // Preserve its original journal timestamp so automatic recovery cannot
                // remove the marker before a delayed guard activation is applied.
                await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.RegistrationResponseUnknown))
                .ConfigureAwait(false);
                throw;
            }

            if (context.TickProjectionGuardRegisteredForTestingAsync is { } guardRegistered)
            {
                try
                {
                    await (guardRegistered())
                .ConfigureAwait(false);
                }
                catch
                {
                    // No data request has started, so this operation is safe for automatic
                    // recovery even if its registration response was delayed.
                    await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission))
                .ConfigureAwait(false);
                    throw;
                }
            }

            try
            {
                await (db.ExecuteQueuedCommandsAsync(createDataCommands(db), useTransaction: true))
                .ConfigureAwait(false);
            }
            catch
            {
                // A logged data batch timeout is ambiguous: batchlog replay can still apply
                // canonical data after this catch. Keep the original nonfailed journal row
                // and active guard ID. Only an explicit cutoff after writers are drained may
                // reclaim it; automatic failed-operation recovery would reopen a race.
                await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.DataBatchResponseUnknown))
                .ConfigureAwait(false);
                throw;
            }

            bool guardCompleted;
            try
            {
                guardCompleted = await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionGuardOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionGuardOperationV3)
                .SetParameters(new CompleteMarketDataProjectionGuardOperationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, Guid.NewGuid(), activeOperations, DateTime.UtcNow, activeOperations))
                .ExecuteSingleAsync(MarketDataDbContext.MapToBoolean))
                .ConfigureAwait(false);
            }
            catch
            {
                await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged))
                .ConfigureAwait(false);
                throw;
            }

            if (guardCompleted)
            {
                try
                {
                    await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                .SetParameters(new DeleteMarketDataProjectionScopeMutationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, operationId))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                }
                catch
                {
                    // The data and guard are committed. Retain a recoverable failed marker
                    // instead of making the caller retry a successful tick write.
                    await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged))
                .ConfigureAwait(false);
                }

                return;
            }

            MarketDataProjectionScopeStateReadModel? guardState;
            try
            {
                var states = await (context.GetProjectionScopeStatesAsync(MarketDataDbContext.FuturesTickByTimeProjection, new[] { guardScopeKey }))
                .ConfigureAwait(false);
                guardState = states[guardScopeKey];
            }
            catch
            {
                await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged))
                .ConfigureAwait(false);
                return;
            }

            if (guardState is { Blocked: true })
            {
                // Backfill owns the guard. Leaving this ID active makes its conditional
                // release fail; the failed marker lets the next repair reclaim it exactly.
                await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged))
                .ConfigureAwait(false);
                return;
            }

            // Another ordinary tick sharing this guard may have prevented the singleton
            // LWT. Its own data-scope generation still protects readers, so remove only
            // this operation and marker. Calls from one bulk write are serialized per guard
            // to keep this uncommon cross-process path off the normal fast path.
            try
            {
                List<object> cleanupCommands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)
                .SetParameters(new RemoveMarketDataProjectionScopeOperationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, operationId))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                .SetParameters(new DeleteMarketDataProjectionScopeMutationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, operationId))
                .QueueCommand(), ];
                await (db.ExecuteQueuedCommandsAsync(cleanupCommands, useTransaction: true))
                .ConfigureAwait(false);
            }
            catch
            {
                await (MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged))
                .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs the <c>UpsertFuturesEodProjectionAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="e">The e used by this operation.</param>
        /// <param name="openPrice">The open price used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task UpsertFuturesEodProjectionAsync(FuturesEodDataV2ReadModel e, decimal openPrice)
        {
            var db = context._dbFactory.MarketDataDb;
            var yearMonth = MarketDataDbContextExtensions.ToYearMonth(e.ValueDate);
            List<object> commands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                .SetParameters(MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(e, openPrice))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(new InsertMarketDataProjectionMonth(MarketDataDbContext.FuturesEodProjection, yearMonth))
                .QueueCommand(), ];
            await (db.ExecuteQueuedCommandsAsync(commands))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>UpsertVixFuturesContractIndexAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task UpsertVixFuturesContractIndexAsync(string contractId) => await (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertVixFuturesContractIndex)}", MarketDataDbCql.InsertVixFuturesContractIndex)
                .SetParameters(new InsertVixFuturesContractIndex(MarketDataDbContextExtensions.GetVixContractBucket(contractId), contractId))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        /// <summary>
        /// Performs the <c>InsertFuturesEodBatchAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="batch">The batch used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task InsertFuturesEodBatchAsync(ICollection<FuturesEodDataV2ReadModel> batch)
        {
            if (batch.Count == 0)
                return;
                await context.InsertNativeFuturesEodBatchAsync(batch)
                .ConfigureAwait(false);
                return;
        }

        /// <summary>
        /// Performs the <c>ReadLegacyCurrentFuturesEodDataAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<FuturesEodDataV2ReadModel?> ReadLegacyCurrentFuturesEodDataAsync(DateOnly valueDate)
        {
            FuturesEodDataV2ReadModel? latest = null;
            await foreach (var row in (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
            .ExecuteStreamAsync(MarketDataDbContext.MapToFuturesEodData!))
                .ConfigureAwait(false))
            {
                if (row.ValueDate > valueDate)
                    continue;
                if (latest is null || row.ValueDate > latest.ValueDate || row.ValueDate == latest.ValueDate && string.CompareOrdinal(row.ContractId, latest.ContractId) < 0 || row.ValueDate == latest.ValueDate && string.Equals(row.ContractId, latest.ContractId, StringComparison.Ordinal) && string.CompareOrdinal(row.Symbol, latest.Symbol) < 0)
                {
                    latest = row;
                }
            }

            return latest;
        }

        /// <summary>
        /// Performs the <c>ReadLegacyFuturesEodDataByMonthsAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="startDate">The start date used by this operation.</param>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <param name="yearMonths">The year months used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<FuturesEodDataV2ReadModel>> ReadLegacyFuturesEodDataByMonthsAsync(DateOnly startDate, DateOnly endDate, IReadOnlySet<int> yearMonths)
        {
            List<FuturesEodDataV2ReadModel> results = [];
            await foreach (var row in (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
            .ExecuteStreamAsync(MarketDataDbContext.MapToFuturesEodData!))
                .ConfigureAwait(false))
            {
                if (row.ValueDate >= startDate && row.ValueDate <= endDate && yearMonths.Contains(MarketDataDbContextExtensions.ToYearMonth(row.ValueDate)))
                    results.Add(row);
            }

            return results;
        }

        /// <summary>
        /// Performs the <c>ReadLegacyVixFuturesEodDataByValueDateAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<VixFuturesEodDataReadModel>> ReadLegacyVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
        {
            List<VixFuturesEodDataReadModel> results = [];
            await foreach (var row in (context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataAll)}", MarketDataDbCql.GetVixFuturesEodDataAll)
            .ExecuteStreamAsync(MarketDataDbContext.MapToVixFuturesEodData))
                .ConfigureAwait(false))
            {
                if (row.ValueDate <= valueDate)
                    results.Add(row);
            }

            return[..results.OrderByDescending(static row => row.ValueDate).ThenBy(static row => row.ContractId, StringComparer.Ordinal), ];
        }

        /// <summary>
        /// Performs the <c>ReadIndexedVixFuturesEodDataByValueDateAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<VixFuturesEodDataReadModel>> ReadIndexedVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
        {
            var db = context._dbFactory.MarketDataDb;
            HashSet<string> contractIds = new(StringComparer.Ordinal);
            for (var firstBucket = 0; firstBucket < MarketDataDbContext.VixContractBucketCount; firstBucket += MarketDataDbContext.ProjectionReadConcurrency)
            {
                var bucketCount = Math.Min(MarketDataDbContext.ProjectionReadConcurrency, MarketDataDbContext.VixContractBucketCount - firstBucket);
                var bucketReads = Enumerable.Range(firstBucket, bucketCount).Select(async bucket => await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesContractIds)}", MarketDataDbCql.GetVixFuturesContractIds)
                .SetParameters(new GetVixFuturesContractIds(bucket))
                .ExecuteQueryAsync(MarketDataDbContext.MapToString))
                .ConfigureAwait(false)).ToArray();
                foreach (var bucket in await (Task.WhenAll(bucketReads))
                .ConfigureAwait(false))
                    contractIds.UnionWith(bucket);
            }

            List<VixFuturesEodDataReadModel> results = [];
            var orderedContractIds = contractIds.Order(StringComparer.Ordinal).ToArray();
            for (var offset = 0; offset < orderedContractIds.Length; offset += MarketDataDbContext.ProjectionReadConcurrency)
            {
                var count = Math.Min(MarketDataDbContext.ProjectionReadConcurrency, orderedContractIds.Length - offset);
                var contractReads = orderedContractIds.AsSpan(offset, count).ToArray().Select(async contractId => await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataThroughDate)}", MarketDataDbCql.GetVixFuturesEodDataThroughDate)
                .SetParameters(new GetVixFuturesEodDataThroughDate(contractId, valueDate))
                .ExecuteQueryAsync(MarketDataDbContext.MapToVixFuturesEodData))
                .ConfigureAwait(false)).ToArray();
                foreach (var rows in await (Task.WhenAll(contractReads))
                .ConfigureAwait(false))
                    results.AddRange(rows);
            }

            return[..results.OrderByDescending(static row => row.ValueDate).ThenBy(static row => row.ContractId, StringComparer.Ordinal), ];
        }

        /// <summary>
        /// Performs the <c>GetYieldCurveRatesCoreAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="startDate">The start date used by this operation.</param>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesCoreAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
        {
            if (endDate < startDate)
                return[];
            if (endDate.DayNumber - startDate.DayNumber + 1 > MarketDataDbContext.YieldCurveMaximumRangeDays)
            {
                throw new ArgumentOutOfRangeException(nameof(endDate), $"Yield-curve ranges may span at most {MarketDataDbContext.YieldCurveMaximumRangeDays} days.");
            }

            return await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRates)}", MarketDataDbSql.GetYieldCurveRates)
                .SetParameters(new GetYieldCurveRates(startDate, endDate) { PostgreSql = true })
                .ExecuteQueryAsync(MarketDataDbContext.MapToYieldCurveRate, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>GetIntrinsicTimeModes</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal List<string> GetIntrinsicTimeModes() => [IntrinsicTimeModeType.TrendExtremeChanged.ToStringFast(), IntrinsicTimeModeType.TrendReversalChanged.ToStringFast(), IntrinsicTimeModeType.TrendDirectionChanged.ToStringFast(), ];
        /// <summary>
        /// Performs the <c>ReadCanonicalFuturesItiSignalsAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractIds">The contract ids used by this operation.</param>
        /// <param name="startDate">The start date used by this operation.</param>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadCanonicalFuturesItiSignalsAsync(IReadOnlyCollection<string> contractIds, DateOnly startDate, DateOnly endDate)
        {
            return await context._dbFactory.MarketDataDb.Use("NativeIti.Range", MarketDataDbSql.GetNativeItiRange)
                .SetParameters(new NativeItiRange(contractIds,startDate,endDate))
                .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadFuturesItiSignalsByDateRangeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractIds">The contract ids used by this operation.</param>
        /// <param name="startDate">The start date used by this operation.</param>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiSignalsByDateRangeAsync(IReadOnlyCollection<string> contractIds, DateOnly startDate, DateOnly endDate)
        {
            return await context.ReadCanonicalFuturesItiSignalsAsync(contractIds,startDate,endDate)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadFuturesItiDayModeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <param name="afterSequenceId">The after sequence id used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiDayModeAsync(string contractId, DateOnly valueDate, IntrinsicTimeModeType intrinsicTimeMode, long? afterSequenceId = null, CancellationToken cancellationToken = default)
        {
            return await context._dbFactory.MarketDataDb.Use("NativeIti.DayMode", MarketDataDbSql.GetNativeItiDayMode)
                .SetParameters(new NativeItiDayMode(contractId,valueDate,intrinsicTimeMode.ToStringFast(),afterSequenceId ?? long.MinValue))
                .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!,cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadLastFuturesItiTrendModeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<FuturesItiSignalV2ReadModel?> ReadLastFuturesItiTrendModeAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, IntrinsicTimeModeType intrinsicTimeMode, CancellationToken cancellationToken = default)
        {
            return await context._dbFactory.MarketDataDb.Use("NativeIti.LatestTrendMode", MarketDataDbSql.GetNativeItiLatestTrendMode)
                .SetParameters(new NativeItiLatestTrendMode(contractId,valueDate,intrinsicTimeTrend.ToStringFast(),intrinsicTimeMode.ToStringFast()))
                .ExecuteSingleAsync(MarketDataDbContext.MapToFuturesItiSignal!,cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadLatestAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="seriesIdentity">The series identity used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="configurationId">The configuration id used by this operation.</param>
        /// <param name="cql">The cql used by this operation.</param>
        /// <param name="operation">The operation used by this operation.</param>
        /// <param name="map">The map used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <typeparam name="T">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<T?> ReadLatestAsync<T>(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, string configurationId, string cql, string operation, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken)
            where T : class
        {
            var endOfValueDateUtc = valueDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            var result = await context.ReadMonthAsync(seriesIdentity, valueDate, endOfValueDateUtc, configurationId, cql, operation, map, cancellationToken)
                .ConfigureAwait(false);
            if (result is not null)
                return result;
            var previousMonth = valueDate.AddMonths(-1);
            return await context.ReadMonthAsync(seriesIdentity, previousMonth, endOfValueDateUtc, configurationId, cql, operation, map, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>ReadMonthAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="seriesIdentity">The series identity used by this operation.</param>
        /// <param name="partitionMonth">The partition month used by this operation.</param>
        /// <param name="marketDataAsOf">The market data as of used by this operation.</param>
        /// <param name="configurationId">The configuration id used by this operation.</param>
        /// <param name="cql">The cql used by this operation.</param>
        /// <param name="operation">The operation used by this operation.</param>
        /// <param name="map">The map used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <param name="timeFrame">The time frame used by this operation.</param>
        /// <typeparam name="T">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task<T?> ReadMonthAsync<T>(MarketSeriesIdentity seriesIdentity, DateOnly partitionMonth, DateTime marketDataAsOf, string configurationId, string cql, string operation, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken, TimeFrameType timeFrame = TimeFrameType.Daily)
            where T : class
        {
            var statement = operation switch
                {
                    nameof(MarketDataDbCql.GetLatestFuturesEmaSignal) => MarketDataDbSql.GetLatestFuturesEmaSignal,
                    nameof(MarketDataDbCql.GetLatestFuturesBollingerBandSignal) => MarketDataDbSql.GetLatestFuturesBollingerBandSignal,
                    _ => cql
                };
            return context._dbFactory.MarketDataDb.Use($"MarketAnalytics.{operation}", statement)
                .SetParameters(new GetLatestFuturesRegimeSignal(seriesIdentity.Format(), timeFrame.ToString(), configurationId, MarketDataDbContextExtensions.Bucket(partitionMonth), marketDataAsOf) { PostgreSql = !string.Equals(statement, cql, StringComparison.Ordinal) })
                .ExecuteSingleAsync(map, cancellationToken);
        }
        /// <summary>
        /// Performs the <c>ReadObservationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="environment">The environment used by this operation.</param>
        /// <param name="id">The id used by this operation.</param>
        /// <param name="token">The token used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<OptionIvObservation?> ReadObservationAsync(string environment, string id, CancellationToken token) => await context.Database.Use("OptionVolatility.Observation.Id.Read", MarketDataDbSql.SelectObservationById)
                .SetParameters(new OptionVolatilityParameters([environment, id]) { PostgreSql = true })
                .ExecuteSingleAsync(row => MarketDataDbContextExtensions.Deserialize<OptionIvObservation>(row.GetBytes(0)), token)
                .ConfigureAwait(false);
        /// <summary>
        /// Performs the <c>ReadLatestAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="scope">The scope used by this operation.</param>
        /// <param name="token">The token used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<OptionIvLatestPointer?> ReadLatestAsync(VolatilityStorageScope scope, CancellationToken token) => await context.Database.Use("OptionVolatility.Latest.Read", MarketDataDbSql.SelectLatest)
                .SetParameters(new OptionVolatilityParameters([scope.Environment, scope.Series.SeriesId, scope.Series.MethodologyVersion, scope.MetricPolicyVersion, ]) { PostgreSql = true })
                .ExecuteSingleAsync(row => new OptionIvLatestPointer(scope, row.GetString(0), row.GetString(1), row.GetLong(2), new DateTimeOffset(DateTime.SpecifyKind(row.GetDateTime(3), DateTimeKind.Utc))), token)
                .ConfigureAwait(false);
            /// <summary>Commits history, current snapshots, and lookup copies together without a business event append.</summary>
        /// <param name="rows">The rows used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal async Task PersistNativeFuturesEodBatchAsync(IReadOnlyList<BufferedFuturesEodRow> rows)
        {
            var latest = rows.GroupBy(row => (row.Snapshot.ContractId, row.Snapshot.ValueDate, row.Snapshot.Symbol))
                .Select(group => group.Last().Snapshot).ToArray();
            var history = rows.Where(row => row.AppendHistory).Select(row =>
            {
                var e = row.Snapshot;
                return new InsertFuturesIntraDayData(e.ContractId, e.ValueDate,
                    row.HistorySequenceId ?? throw new InvalidOperationException("History sequence must be allocated before persistence."),
                    e.Symbol, e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice, e.Volume,
                    e.DailyPercentChange, e.DailyStdDev, e.DailyStdDevAmount, e.UpperBand, e.Mean,
                    e.LowerBand, e.MarketDirection.ToStringFast(), e.MarketVolatility.ToStringFast(),
                    e.PriceDirection.ToStringFast(), e.PriceVolatility.ToStringFast(), e.MarketDirectionIndicator,
                    e.WindowSize) { PostgreSql = true };
            }).ToArray();
            var commands = context.CreateNativeFuturesEodCommands(latest);
            if (history.Length > 0)
                commands.Add(context.Use(nameof(MarketDataDbSql.InsertFuturesIntraDayData), MarketDataDbSql.InsertFuturesIntraDayData)
                    .SetParameters(history)
            .QueueCommand());
            await context.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
            .ConfigureAwait(false);
        }


        /// <summary>Atomically imports current EOD values and their lookup copies.</summary>
        /// <param name="rows">The rows used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal Task InsertNativeFuturesEodBatchAsync(ICollection<FuturesEodDataV2ReadModel> rows)
        {
            MarketDataDbContextExtensions.EnsureDistinctFuturesEodWrites(rows);
            return context.ExecuteQueuedCommandsAsync(context.CreateNativeFuturesEodCommands(rows), useTransaction: true);
        }


        /// <summary>Deletes both current EOD materializations atomically; retained observation history is unchanged.</summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal Task DeleteNativeFuturesEodAsync(string contractId, DateOnly valueDate)
        {
            List<object> commands =
            [
                context.Use(nameof(MarketDataDbSql.DeleteFuturesEodData), MarketDataDbSql.DeleteFuturesEodData)
                    .SetParameters(new DeleteFuturesEodData(contractId, valueDate) { PostgreSql = true })
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.DeleteFuturesEodDataByMonth), MarketDataDbSql.DeleteFuturesEodDataByMonth)
                    .SetParameters(new DeleteFuturesEodDataByMonth(MarketDataDbContextExtensions.ToYearMonth(valueDate), valueDate, contractId) { PostgreSql = true })
            .QueueCommand()
            ];
            return context.ExecuteQueuedCommandsAsync(commands, useTransaction: true);
        }


        /// <summary>Builds typed native commands; no CQL translation occurs at execution.</summary>
        /// <param name="rows">The rows used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        internal List<object> CreateNativeFuturesEodCommands(ICollection<FuturesEodDataV2ReadModel> rows)
        {
            return
            [
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodData), MarketDataDbSql.InsertFuturesEodData)
                    .SetParameters(rows.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataParameters(e, e.OpenPrice) with { PostgreSql = true }))
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodDataByMonth), MarketDataDbSql.InsertFuturesEodDataByMonth)
                    .SetParameters(rows.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(e, e.OpenPrice) with { PostgreSql = true }))
            .QueueCommand(),
                context.Use(nameof(MarketDataDbSql.InsertFuturesEodDataIndex), MarketDataDbSql.InsertFuturesEodDataIndex)
                    .SetParameters(rows.Select(e => new InsertFuturesEodDataIndex(e.ValueDate, e.ContractId) { PostgreSql = true }).Distinct())
            .QueueCommand()
            ];
        }

        /// <summary>Serializes read-modify-write for one VX contract/date. Session statistics replace cumulative values;
        /// otherwise the tick updates high/low/close and adds its size exactly once per call.</summary>
        /// <param name="tick">The tick used by this operation.</param>
        /// <param name="statistics">The statistics used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        internal async Task InsertNativeVixTickAsync(FuturesTickDataV2ReadModel tick, FuturesSessionStatisticsSnapshot? statistics)
        {
            ArgumentNullException.ThrowIfNull(tick);
            if (statistics is { } supplied && (!string.Equals(supplied.ContractId,tick.ContractId,StringComparison.Ordinal) || supplied.ValueDate != tick.ValueDate))
                throw new ArgumentException("Session statistics must match the tick contract and value date.", nameof(statistics));
            var transactions = new PostgresEventTransaction(new DbConnectionSettings()
                .Add(EventSourceActorDbContext.EventSourceActorDbConnection,context.ConnectionString,"System.Data.Postgres"));
            await transactions.ExecuteOnceAsync(async (transaction, token) =>
            {
                var value = await MarketDataDbContextExtensions.ResolveSnapshotAsync(transaction,tick,statistics,token)
            .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertVixFuturesEodData, [value.ContractId,value.ValueDate,value.OpenPrice,value.HighPrice,value.LowPrice,value.ClosePrice,value.Volume],token)
                .ConfigureAwait(false);
                return true;
            })
            .ConfigureAwait(false);
        }
}

    extension(DateOnly valueDate)
    {
        /// <summary>
        /// Performs the <c>ToYearMonth</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int ToYearMonth() => checked(valueDate.Year * 100 + valueDate.Month);
        /// <summary>
        /// Performs the <c>GetFuturesEodScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesEodScopeKey() => MarketDataDbContextExtensions.GetFuturesEodScopeKey(MarketDataDbContextExtensions.ToYearMonth(valueDate));
        /// <summary>
        /// Performs the <c>GetFuturesItiCalendarBucketStart</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="period">The period used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateOnly GetFuturesItiCalendarBucketStart(TimeFrameType period) => period switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(-(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => valueDate,
        };
        /// <summary>
        /// Performs the <c>Bucket</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int Bucket() => (valueDate.Year * 100) + valueDate.Month;
    }

    extension(int yearMonth)
    {
        /// <summary>
        /// Performs the <c>GetMonthStart</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateOnly GetMonthStart() => new(yearMonth / 100, yearMonth % 100, 1);
        /// <summary>
        /// Performs the <c>GetMonthEnd</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateOnly GetMonthEnd()
        {
            var monthStart = MarketDataDbContextExtensions.GetMonthStart(yearMonth);
            if (monthStart.Year == DateOnly.MaxValue.Year && monthStart.Month == DateOnly.MaxValue.Month)
                return DateOnly.MaxValue;
            return monthStart.AddMonths(1).AddDays(-1);
        }

        /// <summary>
        /// Performs the <c>GetFuturesEodScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesEodScopeKey() => yearMonth.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    extension(DateOnly startDate)
    {
        /// <summary>
        /// Performs the <c>GetYearMonths</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S127", Justification = "The last supported calendar month exits before AddMonths to avoid overflow at DateOnly.MaxValue.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4456", Justification = "Validation occurs on enumeration immediately before database access; callers consume this streaming contract asynchronously.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal IEnumerable<int> GetYearMonths(DateOnly endDate)
        {
            if (startDate > endDate)
                throw new ArgumentOutOfRangeException(nameof(startDate), startDate, "Start date must be on or before end date.");
            for (var month = new DateOnly(startDate.Year, startDate.Month, 1); month <= endDate;)
            {
                yield return MarketDataDbContextExtensions.ToYearMonth(month);
                if (month.Year == DateOnly.MaxValue.Year && month.Month == DateOnly.MaxValue.Month)
                    yield break;
                month = month.AddMonths(1);
            }
        }
    }

    extension(FuturesTickDataV2ReadModel row)
    {
        /// <summary>
        /// Performs the <c>GetFuturesTickIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetFuturesTickIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.ContractId);
            hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
            hash = MarketDataProjectionHash.Add(hash, row.TickId);
            hash = MarketDataProjectionHash.Add(hash, row.TickTime);
            hash = MarketDataProjectionHash.Add(hash, row.Price);
            return MarketDataProjectionHash.Add(hash, row.Size);
        }
    }

    extension(FuturesEodDataV2ReadModel row)
    {
        /// <summary>
        /// Performs the <c>GetFuturesEodIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetFuturesEodIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.ContractId);
            hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
            hash = MarketDataProjectionHash.Add(hash, row.Symbol);
            hash = MarketDataProjectionHash.Add(hash, row.OpenPrice);
            hash = MarketDataProjectionHash.Add(hash, row.HighPrice);
            hash = MarketDataProjectionHash.Add(hash, row.LowPrice);
            hash = MarketDataProjectionHash.Add(hash, row.ClosePrice);
            hash = MarketDataProjectionHash.Add(hash, row.Volume);
            hash = MarketDataProjectionHash.Add(hash, row.DailyPercentChange);
            hash = MarketDataProjectionHash.Add(hash, row.DailyStdDev);
            hash = MarketDataProjectionHash.Add(hash, row.DailyStdDevAmount);
            hash = MarketDataProjectionHash.Add(hash, row.UpperBand);
            hash = MarketDataProjectionHash.Add(hash, row.Mean);
            hash = MarketDataProjectionHash.Add(hash, row.LowerBand);
            hash = MarketDataProjectionHash.Add(hash, (int)row.MarketDirection);
            hash = MarketDataProjectionHash.Add(hash, (int)row.MarketVolatility);
            hash = MarketDataProjectionHash.Add(hash, (int)row.PriceDirection);
            hash = MarketDataProjectionHash.Add(hash, (int)row.PriceVolatility);
            hash = MarketDataProjectionHash.Add(hash, row.MarketDirectionIndicator);
            hash = MarketDataProjectionHash.Add(hash, row.WindowSize);
            hash = MarketDataProjectionHash.Add(hash, row.FiftyDMA);
            return MarketDataProjectionHash.Add(hash, row.TwoHundredDMA);
        }
    }

    extension(ICollection<FuturesTickDataV2ReadModel> rows)
    {
        /// <summary>
        /// Performs the <c>EnsureDistinctFuturesTickWrites</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void EnsureDistinctFuturesTickWrites()
        {
            var canonicalKeys = new HashSet<(string ContractId, DateOnly ValueDate, long TickId)>(rows.Count);
            foreach (var row in rows)
            {
                if (!canonicalKeys.Add((row.ContractId, row.ValueDate, row.TickId)))
                {
                    throw new ArgumentException($"The futures-tick write contains duplicate canonical key " + $"('{row.ContractId}', '{row.ValueDate:yyyy-MM-dd}', {row.TickId}).", nameof(rows));
                }
            }
        }
    }

    extension(ICollection<FuturesEodDataV2ReadModel> rows)
    {
        /// <summary>
        /// Performs the <c>EnsureDistinctFuturesEodWrites</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void EnsureDistinctFuturesEodWrites()
        {
            var canonicalKeys = new HashSet<(string ContractId, DateOnly ValueDate, string Symbol)>(rows.Count);
            foreach (var row in rows)
            {
                if (!canonicalKeys.Add((row.ContractId, row.ValueDate, row.Symbol)))
                {
                    throw new ArgumentException($"The futures-EOD write contains duplicate canonical key " + $"('{row.ContractId}', '{row.ValueDate:yyyy-MM-dd}', '{row.Symbol}').", nameof(rows));
                }
            }
        }
    }

    extension(string contractId)
    {
        /// <summary>
        /// Performs the <c>GetVixContractIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetVixContractIdentity() => MarketDataDbContextExtensions.GetVixContractIdentity(MarketDataDbContextExtensions.GetVixContractBucket(contractId), contractId);
        /// <summary>
        /// Performs the <c>GetVixContractBucket</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int GetVixContractBucket() => (int)(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), contractId) % MarketDataDbContext.VixContractBucketCount);
        /// <summary>
        /// Performs the <c>GetFuturesTickScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesTickScopeKey(DateOnly valueDate) => string.Concat(contractId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", contractId, ":", valueDate.DayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        /// <summary>
        /// Performs the <c>GetVixContractIndexScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetVixContractIndexScopeKey() => MarketDataDbContextExtensions.GetVixContractIndexScopeKey(MarketDataDbContextExtensions.GetVixContractBucket(contractId));
        /// <summary>
        /// Performs the <c>IsFuturesContractForSymbol</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="symbol">The symbol used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsFuturesContractForSymbol(string symbol)
        {
            if (!contractId.StartsWith(symbol, StringComparison.Ordinal))
                return false;
            var suffix = contractId.AsSpan(symbol.Length);
            if (suffix.Length == 8 && DateOnly.TryParseExact(suffix, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return true;
            return suffix.Length is >= 2 and <= 5 && "FGHJKMNQUVXZ".Contains(suffix[0], System.StringComparison.Ordinal) && suffix[1..].IndexOfAnyExceptInRange('0', '9') < 0;
        }

        /// <summary>
        /// Performs the <c>GetFuturesItiDayScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesItiDayScopeKey(DateOnly valueDate) => $"day:{contractId.Length}:{contractId}:{valueDate:yyyyMMdd}";
        /// <summary>
        /// Performs the <c>GetFuturesItiMonthScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="yearMonth">The year month used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesItiMonthScopeKey(int yearMonth) => $"month:{contractId.Length}:{contractId}:{yearMonth}";
        /// <summary>
        /// Performs the <c>GetFuturesItiTimelineScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <param name="yearMonth">The year month used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetFuturesItiTimelineScopeKey(string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth) => $"timeline:{contractId.Length}:{contractId}:{intrinsicTimeTrend.Length}:{intrinsicTimeTrend}:{intrinsicTimeMode.Length}:{intrinsicTimeMode}:{yearMonth}";
        /// <summary>
        /// Performs the <c>GetFuturesItiProjectionScopeKeys</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="valueDate">The value date used by this operation.</param>
        /// <param name="intrinsicTimeTrend">The intrinsic time trend used by this operation.</param>
        /// <param name="intrinsicTimeMode">The intrinsic time mode used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string[] GetFuturesItiProjectionScopeKeys(DateOnly valueDate, string intrinsicTimeTrend, string intrinsicTimeMode)
        {
            var yearMonth = MarketDataDbContextExtensions.ToYearMonth(valueDate);
            return[MarketDataDbContextExtensions.GetFuturesItiDayScopeKey(contractId, valueDate), MarketDataDbContextExtensions.GetFuturesItiMonthScopeKey(contractId, yearMonth), MarketDataDbContextExtensions.GetFuturesItiTimelineScopeKey(contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth), ];
        }
    }

    extension(int bucket)
    {
        /// <summary>
        /// Performs the <c>GetVixContractIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="contractId">The contract id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetVixContractIdentity(string contractId)
        {
            var hash = MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), bucket);
            return MarketDataProjectionHash.Add(hash, contractId);
        }

        /// <summary>
        /// Performs the <c>GetVixContractIndexScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetVixContractIndexScopeKey() => bucket.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    extension(string scopeKey)
    {
        /// <summary>
        /// Performs the <c>GetProjectionGuardScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetProjectionGuardScopeKey() => string.Concat(MarketDataDbContext.ProjectionGuardScopePrefix, (MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), scopeKey) % MarketDataDbContext.ProjectionGuardScopeCount).ToString(System.Globalization.CultureInfo.InvariantCulture));
        /// <summary>
        /// Performs the <c>IsProjectionGuardScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsProjectionGuardScopeKey() => scopeKey.StartsWith(MarketDataDbContext.ProjectionGuardScopePrefix, StringComparison.Ordinal);
    }

    extension(IEnumerable<string> scopeKeys)
    {
        /// <summary>
        /// Performs the <c>AddProjectionGuardScopes</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string[] AddProjectionGuardScopes()
        {
            var dataScopes = scopeKeys.Distinct(StringComparer.Ordinal).ToArray();
            return dataScopes.Concat(dataScopes.Where(static scope => !MarketDataDbContextExtensions.IsProjectionGuardScopeKey(scope)).Select(MarketDataDbContextExtensions.GetProjectionGuardScopeKey)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
    }

    extension(IObjectRepository db)
    {
        /// <summary>
        /// Performs the <c>EndProjectionScopeOperationsAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <param name="scopeKeys">The scope keys used by this operation.</param>
        /// <param name="activeOperations">The active operations used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task<long[]> EndProjectionScopeOperationsAsync(string projectionName, IEnumerable<string> scopeKeys, HashSet<Guid> activeOperations, CancellationToken cancellationToken = default)
        {
            var scopes = scopeKeys as ICollection<string> ?? scopeKeys.ToArray();
            return scopes.Count == 0 ? Task.FromResult(Array.Empty<long>()) : db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.EndMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.EndMarketDataProjectionScopeOperationV3)
                .SetParameters(scopes.Select(scope => new EndMarketDataProjectionScopeOperationV3(projectionName, scope, Guid.NewGuid(), activeOperations)))
                .ExecuteCommandAsync(cancellationToken);
        }

        /// <summary>
        /// Performs the <c>TryClassifyTickGuardOperationFailureAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="guardScopeKey">The guard scope key used by this operation.</param>
        /// <param name="operationId">The operation id used by this operation.</param>
        /// <param name="stage">The stage used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task TryClassifyTickGuardOperationFailureAsync(string guardScopeKey, Guid operationId, TickProjectionGuardFailureStage stage) => MarketDataDbContextExtensions.IsTickGuardFailureAutomaticallyRecoverable(stage) ? MarketDataDbContextExtensions.TryFailTickGuardOperationAsync(db, guardScopeKey, operationId) : Task.CompletedTask;
        /// <summary>
        /// Performs the <c>TryFailTickGuardOperationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="guardScopeKey">The guard scope key used by this operation.</param>
        /// <param name="operationId">The operation id used by this operation.</param>
        /// <returns>The completion of the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task TryFailTickGuardOperationAsync(string guardScopeKey, Guid operationId)
        {
            try
            {
                await (db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                .SetParameters(new InsertMarketDataProjectionScopeMutationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, operationId, DateTime.UnixEpoch))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            }
            catch
            {
            // Never remove ambiguous recovery evidence after another storage failure.
            }
        }

        /// <summary>
        /// Performs the <c>EndProjectionOperationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="projectionName">The projection name used by this operation.</param>
        /// <param name="activeOperations">The active operations used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task<long[]> EndProjectionOperationAsync(string projectionName, HashSet<Guid> activeOperations, CancellationToken cancellationToken = default) => db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.EndMarketDataProjectionOperation)}", MarketDataDbCql.EndMarketDataProjectionOperation)
                .SetParameters(new EndMarketDataProjectionOperation(projectionName, Guid.NewGuid(), activeOperations))
                .ExecuteCommandAsync(cancellationToken);
    }

    extension(TickProjectionGuardFailureStage stage)
    {
        /// <summary>
        /// Performs the <c>IsTickGuardFailureAutomaticallyRecoverable</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsTickGuardFailureAutomaticallyRecoverable() => stage is TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission or TickProjectionGuardFailureStage.AfterDataAcknowledged;
    }

    extension(FuturesEodDataV2ReadModel e)
    {
        /// <summary>
        /// Performs the <c>CreateFuturesEodDataParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="openPrice">The open price used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesEodData CreateFuturesEodDataParameters(decimal openPrice) => new(contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA);
        /// <summary>
        /// Performs the <c>CreateFuturesEodDataByMonthParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="openPrice">The open price used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesEodDataByMonth CreateFuturesEodDataByMonthParameters(decimal openPrice) => new(yearMonth: MarketDataDbContextExtensions.ToYearMonth(e.ValueDate), contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA);
    }

    extension(MarketDataDownloadCursor cursor)
    {
        /// <summary>
        /// Performs the <c>ValidateDownloadCursor</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidateDownloadCursor()
        {
            if (cursor.ImportCommandId == Guid.Empty || cursor.RequestedAtUtc == default || cursor.RequestedAtUtc != MarketDataDownloadOutcome.MillisecondUtc(cursor.RequestedAtUtc))
                throw new ArgumentException("Invalid download attempt cursor.", nameof(cursor));
        }
    }

    extension(IObjectDataRecord row)
    {
        /// <summary>
        /// Performs the <c>GetNullableString</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="index">The index used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string? GetNullableString(int index) => row.IsNull(index) ? null : row.GetString(index);
    }

    extension(DateTime value)
    {
        /// <summary>
        /// Performs the <c>NormalizeEconomicCalendarTimestamp</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateTime NormalizeEconomicCalendarTimestamp()
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
            return new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        }

        /// <summary>
        /// Performs the <c>EconomicCalendarMonthBucket</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int EconomicCalendarMonthBucket() => value.Year * 100 + value.Month;
        /// <summary>
        /// Performs the <c>Utc</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateTimeOffset Utc() => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    extension(DateTime startDate)
    {
        /// <summary>
        /// Performs the <c>EconomicCalendarMonthBucketsDescending</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="endDate">The end date used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal IEnumerable<int> EconomicCalendarMonthBucketsDescending(DateTime endDate)
        {
            var firstMonth = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var month = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc); month >= firstMonth; month = month.AddMonths(-1))
                yield return MarketDataDbContextExtensions.EconomicCalendarMonthBucket(month);
        }
    }

    extension(EconomicCalendarReadModel row)
    {
        /// <summary>
        /// Performs the <c>IsAfterCursor</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="cursor">The cursor used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsAfterCursor(CalendarPageToken cursor) => cursor.LastEventDateTicks is null || row.EventDate.Ticks < cursor.LastEventDateTicks.Value || (row.EventDate.Ticks == cursor.LastEventDateTicks.Value && string.CompareOrdinal(row.EventName, cursor.LastEventName) > 0);
        /// <summary>
        /// Performs the <c>GetEconomicCalendarProjectionIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetEconomicCalendarProjectionIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.EventDate.Ticks);
            hash = MarketDataProjectionHash.Add(hash, row.CountryCode);
            hash = MarketDataProjectionHash.Add(hash, row.EventName);
            hash = MarketDataProjectionHash.Add(hash, row.Actual);
            hash = MarketDataProjectionHash.Add(hash, row.Forecast);
            hash = MarketDataProjectionHash.Add(hash, row.Prior);
            hash = MarketDataProjectionHash.Add(hash, row.Impact);
            hash = MarketDataProjectionHash.Add(hash, row.Unit);
            hash = MarketDataProjectionHash.Add(hash, row.Change);
            hash = MarketDataProjectionHash.Add(hash, row.ChangePercentage);
            hash = MarketDataProjectionHash.Add(hash, row.CreatedOn.Ticks);
            return MarketDataProjectionHash.Add(hash, row.CreatedBy);
        }
    }

    extension(EconomicCalendarPageRequest request)
    {
        /// <summary>
        /// Performs the <c>GetPageRequestFingerprint</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="countries">The countries used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetPageRequestFingerprint(string[] countries)
        {
            var identity = $"v1|{request.StartDateUtc.Ticks}|{request.EndDateUtc.Ticks}|{string.Join(',', countries)}|{request.PageSize}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        }
    }

    extension(CalendarPageToken token)
    {
        /// <summary>
        /// Performs the <c>EncodePageToken</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string EncodePageToken()
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(token);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }

    extension(string? token)
    {
        /// <summary>
        /// Performs the <c>DecodePageToken</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="fingerprint">The fingerprint used by this operation.</param>
        /// <param name="partitionCount">The partition count used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal CalendarPageToken DecodePageToken(string fingerprint, int partitionCount)
        {
            if (string.IsNullOrEmpty(token))
                return new CalendarPageToken(fingerprint, 0, null, null);
            try
            {
                var value = token.Replace('-', '+').Replace('_', '/');
                value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
                var cursor = JsonSerializer.Deserialize<CalendarPageToken>(Convert.FromBase64String(value)) ?? throw new FormatException();
                if (!string.Equals(cursor.Fingerprint, fingerprint, StringComparison.Ordinal) || cursor.PartitionIndex < 0 || cursor.PartitionIndex > partitionCount || cursor.LastEventName?.Any(char.IsControl) == true)
                    throw new FormatException();
                return cursor;
            }
            catch (Exception ex)when (ex is FormatException or JsonException)
            {
                throw new ArgumentException("The economic-calendar continuation token is invalid for this request.", nameof(token), ex);
            }
        }
    }

    extension(YieldCurveRateReadModel row)
    {
        /// <summary>
        /// Performs the <c>GetYieldCurveProjectionIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetYieldCurveProjectionIdentity()
        {
            var hash = MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), row.ValueDate);
            hash = MarketDataProjectionHash.Add(hash, row.OneMonth);
            hash = MarketDataProjectionHash.Add(hash, row.TwoMonth);
            hash = MarketDataProjectionHash.Add(hash, row.ThreeMonth);
            hash = MarketDataProjectionHash.Add(hash, row.SixMonth);
            hash = MarketDataProjectionHash.Add(hash, row.OneYear);
            hash = MarketDataProjectionHash.Add(hash, row.TwoYear);
            hash = MarketDataProjectionHash.Add(hash, row.ThreeYear);
            hash = MarketDataProjectionHash.Add(hash, row.FiveYear);
            hash = MarketDataProjectionHash.Add(hash, row.SevenYear);
            hash = MarketDataProjectionHash.Add(hash, row.TenYear);
            hash = MarketDataProjectionHash.Add(hash, row.TwentyYear);
            return MarketDataProjectionHash.Add(hash, row.ThirtyYear);
        }
    }

    extension(IEnumerable<int> values)
    {
        /// <summary>
        /// Performs the <c>BuildIntegerSetIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ProjectionIdentity BuildIntegerSetIdentity()
        {
            var identity = new ProjectionIdentityBuilder();
            foreach (var value in values)
                identity.Add(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), value));
            return identity.Build();
        }
    }

    extension(FuturesTradeSignalRepairRow candidate)
    {
        /// <summary>
        /// Performs the <c>IsNewer</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="current">The current used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool IsNewer(FuturesTradeSignalRepairRow current) => (candidate.ValueDate.DayNumber, candidate.Timestamp.Ticks, candidate.SequenceId).CompareTo((current.ValueDate.DayNumber, current.Timestamp.Ticks, current.SequenceId)) > 0;
    }

    extension(string payload)
    {
        /// <summary>
        /// Performs the <c>ParseFuturesTradeSignalRepairRow</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal FuturesTradeSignalRepairParseResult ParseFuturesTradeSignalRepairRow()
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                var contractId = MarketDataDbContextExtensions.GetString(root, "contractid");
                var valueDateText = MarketDataDbContextExtensions.GetString(root, "valuedate");
                var timePeriodText = MarketDataDbContextExtensions.GetString(root, "timeperiod");
                var timestampText = MarketDataDbContextExtensions.GetString(root, "timestamp");
                var sequenceId = MarketDataDbContextExtensions.GetInt64(root, "sequenceid");
                List<string> errors = [];
                if (string.IsNullOrWhiteSpace(contractId) || contractId.Contains(',', System.StringComparison.Ordinal))
                    errors.Add("invalid contractId");
                var hasValidDate = DateOnly.TryParseExact(valueDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate);
                if (!hasValidDate || valueDate == DateOnly.MinValue || valueDate == DateOnly.MaxValue)
                    errors.Add("invalid valueDate");
                if (!Enum.TryParse<TimeFrameType>(timePeriodText, true, out var timePeriod) || !Enum.IsDefined(timePeriod))
                    errors.Add("invalid timePeriod");
                if (!TimeOnly.TryParse(timestampText, CultureInfo.InvariantCulture, out var timestamp))
                    errors.Add("invalid timestamp");
                if (sequenceId < 0)
                    errors.Add("invalid sequenceId");
                if (errors.Count != 0)
                    return new FuturesTradeSignalRepairParseResult(null, string.Join(", ", errors));
                return new FuturesTradeSignalRepairParseResult(new FuturesTradeSignalRepairRow(contractId, valueDate, timePeriod.ToStringFast(), timestamp, sequenceId), null);
            }
            catch (JsonException exception)
            {
                return new FuturesTradeSignalRepairParseResult(null, $"invalid JSON: {exception.Message}");
            }
        }

        /// <summary>
        /// Performs the <c>Fingerprint</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    extension<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord
    {
        /// <summary>
        /// Performs the <c>MapJsonPayload</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        internal string MapJsonPayload() => row.GetString(0);
    }

    extension(JsonElement root)
    {
        /// <summary>
        /// Performs the <c>GetString</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="name">The name used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string GetString(string name)
        {
            if (!MarketDataDbContextExtensions.TryGetProperty(root, name, out var property) || property.ValueKind == JsonValueKind.Null)
                return string.Empty;
            return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
        }

        /// <summary>
        /// Performs the <c>GetInt64</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="name">The name used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal long GetInt64(string name)
        {
            if (!MarketDataDbContextExtensions.TryGetProperty(root, name, out var property))
                return -1;
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
                return number;
            return long.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : -1;
        }

        /// <summary>
        /// Performs the <c>TryGetProperty</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="name">The name used by this operation.</param>
        /// <param name="value">The value used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool TryGetProperty(string name, out JsonElement value)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }

    extension(FuturesItiSignalV2ReadModel row)
    {
        /// <summary>
        /// Performs the <c>GetFuturesItiSignalIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal ulong GetFuturesItiSignalIdentity()
        {
            var hash = MarketDataProjectionHash.Start();
            hash = MarketDataProjectionHash.Add(hash, row.ContractId);
            hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
            hash = MarketDataProjectionHash.Add(hash, row.TimePeriod.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.SequenceId);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTime.Ticks);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeGroupId);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeLength);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicPrice);
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeTrend.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeMode.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.TrendPrice);
            hash = MarketDataProjectionHash.Add(hash, row.TrendExtreme);
            hash = MarketDataProjectionHash.Add(hash, row.TrendReversal);
            hash = MarketDataProjectionHash.Add(hash, row.TrendDelta);
            hash = MarketDataProjectionHash.Add(hash, row.TargetDelta);
            hash = MarketDataProjectionHash.Add(hash, row.Lambda);
            hash = MarketDataProjectionHash.Add(hash, row.TradingDays);
            hash = MarketDataProjectionHash.Add(hash, row.Threshold);
            hash = MarketDataProjectionHash.Add(hash, row.UpTrendTrigger);
            hash = MarketDataProjectionHash.Add(hash, row.DownTrendTrigger);
            hash = MarketDataProjectionHash.Add(hash, row.TradeState.ToStringFast());
            hash = MarketDataProjectionHash.Add(hash, row.BandLevel);
            return MarketDataProjectionHash.Add(hash, row.ReversalLevel);
        }

        /// <summary>
        /// Performs the <c>ToFuturesItiSignalMdi</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal FuturesItiSignalMDIV2ReadModel ToFuturesItiSignalMdi() => new(contractId: row.ContractId, valueDate: row.ValueDate, intrinsicTime: row.IntrinsicTime, trendType: row.IntrinsicTimeTrend, mdi: row.IntrinsicPrice);
    }

    extension(FuturesItiSignalV2ReadModel e)
    {
        /// <summary>
        /// Performs the <c>CreateFuturesItiSignalParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="sequenceId">The sequence id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesItiSignal CreateFuturesItiSignalParameters(long sequenceId) => new(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandLevel, e.ReversalLevel);
        /// <summary>
        /// Performs the <c>CreateFuturesItiSignalMonthParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="sequenceId">The sequence id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal InsertFuturesItiSignalByContractMonth CreateFuturesItiSignalMonthParameters(long sequenceId) => new(MarketDataDbContextExtensions.ToYearMonth(e.ValueDate), e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandLevel, e.ReversalLevel);
        /// <summary>
        /// Performs the <c>CreateFuturesItiTimeFrameStateParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="sequenceId">The sequence id used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Zero is the explicit missing-value sentinel in the persisted pricing contract; approximate comparison would classify valid nonzero values as missing.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal UpsertFuturesItiTimeFrameState CreateFuturesItiTimeFrameStateParameters(long sequenceId) => new(e.ContractId, e.TimePeriod.ToStringFast(), MarketDataDbContextExtensions.GetFuturesItiCalendarBucketStart(e.ValueDate, e.TimePeriod), e.TimeFrameStartValueDate == default ? e.ValueDate : e.TimeFrameStartValueDate, e.ValueDate, sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandAnchorPrice == 0 ? e.IntrinsicPrice : e.BandAnchorPrice, e.BandPercentage == 0 ? 0.15 : e.BandPercentage, e.BandSize == 0 ? e.Threshold * 0.15 : e.BandSize, e.BandLevel, e.ReversalLevel);
    }

    extension(IObjectDataRecord value)
    {
        /// <summary>
        /// Performs the <c>Decimal</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="index">The index used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal decimal? Decimal(int index) => value.IsNull(index) ? null : value.GetDecimal(index);
    }

    extension(OptionIvLatestPointer pointer)
    {
        /// <summary>
        /// Performs the <c>LatestValues</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionVolatilityParameters LatestValues() => new([pointer.Scope.Environment, pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion, pointer.Scope.MetricPolicyVersion, pointer.SnapshotId, pointer.SnapshotDigest, pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime, ]);
        /// <summary>
        /// Performs the <c>AdvanceLatestValues</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionVolatilityParameters AdvanceLatestValues() => new([pointer.SnapshotId, pointer.SnapshotDigest, pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime, pointer.Scope.Environment, pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion, pointer.Scope.MetricPolicyVersion, ]);
    }

    extension(VolatilityHistoryPageRequest request)
    {
        /// <summary>
        /// Performs the <c>HistoryValues</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <param name="includePolicy">The include policy used by this operation.</param>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionVolatilityParameters HistoryValues(bool includePolicy) => includePolicy ? new([request.Scope.Environment, request.Scope.Series.SeriesId, request.Scope.Series.MethodologyVersion, request.Scope.MetricPolicyVersion, request.Bucket.Value, request.FromValueDate, request.ToValueDate, ]) : new([request.Scope.Environment, request.Scope.Series.SeriesId, request.Scope.Series.MethodologyVersion, request.Bucket.Value, request.FromValueDate, request.ToValueDate, ]);
        /// <summary>
        /// Performs the <c>ValidatePage</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidatePage()
        {
            ArgumentNullException.ThrowIfNull(request);
            MarketDataDbContextExtensions.ValidateEnvironment(request.Scope.Environment);
            if (request.PageSize is < 1 or > 500 || request.FromValueDate > request.ToValueDate || VolatilityCalendarBucket.From(request.FromValueDate) != request.Bucket || VolatilityCalendarBucket.From(request.ToValueDate) != request.Bucket || request.Mode == VolatilityHistoricalMode.AsKnown && request.KnownAtUtc is null || request.Mode == VolatilityHistoricalMode.Restated && request.KnownAtUtc is not null)
                throw new ArgumentException("History query must be bounded to one calendar bucket.", nameof(request));
        }
    }

    extension(OptionIvPublication publication)
    {
        /// <summary>
        /// Performs the <c>Pointer</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal OptionIvLatestPointer Pointer()
        {
            var snapshot = publication.Metric.Snapshot;
            return new(new(publication.Environment, snapshot.Series, snapshot.MetricPolicyVersion), snapshot.SnapshotId, snapshot.SnapshotDigest, publication.Metric.PublicationSequence, snapshot.AvailableAtUtc);
        }

        /// <summary>
        /// Performs the <c>ValidatePublication</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidatePublication()
        {
            ArgumentNullException.ThrowIfNull(publication);
            MarketDataDbContextExtensions.ValidateEnvironment(publication.Environment);
            if (publication.Metric.Revision < 1 || publication.Metric.PublicationSequence < 1 || (publication.Metric.Revision == 1) != (publication.Metric.SupersedesSnapshotId is null) || publication.SourceObservations.IsDefaultOrEmpty || !publication.Metric.Snapshot.SourceObservationIds.Order(StringComparer.Ordinal).SequenceEqual(publication.SourceObservations.Select(x => x.ObservationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw new ArgumentException("A complete immutable publication is required.", nameof(publication));
        }
    }

    extension<T>(T value)
    {
        /// <summary>
        /// Performs the <c>Serialize</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        internal byte[] Serialize()
        {
            var payload = MessagePackBinarySerializer.Shared.Serialize(value) ?? throw new InvalidDataException("Option volatility serialization returned no payload.");
            if (payload.Length is 0 or > MarketDataDbContext.MaximumPayloadBytes)
                throw new ArgumentException("Option volatility payload exceeds its bounded size.");
            return payload;
        }
    }

    extension(byte[] payload)
    {
        /// <summary>
        /// Performs the <c>Deserialize</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <typeparam name="T">The event, snapshot, state or record type used by the operation.</typeparam>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal T Deserialize<T>()
        {
            if (payload.Length is 0 or > MarketDataDbContext.MaximumPayloadBytes)
                throw new InvalidDataException("Invalid option volatility payload size.");
            return MessagePackBinarySerializer.Shared.Deserialize<T>(payload) ?? throw new InvalidDataException("Missing option volatility payload.");
        }
    }

    extension(OptionIvObservation observation)
    {
        /// <summary>
        /// Performs the <c>ValidateObservation</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidateObservation()
        {
            ArgumentNullException.ThrowIfNull(observation);
            if (observation.SchemaVersion != OptionIvObservation.CurrentSchemaVersion || string.IsNullOrWhiteSpace(observation.ObservationId) || observation.ExchangeValueDate == default || observation.Revision < 1 || (observation.Revision == 1) != (observation.SupersedesObservationId is null) || observation.RecordedAtUtc.Offset != TimeSpan.Zero || observation.AvailableAtUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("Invalid option-IV observation.", nameof(observation));
        }
    }

    extension(LatestVolatilityRequest request)
    {
        /// <summary>
        /// Performs the <c>ValidateLatest</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidateLatest()
        {
            ArgumentNullException.ThrowIfNull(request);
            MarketDataDbContextExtensions.ValidateEnvironment(request.Scope.Environment);
            if (request.MaximumAge <= TimeSpan.Zero || request.RequestedAtUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("A positive configured freshness requirement is required.", nameof(request));
        }
    }

    extension(string environment)
    {
        /// <summary>
        /// Performs the <c>ValidateEnvironment</c> operation for MarketDataDb persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidateEnvironment() => ArgumentException.ThrowIfNullOrWhiteSpace(environment);
    }

    extension(DateOnly value)
    {
        /// <summary>
        /// Performs the <c>YearMonth</c> operation for MarketDataDb persistence.
        /// </summary>
        /// <returns>The typed result of the market-data query or mutation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int YearMonth() => checked(value.Year * 100 + value.Month);
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Checks the usable indexes for the canonical PostgreSQL query paths.</summary>
        /// <param name="token">Cancels the database query.</param>
        /// <returns>Readiness of PostgreSQL queries, with raw tick ownership explicitly excluded.</returns>
        internal async Task<MarketDataProjectionReadinessReadModel> ReadCanonicalQueryReadinessAsync(CancellationToken token)
        {
            var rows = await transaction.QueryAsync(MarketDataDbSql.GetCanonicalQueryReadiness, [], reader => new MarketDataProjectionReadinessReadModel(false, reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2)) { RawTicksManagedByTradeTickQuoteDb = true }, token)
                .ConfigureAwait(false);
            return rows.Single();
        }

        /// <summary>Rebuilds canonical indexes and reports the single-table query inventory in the same transaction.</summary>
        /// <param name="token">Cancels maintenance and rolls back its index changes.</param>
        /// <returns>The canonical inventory and readiness after rebuilding indexes.</returns>
        internal async Task<MarketDataProjectionBackfillReadModel> RebuildCanonicalQueryIndexesAsync(CancellationToken token)
        {
            await transaction.ExecuteAsync(Schema.MarketDataSchemaSql.RebuildCanonicalQueryIndexes, [], token)
                .ConfigureAwait(false);
            var rows = await transaction.QueryAsync(MarketDataDbSql.GetCanonicalQueryInventory, [], reader => (Kind: reader.GetString(0), Count: reader.GetInt64(1), Fingerprint: reader.GetString(2)), token)
                .ConfigureAwait(false);
            var inventory = rows.ToDictionary(row => row.Kind);
            var eod = inventory["eod"]; var vix = inventory["vix"]; var iti = inventory["iti"];
            var readiness = await transaction.ReadCanonicalQueryReadinessAsync(token)
                .ConfigureAwait(false);
            return new(0, 0, "not-applicable:TradeTickQuoteDb", "not-applicable:TradeTickQuoteDb",
                eod.Count, eod.Count, eod.Fingerprint, eod.Fingerprint,
                inventory["vix_rows"].Count, vix.Count, vix.Count, vix.Fingerprint, vix.Fingerprint,
                iti.Count, iti.Count, iti.Count, iti.Count,
                iti.Fingerprint, iti.Fingerprint, iti.Fingerprint, iti.Fingerprint, readiness.IsReady)
                { RawTicksManagedByTradeTickQuoteDb = true };
        }

    /// <summary>Resolves exact replacement facts while holding the owning transaction's contract/date lock.</summary>
    /// <param name="tick">The tick used by this operation.</param>
    /// <param name="statistics">The statistics used by this operation.</param>
    /// <param name="token">The token used by this operation.</param>
    /// <returns>The typed result of the market-data query or mutation.</returns>
    public async Task<VixFuturesEodDataReadModel> ResolveSnapshotAsync(        FuturesTickDataV2ReadModel tick,FuturesSessionStatisticsSnapshot? statistics,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(tick);
        if (statistics is { } supplied && (!string.Equals(supplied.ContractId,tick.ContractId,StringComparison.Ordinal) || supplied.ValueDate != tick.ValueDate))
            throw new ArgumentException("Session statistics must match the tick contract and value date.",nameof(statistics));
        var key = string.Create(CultureInfo.InvariantCulture,$"market_data.vix:{tick.ContractId}:{tick.ValueDate:yyyyMMdd}");
        await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,[key],token)
            .ConfigureAwait(false);
        var rows = await transaction.QueryAsync(MarketDataDbSql.GetVixFuturesEodData,[tick.ContractId,tick.ValueDate], reader => new VixFuturesEodDataReadModel(reader.GetString(0),reader.GetFieldValue<DateOnly>(1),reader.GetDecimal(2), reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetInt64(6)),token)
            .ConfigureAwait(false);
        var old = rows.Count == 0 ? null : rows[0];
        var hasPrices = statistics is { HasPriceStatistics: true };
        var open = hasPrices ? statistics!.Value.OpenPrice : old?.OpenPrice ?? tick.Price;
        var high = hasPrices ? statistics!.Value.HighPrice : Math.Max(old?.HighPrice ?? tick.Price,tick.Price);
        var low = hasPrices ? statistics!.Value.LowPrice : Math.Min(old?.LowPrice ?? tick.Price,tick.Price);
        var volume = statistics is { HasVolume: true } ? statistics.Value.Volume : checked((old?.Volume ?? 0) + tick.Size);
        return new(tick.ContractId,tick.ValueDate,open,high,low,tick.Price,volume);
    }



        /// <summary>Writes accepted calendar rows and country lookups with command ownership checks.</summary>
        /// <param name="rows">The rows used by the persistence operation.</param>
        /// <param name="policy">The policy used by the persistence operation.</param>
        /// <param name="commandId">The commandId used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task ImportEconomicCalendarsAsync(IReadOnlyList<EconomicCalendarReadModel> rows,
            ImportDuplicatePolicy policy, Guid commandId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(rows);
            MarketDataDbContextExtensions.ValidateImportPolicy(policy,commandId);
            var ordered = rows.OrderBy(row => row.CountryCode,StringComparer.Ordinal).ThenBy(row => row.EventDate).ThenBy(row => row.EventName,StringComparer.Ordinal).ToArray();
            var identities = new HashSet<(string,DateTime,string)>();
            foreach (var row in ordered)
            {
                var date = MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.EventDate);
                var month = MarketDataDbContextExtensions.EconomicCalendarMonthBucket(date);
                if (!identities.Add((row.CountryCode,date,row.EventName)))
                    throw new ArgumentException("An import batch contains duplicate economic-calendar identities.",nameof(rows));
                var key = string.Create(CultureInfo.InvariantCulture,$"economic-calendar:{row.CountryCode.Length}:{row.CountryCode}:{date.Ticks}:{row.EventName}");
                await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,[key],token)
                .ConfigureAwait(false);
                if (policy == ImportDuplicatePolicy.Reject)
                {
                    var owners = await transaction.QueryAsync(MarketDataDbSql.GetEconomicCalendarV2CommandId, (NpgsqlParameter[])new GetEconomicCalendarV2CommandId(row.CountryCode,month,date,row.EventName) { PostgreSql = true }.Bind(),reader => reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0),token)
                .ConfigureAwait(false);
                    if (owners.Count != 0 && owners[0] != commandId)
                        throw new MarketDataImportDuplicateException("Economic-calendar event is owned by another import command.");
                }
                var values = new InsertEconomicCalendarV2(row.CountryCode,month,date,row.EventName,row.Actual,row.Forecast,row.Prior,row.Impact,row.Unit,row.Change,row.ChangePercentage,
                    MarketDataDbContextExtensions.NormalizeEconomicCalendarTimestamp(row.CreatedOn),row.CreatedBy,commandId) { PostgreSql = true };
                await transaction.ExecuteAsync(MarketDataDbSql.InsertEconomicCalendarV2,(NpgsqlParameter[])values.Bind(),token)
                .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertEconomicCalendarCountryCode, (NpgsqlParameter[])new InsertEconomicCalendarCountryCode(MarketDataDbContext.EconomicCalendarLookupId,row.CountryCode) { PostgreSql = true }.Bind(),token)
                .ConfigureAwait(false);
            }
        }

        /// <summary>Rebuilds and verifies date/year lookups from canonical Treasury curves in one transaction.</summary>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task<FmpQueryProjectionBackfillReadModel> RebuildYieldCurveIndexesAsync(CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            await transaction.ExecuteAsync(MarketDataDbSql.LockYieldCurveIndexes,[],token)
                .ConfigureAwait(false);
            var sourceRows = await transaction.QueryAsync(MarketDataDbSql.GetCanonicalYieldCurvesForRebuild,[], reader => MarketDataDbContext.MapToYieldCurveRate(new TomasAI.IFM.Framework.Storage.AdoNetDataRecord().SetReader(reader)),token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.ClearYieldCurveDateIndex,[],token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.ClearYieldCurveYearIndex,[],token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.RebuildYieldCurveDateIndex,[],token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.RebuildYieldCurveYearIndex,[],token)
                .ConfigureAwait(false);
            var targetRows = await transaction.QueryAsync(MarketDataDbSql.GetIndexedYieldCurvesForRebuild,[], reader => MarketDataDbContext.MapToYieldCurveRate(new TomasAI.IFM.Framework.Storage.AdoNetDataRecord().SetReader(reader)),token)
                .ConfigureAwait(false);
            var targetYears = await transaction.QueryAsync(MarketDataDbSql.GetYieldCurveIndexYears,[],reader => reader.GetInt32(0),token)
                .ConfigureAwait(false);
            var sourceIdentity = new ProjectionIdentityBuilder();
            var targetIdentity = new ProjectionIdentityBuilder();
            foreach (var row in sourceRows) sourceIdentity.Add(MarketDataDbContextExtensions.GetYieldCurveProjectionIdentity(row));
            foreach (var row in targetRows) targetIdentity.Add(MarketDataDbContextExtensions.GetYieldCurveProjectionIdentity(row));
            var source = sourceIdentity.Build();
            var target = targetIdentity.Build();
            var sourceYears = sourceRows.Select(row => row.ValueDate.Year).Distinct().ToArray();
            var sourceYearIdentity = MarketDataDbContextExtensions.BuildIntegerSetIdentity(sourceYears);
            var targetYearIdentity = MarketDataDbContextExtensions.BuildIntegerSetIdentity(targetYears);
            if (sourceRows.Count != targetRows.Count || !string.Equals(source.Fingerprint,target.Fingerprint,StringComparison.Ordinal) || !string.Equals(sourceYearIdentity.Fingerprint,targetYearIdentity.Fingerprint,StringComparison.Ordinal))
                throw new InvalidDataException("Treasury index reconstruction failed verification; all index changes will roll back.");
            return new(sourceRows.Count,targetRows.Count,source.Fingerprint,target.Fingerprint,sourceYears.Length,targetYears.Count,sourceYearIdentity.Fingerprint,targetYearIdentity.Fingerprint);
        }

        /// <summary>Writes accepted Treasury curves and their indexes while enforcing import ownership.</summary>
        /// <param name="rows">The rows used by the persistence operation.</param>
        /// <param name="policy">The policy used by the persistence operation.</param>
        /// <param name="commandId">The commandId used by the persistence operation.</param>
        /// <param name="acceptedAtUtc">The acceptedAtUtc used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task ImportYieldCurvesAsync(IReadOnlyList<YieldCurveRateReadModel> rows,
            ImportDuplicatePolicy policy, Guid commandId, DateTime acceptedAtUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(rows);
            MarketDataDbContextExtensions.ValidateImportPolicy(policy,commandId);
            if (rows.Select(row => row.ValueDate).Distinct().Count() != rows.Count)
                throw new ArgumentException("An import batch must contain one Treasury curve per value date.",nameof(rows));
            // Stable lock order includes empty dates and serializes overwrite imports with reject-policy imports.
            foreach (var row in rows.OrderBy(row => row.ValueDate))
            {
                var key = row.ValueDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
                await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,["market_data.treasury-curve:" + key],token)
                .ConfigureAwait(false);
                if (policy == ImportDuplicatePolicy.Reject)
                {
                    var owners = await transaction.QueryAsync(MarketDataDbSql.ReadMarketDataImportOwnership, ["treasury-curve",key],reader => (CommandId:reader.GetGuid(0),MayWrite:reader.GetBoolean(1)),token)
                .ConfigureAwait(false);
                    if (owners.Count > 0)
                    {
                        if (owners[0].CommandId != commandId || !owners[0].MayWrite)
                            throw new MarketDataImportDuplicateException($"Treasury curve {key} is owned by another import command.");
                    }
                    else
                    {
                        var exists = (bool)(await transaction.ScalarAsync(MarketDataDbSql.YieldCurveExists,[row.ValueDate],token)
                .ConfigureAwait(false))!;
                        if (exists) throw new MarketDataImportDuplicateException($"Treasury curve {key} already exists.");
                        await transaction.ExecuteAsync(MarketDataDbSql.InsertMarketDataImportOwnership, [Text("treasury-curve"),Text(key),Uuid(commandId),Boolean(true),TimestampTz(acceptedAtUtc)],token)
                .ConfigureAwait(false);
                    }
                }
                var values = new InsertYieldCurveRate(1,row.ValueDate,row.OneMonth,row.TwoMonth,row.ThreeMonth,row.SixMonth,row.OneYear,
                    row.TwoYear,row.ThreeYear,row.FiveYear,row.SevenYear,row.TenYear,row.TwentyYear,row.ThirtyYear) { PostgreSql = true };
                await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRate,(NpgsqlParameter[])values.Bind(),token)
                .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRateByDate,(NpgsqlParameter[])values.Bind(),token)
                .ConfigureAwait(false);
                await transaction.ExecuteAsync(MarketDataDbSql.InsertYieldCurveRateYear,[Integer(1),Integer(row.ValueDate.Year)],token)
                .ConfigureAwait(false);
            }
        }

        /// <summary>Preserves immutable option-chain parameter versions and advances the current revision.</summary>
        /// <param name="parameters">The parameters used by the persistence operation.</param>
        /// <param name="revision">The revision used by the persistence operation.</param>
        /// <param name="published">The published used by the persistence operation.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistOptionChainParametersAsync(StrategyOptionChainParameterSet parameters,
            long revision, bool published, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
            parameters.Validate();
            await ConfigurationDb.ConfigurationDbContext.WriteTypedValueAsync(transaction,TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterSetValue.From(parameters),cancellationToken).ConfigureAwait(false);
            var payload=parameters.Hash();
            await transaction.ExecuteAsync(MarketDataDbSql.InsertOptionChainParameterVersion, Values(Uuid(parameters.ParameterSetId), Integer(parameters.Version), Text(payload)), cancellationToken)
                .ConfigureAwait(false);
            var stored = await transaction.ScalarAsync(MarketDataDbSql.GetOptionChainParameterVersion, Values(Uuid(parameters.ParameterSetId), Integer(parameters.Version)), cancellationToken)
                .ConfigureAwait(false);
            if (stored is not string content || !string.Equals(content, parameters.Hash(), StringComparison.Ordinal))
                throw new InvalidDataException("An immutable option-chain parameter version has conflicting content.");
            await transaction.ExecuteAsync(MarketDataDbSql.UpsertOptionChainParameterCurrent, Values(Text(parameters.Environment), Uuid(parameters.ParameterSetId), Integer(parameters.Version), Bigint(revision), Boolean(published), Text(payload)), cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Appends immutable volatility observation evidence and its dated history row.</summary>
        /// <param name="environment">The environment used by the persistence operation.</param>
        /// <param name="observation">The observation used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task AppendOptionIvObservationAsync(string environment, OptionIvObservation observation, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(observation);
            MarketDataDbContextExtensions.ValidateEnvironment(environment);
            MarketDataDbContextExtensions.ValidateObservation(observation);
            if (observation.Revision > 1)
            {
                var previous = await transaction.QueryAsync(MarketDataDbSql.SelectObservationById,transaction.BindOptionVolatilityValues(environment,observation.SupersedesObservationId!), row => MarketDataDbContextExtensions.Deserialize<OptionIvObservation>(row.GetFieldValue<byte[]>(0)),token)
                .ConfigureAwait(false);
                if (previous.Count == 0 || previous[0].Series != observation.Series || previous[0].ExchangeValueDate != observation.ExchangeValueDate
                    || !string.Equals(previous[0].SamplingSlot,observation.SamplingSlot,StringComparison.Ordinal) || previous[0].Revision >= observation.Revision)
                    throw new InvalidOperationException("An observation revision must supersede accepted immutable evidence.");
            }
            var payload = MarketDataDbContextExtensions.Serialize(observation);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertObservationById,transaction.BindOptionVolatilityValues(environment,observation.ObservationId,payload),token)
                .ConfigureAwait(false);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertObservationHistory,transaction.BindOptionVolatilityValues(environment,observation.Series.SeriesId,observation.Series.MethodologyVersion,
                VolatilityCalendarBucket.From(observation.ExchangeValueDate).Value,observation.ExchangeValueDate,observation.SamplingSlot,observation.Revision,observation.ObservationId,observation.AvailableAtUtc.UtcDateTime,payload),token)
                .ConfigureAwait(false);
        }

        /// <summary>Persists accepted volatility evidence, metric history and the monotonic latest pointer atomically.</summary>
        /// <param name="publication">The publication used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PublishOptionIvAsync(OptionIvPublication publication, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(publication);
            MarketDataDbContextExtensions.ValidatePublication(publication);
            var metric = publication.Metric;
            var snapshot = metric.Snapshot;
            var scope = string.Join(":",publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion);
            await transaction.ExecuteAsync(MarketDataDbSql.AcquireMarketDataIdentityLock,["option-volatility:"+scope],token)
                .ConfigureAwait(false);
            foreach(var observation in publication.SourceObservations.OrderBy(row => row.Revision).ThenBy(row => row.ObservationId,StringComparer.Ordinal))
                await MarketDataDbContextExtensions.AppendOptionIvObservationAsync(transaction,publication.Environment,observation,token)
                .ConfigureAwait(false);
            if (metric.Revision > 1)
            {
                var previous = await transaction.QueryAsync(MarketDataDbSql.SelectSnapshot,transaction.BindOptionVolatilityValues(publication.Environment,metric.SupersedesSnapshotId!), row => MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>(row.GetFieldValue<byte[]>(2)),token)
                .ConfigureAwait(false);
                if (previous.Count == 0 || previous[0].Snapshot.Series != snapshot.Series
                    || !string.Equals(previous[0].Snapshot.MetricPolicyVersion,snapshot.MetricPolicyVersion,StringComparison.Ordinal) || previous[0].Revision >= metric.Revision)
                    throw new InvalidOperationException("A metric revision must supersede accepted evidence in the same policy.");
            }
            var payload = MarketDataDbContextExtensions.Serialize(metric);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertSnapshot,transaction.BindOptionVolatilityValues(publication.Environment,snapshot.SnapshotId,snapshot.SnapshotDigest,metric.PublicationSequence,payload),token)
                .ConfigureAwait(false);
            await MarketDataDbContextExtensions.WriteImmutableVolatilityEvidenceAsync(transaction,MarketDataDbSql.InsertMetricHistory,transaction.BindOptionVolatilityValues(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion,
                VolatilityCalendarBucket.From(snapshot.ExchangeValueDate).Value,snapshot.ExchangeValueDate,snapshot.SamplingSlot,metric.Revision,snapshot.SnapshotId,snapshot.AvailableAtUtc.UtcDateTime,metric.PublicationSequence,payload),token)
                .ConfigureAwait(false);
            var pointer = MarketDataDbContextExtensions.Pointer(publication);
            var existing = await transaction.QueryAsync(MarketDataDbSql.SelectLatest,transaction.BindOptionVolatilityValues(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion), row => (Id:row.GetString(0),Digest:row.GetString(1),Sequence:row.GetInt64(2),Available:row.GetDateTime(3)),token)
                .ConfigureAwait(false);
            if (existing.Count != 0 && existing[0].Sequence == metric.PublicationSequence
                && (!string.Equals(existing[0].Id,snapshot.SnapshotId,StringComparison.Ordinal) || !string.Equals(existing[0].Digest,snapshot.SnapshotDigest,StringComparison.Ordinal) || existing[0].Available != pointer.AvailableAtUtc.UtcDateTime))
                throw new InvalidOperationException("Publication sequence conflicts with a different latest pointer.");
            await transaction.ExecuteAsync(MarketDataDbSql.InsertLatest,transaction.BindOptionVolatilityValues(publication.Environment,snapshot.Series.SeriesId,snapshot.Series.MethodologyVersion,snapshot.MetricPolicyVersion, snapshot.SnapshotId,snapshot.SnapshotDigest,metric.PublicationSequence,pointer.AvailableAtUtc.UtcDateTime),token)
                .ConfigureAwait(false);
        }

        /// <summary>Reads a bounded page of observation revisions eligible for the requested point in time.</summary>
        /// <param name="request">The request used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public Task<VolatilityPage<OptionIvObservation>> ReadOptionIvObservationHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken token = default)
            => MarketDataDbContextExtensions.ReadOptionIvHistoryAsync(transaction,request,false,MarketDataDbContextExtensions.Deserialize<OptionIvObservation>,token);

        /// <summary>Reads a bounded page of metric revisions eligible for the requested point in time.</summary>
        /// <param name="request">The request used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public Task<VolatilityPage<OptionIvMetricRevision>> ReadOptionIvMetricHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken token = default)
            => MarketDataDbContextExtensions.ReadOptionIvHistoryAsync(transaction,request,true,MarketDataDbContextExtensions.Deserialize<OptionIvMetricRevision>,token);

        /// <summary>Reads immutable volatility history using a query-bound continuation cursor.</summary>
        /// <param name="request">The request used by the persistence operation.</param>
        /// <param name="metric">The metric used by the persistence operation.</param>
        /// <param name="decode">The decode used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <typeparam name="T">The persisted evidence type.</typeparam>
        /// <returns>The persistence operation result.</returns>
        internal async Task<VolatilityPage<T>> ReadOptionIvHistoryAsync<T>(VolatilityHistoryPageRequest request, bool metric, Func<byte[],T> decode, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(request);
            MarketDataDbContextExtensions.ValidatePage(request);
            var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { PagingState = null, PageSize = 0 })));
            VolatilityHistoryCursor? cursor = null;
            if (request.PagingState is not null)
            {
                try { cursor = JsonSerializer.Deserialize<VolatilityHistoryCursor>(request.PagingState); }
                catch (JsonException exception) { throw new ArgumentException("Invalid volatility history cursor.",nameof(request),exception); }
                if (cursor is null || !string.Equals(cursor.Fingerprint,fingerprint,StringComparison.Ordinal) || cursor.Metric != metric)
                    throw new ArgumentException("Volatility history cursor belongs to a different query.",nameof(request));
            }
            List<NpgsqlParameter> values = [Text(request.Scope.Environment),Text(request.Scope.Series.SeriesId),Text(request.Scope.Series.MethodologyVersion)];
            if (metric) values.Add(Text(request.Scope.MetricPolicyVersion));
            values.AddRange([Integer(request.Bucket.Value),Date(request.FromValueDate),Date(request.ToValueDate),Text(request.SamplingSlot),TimestampTz(request.Mode == VolatilityHistoricalMode.AsKnown ? request.KnownAtUtc!.Value.UtcDateTime : null),Date(cursor?.Date),Text(cursor?.Slot),Integer(request.PageSize + 1)]);
            // Table and column fragments are fixed by the repository, never supplied by the caller.
            var sql = metric ? MarketDataDbSql.GetOptionIvMetricHistory : MarketDataDbSql.GetOptionIvObservationHistory;
            var rows = await transaction.QueryAsync(sql,[..values],row => (Date:row.GetFieldValue<DateOnly>(0),Slot:row.GetString(1),Payload:row.GetFieldValue<byte[]>(2)),token)
                .ConfigureAwait(false);
            var visible = rows.Take(request.PageSize).ToArray();
            var next = rows.Count > request.PageSize ? JsonSerializer.SerializeToUtf8Bytes(new VolatilityHistoryCursor(fingerprint,metric,visible[^1].Date,visible[^1].Slot)) : null;
            return new(visible.Select(row => decode(row.Payload)).ToImmutableArray(),next);
        }

        /// <summary>Accepts identical volatility evidence and rejects conflicting facts for the same identity.</summary>
        /// <param name="sql">The sql used by the persistence operation.</param>
        /// <param name="values">The values used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        internal async Task WriteImmutableVolatilityEvidenceAsync(string sql, object?[] values, CancellationToken token)
        {
            if (await transaction.ScalarAsync(sql,values,token)
                .ConfigureAwait(false) is null)
                throw new InvalidOperationException("Volatility evidence identity conflicts with different immutable facts.");
        }

        /// <summary>Rebuilds signal lookups and quarantines malformed identities without deleting source rows.</summary>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task<FuturesTradeSignalRepairReadModel> RebuildTradeSignalIndexesAsync(CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            await transaction.ExecuteAsync(MarketDataDbSql.LockTradeSignalIndexes,[],token)
                .ConfigureAwait(false);
            var payloads = await transaction.QueryAsync(MarketDataDbSql.ReadTradeSignalRepairRows,[],reader => reader.GetString(0),token)
                .ConfigureAwait(false);
            var latest = new Dictionary<string,FuturesTradeSignalRepairRow>(StringComparer.Ordinal);
            var daily = new Dictionary<(string,DateOnly,string),FuturesTradeSignalRepairRow>();
            long valid = 0,quarantined = 0;
            foreach (var payload in payloads)
            {
                var parsed = MarketDataDbContextExtensions.ParseFuturesTradeSignalRepairRow(payload);
                if (parsed.Row is not { } row)
                {
                    quarantined++;
                    var values = new InsertFuturesTradeSignalQuarantine(MarketDataDbContextExtensions.Fingerprint(payload),payload,parsed.Error ?? "Malformed identity",DateTime.UtcNow) { PostgreSql = true };
                    await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalQuarantine,(NpgsqlParameter[])values.Bind(),token)
                .ConfigureAwait(false);
                    continue;
                }
                valid++;
                if (!latest.TryGetValue(row.TimePeriod,out var current) || MarketDataDbContextExtensions.IsNewer(row,current)) latest[row.TimePeriod] = row;
                var key = (row.TimePeriod,row.ValueDate,row.ContractId);
                if (!daily.TryGetValue(key,out var dated) || MarketDataDbContextExtensions.IsNewer(row,dated)) daily[key] = row;
            }
            await transaction.ExecuteAsync(MarketDataDbSql.ClearTradeSignalLookup,[],token)
                .ConfigureAwait(false);
            var indexes = latest.Values.Select(row => new InsertFuturesTradeSignalIndex($"latest:{row.TimePeriod}","latest",row.SequenceId,row.ContractId,row.ValueDate,row.TimePeriod) { PostgreSql = true })
                .Concat(daily.Values.Select(row => new InsertFuturesTradeSignalIndex($"date:{row.TimePeriod}:{row.ValueDate.DayNumber}",row.ContractId,row.SequenceId,row.ContractId,row.ValueDate,row.TimePeriod) { PostgreSql = true })).ToArray();
            foreach (var index in indexes)
                await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(NpgsqlParameter[])index.Bind(),token)
                .ConfigureAwait(false);
            return new(payloads.Count,valid,quarantined,indexes.Length);
        }

        /// <summary>Writes the accepted live-feed enabled state.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistTradeLiveFeedAsync(TradeLiveFeedTurnedOnEvent source,CancellationToken token)
        {
            var parameters = new InsertTradeLiveFeed(source.OrderId,source.TradeId,TradeLiveFeedStateType.On.ToStringFast()) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertTradeLiveFeed,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Removes the accepted live-feed subscription state.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task DeleteTradeLiveFeedAsync(TradeLiveFeedTurnedOffEvent source,CancellationToken token)
        {
            var parameters = new DeleteTradeLiveFeed(source.OrderId,source.TradeId) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.DeleteTradeLiveFeed,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Persists the raw EOD facts carried by the accepted source event.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedRawEodAsync(FuturesEodDataInsertedEvent source,CancellationToken token)
        {
            var value = source.RawObservation ?? throw new InvalidOperationException("This older EOD source lacks accepted session facts; use a verified baseline.");
            var parameters = new HistoricalRawEodParameter(value) { PostgreSql = true };
            _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertRawEod,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Persists the resolved VX snapshot carried by the accepted source event.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedVixEodAsync(VixFuturesEodDataInsertedEvent source,CancellationToken token)
        {
            var value = source.AcceptedSnapshot ?? throw new InvalidOperationException("This older VX source lacks accepted replacement facts; use a verified baseline.");
            var parameters = new InsertVixFuturesEodData(value.ContractId,value.ValueDate,value.OpenPrice,value.HighPrice,value.LowPrice,value.ClosePrice,value.Volume) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertVixFuturesEodData,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes an accepted futures bar.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesBarAsync(FuturesBarDataInsertedEvent source,CancellationToken token)
        {
            var value = source.FuturesBarData;
            var parameters = new InsertFuturesBarData(value.ContractId,value.Symbol,value.ValueDate,value.BarDate,value.BarRateType.ToStringFast(),value.BarValue,value.UpTrendTrigger,value.DownTrendTrigger) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesBarData,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Deletes the identified futures bar.</summary>
        /// <param name="source">The source used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task DeleteAcceptedFuturesBarAsync(FuturesBarDataDeletedEvent source,CancellationToken token)
        {
            var value = source.BarDataId;
            var parameters = new DeleteFuturesBarData(value.ContractId,value.Symbol,value.ValueDate) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.DeleteFuturesBarData,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes an accepted session bar and its supplied daily EOD observation.</summary>
        /// <param name="bar">The bar used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <param name="eod">The accepted daily observation, or null when only a session bar is written.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistSessionBarFactsAsync(FuturesTradeSessionBarReadModel bar, FuturesEodObservationReadModel? eod, CancellationToken token)
        {
            var parameters = new HistoricalObservationParameter(bar) { PostgreSql = true };
            _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertObservation,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
            if (eod is null) return;
            var raw = new HistoricalRawEodParameter(eod) { PostgreSql = true };
            _ = await transaction.ScalarAsync(MarketDataDbSql.TryInsertRawEod,(Npgsql.NpgsqlParameter[])raw.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes the accepted dated closing price.</summary>
        /// <param name="accepted">The accepted used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedClosingPriceAsync(FuturesClosingPriceInsertedEvent accepted, CancellationToken token)
        {
            var closing = new InsertFuturesClosingPrice(accepted.FuturesClosingPriceId.ContractId, accepted.FuturesClosingPriceId.ValueDate,
                accepted.ClosingPrice, accepted.CreatedOn, accepted.CreatedBy) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesClosingPrice,(Npgsql.NpgsqlParameter[])closing.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes the accepted outlook snapshot with its source-event revision.</summary>
        /// <param name="accepted">The accepted used by the persistence operation.</param>
        /// <param name="eventId">The eventId used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedMarketOutlookAsync(MarketOutlookSnapshotInsertedEvent accepted, long eventId, CancellationToken token)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
            var snapshot = accepted.MarketOutlook;
            var parameters = new UpsertMarketOutlookSnapshot(snapshot.ContractId, snapshot.ValueDate, eventId, snapshot.UpdatedAtUtc,
                MessagePackSerializer.Serialize(snapshot.FuturesEodData,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token),
                snapshot.FuturesTradeSignal is null ? null : MessagePackSerializer.Serialize(snapshot.FuturesTradeSignal,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token),
                snapshot.MissingInputs,MessagePackSerializer.Serialize(snapshot,MarketDataDbContext.MarketOutlookSerializerOptions,cancellationToken:token)) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.UpsertMarketOutlookSnapshot,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Adx signal values without recalculating them.</summary>
        /// <param name="futuresAdxSignal">The futuresAdxSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesAdxSignalAsync(FuturesAdxSignalReadModel futuresAdxSignal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(futuresAdxSignal);
            var parameters = new InsertFuturesAdxSignal(contractId: futuresAdxSignal.ContractId, valueDate: futuresAdxSignal.ValueDate, timePeriod: futuresAdxSignal.TimePeriod.ToStringFast(), periodLength: futuresAdxSignal.PeriodLength, timestamp: futuresAdxSignal.Timestamp, futuresPrice: futuresAdxSignal.FuturesPrice, plusDI: futuresAdxSignal.PlusDI, minusDI: futuresAdxSignal.MinusDI, adxValue: futuresAdxSignal.AdxValue, adx: futuresAdxSignal.ADX.ToStringFast(), adxStrength: futuresAdxSignal.ADXStrength.ToStringFast(), configurationId: futuresAdxSignal.Metadata?.CalculationConfigurationId, observationId: futuresAdxSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAdxSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAdxSignal.Metadata?.SourceSequence, calculationVersion: futuresAdxSignal.Metadata?.CalculationVersion, calculationMethod: futuresAdxSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAdxSignal.Metadata is { } metadata ? metadata.SchemaVersion : null, isValid: futuresAdxSignal.Metadata?.IsValid, isWarm: futuresAdxSignal.IsWarm) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesAdxSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Atr signal values without recalculating them.</summary>
        /// <param name="futuresAtrSignal">The futuresAtrSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesAtrSignalAsync(FuturesAtrSignalReadModel futuresAtrSignal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(futuresAtrSignal);
            var parameters = new InsertFuturesAtrSignal(contractId: futuresAtrSignal.ContractId, valueDate: futuresAtrSignal.ValueDate, timePeriod: futuresAtrSignal.TimePeriod.ToStringFast(), periodLength: futuresAtrSignal.PeriodLength, timestamp: futuresAtrSignal.Timestamp, futuresPrice: futuresAtrSignal.FuturesPrice, atrValue: futuresAtrSignal.AtrValue, trueRange: futuresAtrSignal.TrueRange, atr: futuresAtrSignal.ATR.ToStringFast(), atrStrength: futuresAtrSignal.ATRStrength.ToStringFast(), configurationId: futuresAtrSignal.Metadata?.CalculationConfigurationId, observationId: futuresAtrSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresAtrSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresAtrSignal.Metadata?.SourceSequence, calculationVersion: futuresAtrSignal.Metadata?.CalculationVersion, calculationMethod: futuresAtrSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresAtrSignal.Metadata is { } atrMetadata ? atrMetadata.SchemaVersion : null, isValid: futuresAtrSignal.Metadata?.IsValid, previousAtrValue: futuresAtrSignal.PreviousAtrValue, atrBaseline: futuresAtrSignal.AtrBaseline, atrRatio: futuresAtrSignal.AtrRatio, isWarm: futuresAtrSignal.IsWarm) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesAtrSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Macd signal values without recalculating them.</summary>
        /// <param name="futuresMacdSignal">The futuresMacdSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesMacdSignalAsync(FuturesMacdSignalReadModel futuresMacdSignal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(futuresMacdSignal);
            var parameters = new InsertFuturesMacdSignal(contractId: futuresMacdSignal.ContractId, valueDate: futuresMacdSignal.ValueDate, timePeriod: futuresMacdSignal.TimePeriod.ToStringFast(), signalEmaPeriod: futuresMacdSignal.SignalEmaPeriod, fastEmaPeriod: futuresMacdSignal.FastEmaPeriod, slowEmaPeriod: futuresMacdSignal.SlowEmaPeriod, timestamp: futuresMacdSignal.Timestamp, futuresPrice: futuresMacdSignal.FuturesPrice, fastEma: futuresMacdSignal.FastEma, slowEma: futuresMacdSignal.SlowEma, macdLine: futuresMacdSignal.MacdLine, signalLine: futuresMacdSignal.SignalLine, histogram: futuresMacdSignal.Histogram, macd: futuresMacdSignal.MACD.ToStringFast(), macdStrength: futuresMacdSignal.MACDStrength.ToStringFast(), configurationId: futuresMacdSignal.Metadata?.CalculationConfigurationId, observationId: futuresMacdSignal.Metadata?.ObservationId.Value, marketDataAsOf: futuresMacdSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime, sourceSequence: futuresMacdSignal.Metadata?.SourceSequence, calculationVersion: futuresMacdSignal.Metadata?.CalculationVersion, calculationMethod: futuresMacdSignal.Metadata?.CalculationMethod.ToString(), schemaVersion: futuresMacdSignal.Metadata is { } macdMetadata ? macdMetadata.SchemaVersion : null, isValid: futuresMacdSignal.Metadata?.IsValid, isWarm: futuresMacdSignal.IsWarm, observationCount: futuresMacdSignal.ObservationCount) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesMacdSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Rsi signal values without recalculating them.</summary>
        /// <param name="e">The e used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesRsiSignalAsync(FuturesRsiSignalReadModel e, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(e);
            var parameters = new InsertFuturesRsiSignal(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), e.PeriodLength, e.Timestamp, e.Price, e.PriceChange, e.PriceGain, e.PriceLoss, e.AveragePriceGain, e.AveragePriceLoss, e.RS, e.RSI, e.RSIAverage, e.RSISlope, e.SourceSequence, e.SourceEventTimestamp, e.Metadata?.CalculationConfigurationId, e.Metadata?.ObservationId.Value, e.Metadata?.MarketDataAsOfUtc.UtcDateTime, e.Metadata?.CalculationVersion, e.Metadata?.CalculationMethod.ToString(), e.Metadata is { } rsiMetadata ? rsiMetadata.SchemaVersion : null, e.Metadata?.IsValid, e.PreviousRsi, e.RegimeSlope, e.IsWarm) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesRsiSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Tdi signal values without recalculating them.</summary>
        /// <param name="futuresTdiSignal">The futuresTdiSignal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesTdiSignalAsync(FuturesTdiSignalReadModel futuresTdiSignal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(futuresTdiSignal);
            var parameters = new InsertFuturesTdiSignal(contractId: futuresTdiSignal.ContractId, timePeriod: futuresTdiSignal.TimePeriod.ToStringFast(), configurationId: futuresTdiSignal.ConfigurationId, valueDate: futuresTdiSignal.ValueDate, timestamp: futuresTdiSignal.Timestamp, schemaVersion: futuresTdiSignal.SchemaVersion, rsiPeriod: futuresTdiSignal.RsiPeriod, priceLinePeriod: futuresTdiSignal.PriceLinePeriod, signalLinePeriod: futuresTdiSignal.SignalLinePeriod, marketBasePeriod: futuresTdiSignal.MarketBasePeriod, volatilityBandPeriod: futuresTdiSignal.VolatilityBandPeriod, volatilityBandDeviation: futuresTdiSignal.VolatilityBandDeviation, price: futuresTdiSignal.Price, rsi: futuresTdiSignal.Rsi, priceLine: futuresTdiSignal.PriceLine, signalLine: futuresTdiSignal.SignalLine, marketBaseLine: futuresTdiSignal.MarketBaseLine, upperVolatilityBand: futuresTdiSignal.UpperVolatilityBand, lowerVolatilityBand: futuresTdiSignal.LowerVolatilityBand, bandWidth: futuresTdiSignal.BandWidth, priceSignalDivergence: futuresTdiSignal.PriceSignalDivergence, crossType: futuresTdiSignal.Cross.ToString(), marketState: futuresTdiSignal.MarketState.ToString(), trendDirection: futuresTdiSignal.TDI.ToStringFast(), trendStrength: futuresTdiSignal.TDIStrength.ToStringFast(), sourceSequence: futuresTdiSignal.SourceSequence, sourceEventTimestamp: futuresTdiSignal.SourceEventTimestamp) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTdiSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Ema signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesEmaSignalAsync(FuturesEmaSignalReadModel signal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(signal);
            var metadata = signal.Metadata;
            var parameters = new InsertFuturesEmaSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10, signal.PreviousEma10, signal.Ema10Slope, signal.Ema20, signal.PreviousEma20, signal.Ema20Slope, signal.Ema50, signal.PreviousEma50, signal.Ema50Slope, signal.Ema200, signal.PreviousEma200, signal.Ema200Slope, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesEmaSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Bb signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesBbSignalAsync(FuturesBbSignalReadModel signal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(signal);
            var metadata = signal.Metadata;
            var parameters = new InsertFuturesBollingerBandSignal(metadata.MarketSeriesIdentity.Format(), metadata.TimeFrame.ToString(), metadata.CalculationConfigurationId, MarketDataDbContextExtensions.Bucket(metadata.ValueDate), metadata.MarketDataAsOfUtc.UtcDateTime, metadata.ObservationId.Value, metadata.ContractId, metadata.ValueDate, signal.Price, signal.Ema10Center, signal.StandardDeviation10, signal.Upper10, signal.Lower10, signal.Width10, signal.Position10, signal.Ema20Center, signal.StandardDeviation20, signal.Upper20, signal.Lower20, signal.Width20, signal.Position20, signal.Width20Baseline, signal.Width20Ratio, signal.IsWarm, metadata.SourceSequence, metadata.CalculatedAtUtc.UtcDateTime, metadata.SchemaVersion, metadata.CalculationVersion, metadata.CalculationMethod.ToString(), metadata.IsValid) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesBollingerBandSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted Vwap signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesVwapSignalAsync(FuturesVwapSignalReadModel signal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(signal);
            var parameters = new InsertFuturesVwapSignal(signal.ContractId, signal.ValueDate, signal.ConfigurationId, signal.AsOfUtc.UtcDateTime, signal.LastTradeOrdinal, signal.SessionStartUtc.UtcDateTime, signal.SessionEndUtc.UtcDateTime, signal.CumulativePriceVolume, signal.CumulativeVolume, signal.EligibleTradeCount, signal.RejectedTradeCount, signal.LastPrice, signal.Vwap, signal.PriceMinusVwap, signal.PriceToVwapPercent, signal.LastTradeSourceSequence, signal.StreamEpochId, signal.IsWarm, signal.IsValid, signal.InvalidReason.ToString(), signal.IsTickExact, signal.CalculationMethod.ToString(), signal.SchemaVersion, signal.CalculationVersion) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesVwapSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted VxTermStructure signal values without recalculating them.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesVxTermStructureSignalAsync(FuturesVxTermStructureSignalReadModel signal, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(signal);
            var parameters = new InsertFuturesVxTermStructureSignal(signal.ValueDate, signal.ConfigurationId, signal.CalculatedAtUtc.UtcDateTime, signal.FrontSourceSequence, signal.BackSourceSequence, signal.FrontVxContractId, signal.FrontExpiry, signal.FrontVxPrice, signal.BackVxContractId, signal.BackExpiry, signal.BackVxPrice, signal.FrontBackSpread, signal.FrontBackRatio, signal.TermStructurePercent, signal.TermStructureState.ToString(), signal.PriorFrontBackRatio, signal.PriorTermStructurePercent, signal.FrontSourceTimestampUtc.UtcDateTime, signal.BackSourceTimestampUtc.UtcDateTime, signal.IsWarm, signal.IsValid, signal.SchemaVersion, signal.CalculationVersion) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesVxTermStructureSignal,(Npgsql.NpgsqlParameter[])parameters.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted trade-signal facts and their current/date lookups.</summary>
        /// <param name="FuturesTradeSignalV2ReadModel">The FuturesTradeSignalV2ReadModel used by the persistence operation.</param>
        /// <param name="eventId">The eventId used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel FuturesTradeSignalV2ReadModel, long eventId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(FuturesTradeSignalV2ReadModel);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
            var sequenceId = eventId;
            var timePeriod = FuturesTradeSignalV2ReadModel.TimePeriod.ToStringFast();
            var parameters0 = new InsertFuturesTradeSignal(contractId: FuturesTradeSignalV2ReadModel.ContractId, valueDate: FuturesTradeSignalV2ReadModel.ValueDate, timePeriod, sequenceId, timestamp: FuturesTradeSignalV2ReadModel.Timestamp, mean: FuturesTradeSignalV2ReadModel.Mean, stdDev: FuturesTradeSignalV2ReadModel.StdDev, futuresPrice: FuturesTradeSignalV2ReadModel.FuturesPrice, priceChangePercent: FuturesTradeSignalV2ReadModel.PriceChangePercent, fundRiskPercent: FuturesTradeSignalV2ReadModel.FundRiskPercent, rsi: FuturesTradeSignalV2ReadModel.RSI, rsiSlope: FuturesTradeSignalV2ReadModel.RSISlope, trendType: FuturesTradeSignalV2ReadModel.TrendType.ToStringFast(), trendStrength: FuturesTradeSignalV2ReadModel.TrendStrength.ToStringFast(), tradeSignal: FuturesTradeSignalV2ReadModel.TradeSignal.ToStringFast(), tdi: FuturesTradeSignalV2ReadModel.TDI.ToStringFast(), tdiStrength: FuturesTradeSignalV2ReadModel.TDIStrength.ToStringFast(), mdi: FuturesTradeSignalV2ReadModel.MDI, mdiTrend: FuturesTradeSignalV2ReadModel.MDITrend.ToStringFast(), mdiUpTrendLimit: FuturesTradeSignalV2ReadModel.MDIUpTrendLimit, mdiDownTrendLimit: FuturesTradeSignalV2ReadModel.MDIDownTrendLimit, upTrendingTrigger: FuturesTradeSignalV2ReadModel.UpTrendingTrigger, downTrendingTrigger: FuturesTradeSignalV2ReadModel.DownTrendingTrigger, entryTrigger: FuturesTradeSignalV2ReadModel.EntryTrigger, exitTrigger: FuturesTradeSignalV2ReadModel.ExitTrigger, trendDelta: FuturesTradeSignalV2ReadModel.TrendDelta, trendExtreme: FuturesTradeSignalV2ReadModel.TrendExtreme, trendReversal: FuturesTradeSignalV2ReadModel.TrendReversal, fiftyDma: FuturesTradeSignalV2ReadModel.FiftyDMA, twoHundredDma: FuturesTradeSignalV2ReadModel.TwoHundredDMA, tradeExecuteState: FuturesTradeSignalV2ReadModel.TradeExecuteState.ToStringFast()) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignal,(Npgsql.NpgsqlParameter[])parameters0.Bind(),token)
                .ConfigureAwait(false);
            var parameters1 = new InsertFuturesTradeSignalIndex($"latest:{timePeriod}", "latest", sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(Npgsql.NpgsqlParameter[])parameters1.Bind(),token)
                .ConfigureAwait(false);
            var parameters2 = new InsertFuturesTradeSignalIndex($"date:{timePeriod}:{FuturesTradeSignalV2ReadModel.ValueDate.DayNumber}", FuturesTradeSignalV2ReadModel.ContractId, sequenceId, FuturesTradeSignalV2ReadModel.ContractId, FuturesTradeSignalV2ReadModel.ValueDate, timePeriod) { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesTradeSignalIndex,(Npgsql.NpgsqlParameter[])parameters2.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Writes accepted ITI signal facts and the current time-frame state.</summary>
        /// <param name="signal">The signal used by the persistence operation.</param>
        /// <param name="eventId">The eventId used by the persistence operation.</param>
        /// <param name="token">Cancels the operation.</param>
        /// <returns>The persistence operation result.</returns>
        public async Task PersistAcceptedFuturesItiSignalAsync(FuturesItiSignalV2ReadModel signal, long eventId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(signal);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventId);
            var sequenceId = signal.SequenceId > 0 ? signal.SequenceId : eventId;
            var canonical = MarketDataDbContextExtensions.CreateFuturesItiSignalParameters(signal,sequenceId) with { PostgreSql = true };
            var latest = MarketDataDbContextExtensions.CreateFuturesItiTimeFrameStateParameters(signal,sequenceId) with { PostgreSql = true };
            await transaction.ExecuteAsync(MarketDataDbSql.InsertFuturesItiSignal,(Npgsql.NpgsqlParameter[])canonical.Bind(),token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(MarketDataDbSql.UpsertFuturesItiTimeFrameState,(Npgsql.NpgsqlParameter[])latest.Bind(),token)
                .ConfigureAwait(false);
        }

        /// <summary>Binds immutable volatility evidence values to PostgreSQL parameters.</summary>
        /// <param name="items">The ordered database values.</param>
        /// <returns>The typed PostgreSQL parameters.</returns>
        internal NpgsqlParameter[] BindOptionVolatilityValues(params object?[] items)
            => (NpgsqlParameter[])new OptionVolatilityParameters(items) { PostgreSql = true }.Bind();
    }
}

/// <summary>Opaque economic-calendar continuation state bound to the originating query.</summary>
/// <param name="Fingerprint">The query fingerprint used to reject tokens from other requests.</param>
/// <param name="PartitionIndex">The current country/month partition in query order.</param>
/// <param name="LastEventDateTicks">The last emitted event timestamp, or null before reading the partition.</param>
/// <param name="LastEventName">The last emitted event name used to disambiguate equal timestamps.</param>
internal sealed record CalendarPageToken(
    string Fingerprint,
    int PartitionIndex,
    long? LastEventDateTicks,
    string? LastEventName);

/// <summary>Identifies one country/month partition of the economic-calendar query.</summary>
/// <param name="CountryCode">The partition's country code.</param>
/// <param name="MonthBucket">The partition's year/month bucket.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CalendarPartition(string CountryCode, int MonthBucket);

/// <summary>Materialized ITI identity used to identify a persistence projection scope.</summary>
/// <param name="ContractId">The futures contract identifier.</param>
/// <param name="ValueDate">The observation's value date.</param>
/// <param name="IntrinsicTimeTrend">The persisted intrinsic-time trend.</param>
/// <param name="IntrinsicTimeMode">The persisted intrinsic-time mode.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct FuturesItiProjectionScopeData(
    string ContractId,
    DateOnly ValueDate,
    string IntrinsicTimeTrend,
    string IntrinsicTimeMode);

/// <summary>The accepted trade-signal identity or a parsing error used by index maintenance.</summary>
/// <param name="Row">The parsed identity, or null when the stored row is malformed.</param>
/// <param name="Error">The parsing error, or null when the identity is accepted.</param>
internal sealed record FuturesTradeSignalRepairParseResult(FuturesTradeSignalRepairRow? Row, string? Error);

/// <summary>Stored trade-signal identity used to rebuild derived lookup entries.</summary>
/// <param name="ContractId">The futures contract identifier.</param>
/// <param name="ValueDate">The signal's value date.</param>
/// <param name="TimePeriod">The persisted signal period.</param>
/// <param name="Timestamp">The signal's time within its value date.</param>
/// <param name="SequenceId">The sequence used to order signal identities.</param>
internal sealed record FuturesTradeSignalRepairRow(
    string ContractId,
    DateOnly ValueDate,
    string TimePeriod,
    TimeOnly Timestamp,
    long SequenceId);

/// <summary>Stored ownership and write permission for an idempotent market-data import.</summary>
/// <param name="CommandId">The command that owns the persisted identity.</param>
/// <param name="MayWrite">Whether that owner may persist the requested facts.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct MarketDataImportOwnership(Guid CommandId, bool MayWrite);

/// <summary>Query-bound position in an immutable volatility evidence history page.</summary>
/// <param name="Fingerprint">The originating query fingerprint.</param>
/// <param name="Metric">Whether the page contains metric rather than observation evidence.</param>
/// <param name="Date">The last emitted value date.</param>
/// <param name="Slot">The last emitted sampling slot.</param>
internal sealed record VolatilityHistoryCursor(string Fingerprint, bool Metric, DateOnly Date, string Slot);
