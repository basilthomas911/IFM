using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using MathNet.Numerics.Distributions;
using Microsoft.Extensions.Logging;
using System.Globalization;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using static TomasAI.IFM.Application.Storage.MarketDataDb.MarketDataDbContext;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

internal static class MarketDataDbContextExtensions
{
    extension(ImportDuplicatePolicy duplicatePolicy)
    {
        /// <summary>
        /// Performs the <c>ValidateImportPolicy</c> operation for MarketDataDb persistence.
        /// </summary>
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
        /// <summary>
        /// Performs the <c>EnsureImportOwnershipAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task EnsureImportOwnershipAsync(string dataset, string logicalKey, Guid commandId, bool logicalRowAlreadyExists)
        {
            var db = context._dbFactory.MarketDataDb;
            var applied = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.ClaimMarketDataImportOwnership)}", MarketDataDbCql.ClaimMarketDataImportOwnership)
                .SetParameters(new ClaimMarketDataImportOwnership(dataset, logicalKey, commandId, !logicalRowAlreadyExists, DateTime.UtcNow))
                .ExecuteScalarAsync(MapToBoolean!);
            if (applied && !logicalRowAlreadyExists)
                return;
            var owner = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataImportOwnership)}", MarketDataDbCql.GetMarketDataImportOwnership)
                .SetParameters(new GetMarketDataImportOwnership(dataset, logicalKey))
                .ExecuteSingleAsync(MapToMarketDataImportOwnership!);
            if (owner is { CommandId: var ownerCommandId, MayWrite: true } && ownerCommandId == commandId)
                return;
            throw new MarketDataImportDuplicateException($"A {dataset} row with logical key '{logicalKey}' is already owned by another import command.");
        }

        /// <summary>
        /// Performs the <c>GetProjectionGuardScopeKeys</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string[] GetProjectionGuardScopeKeys() => Enumerable.Range(0, MarketDataDbContext.ProjectionGuardScopeCount).Select(bucket => string.Concat(MarketDataDbContext.ProjectionGuardScopePrefix, bucket.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();

        /// <summary>
        /// Performs the <c>GetProjectionStateAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<MarketDataProjectionStateReadModel?> GetProjectionStateAsync(string projectionName) => await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionState)}", MarketDataDbCql.GetMarketDataProjectionState)
            .SetParameters(new GetMarketDataProjectionState(projectionName)).ExecuteSingleAsync<MarketDataProjectionStateReadModel?>(static row => MarketDataDbContext.MapToProjectionState(row));

        /// <summary>
        /// Performs the <c>HasProjectionMutationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<bool> HasProjectionMutationAsync(string projectionName) => (await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMutation)}", MarketDataDbCql.GetMarketDataProjectionMutation)
            .SetParameters(new GetMarketDataProjectionMutation(projectionName))
            .ExecuteQueryAsync(MarketDataDbContext.MapToGuid)).Count != 0;

        /// <summary>
        /// Performs the <c>GetProjectionReadGenerationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<Guid?> GetProjectionReadGenerationAsync(string projectionName)
        {
            var state = await context.GetProjectionStateAsync(projectionName);
            if (state is null || !state.Value.IsReady || await context.HasProjectionMutationAsync(projectionName))
                return null;
            return state.Value.Generation;
        }

        /// <summary>
        /// Performs the <c>IsProjectionReadGenerationValidAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<bool> IsProjectionReadGenerationValidAsync(string projectionName, Guid generation)
        {
            var state = await context.GetProjectionStateAsync(projectionName);
            return state is { IsReady: true } && state.Value.Generation == generation && !await context.HasProjectionMutationAsync(projectionName);
        }

        /// <summary>
        /// Performs the <c>GetProjectionScopeStatesAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<Dictionary<string, MarketDataProjectionScopeStateReadModel?>> GetProjectionScopeStatesAsync(string projectionName, IReadOnlyList<string> scopeKeys)
        {
            var states = new Dictionary<string, MarketDataProjectionScopeStateReadModel?>(scopeKeys.Count, StringComparer.Ordinal);
            for (var offset = 0; offset < scopeKeys.Count; offset += MarketDataDbContext.ProjectionScopeStateReadBatchSize)
            {
                var count = Math.Min(MarketDataDbContext.ProjectionScopeStateReadBatchSize, scopeKeys.Count - offset);
                var keys = scopeKeys.Skip(offset).Take(count).ToArray();
                foreach (var key in keys)
                    states.Add(key, null);
                var values = await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)}", MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
                    .SetParameters(new GetMarketDataProjectionScopeStatesV3(projectionName, keys))
                    .ExecuteQueryAsync(MarketDataDbContext.MapToProjectionScopeState);
                foreach (var value in values)
                    states[value.ScopeKey] = value;
            }

            return states;
        }

        /// <summary>
        /// Performs the <c>GetProjectionScopeReadStampAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<MarketDataProjectionScopeReadStamp?> GetProjectionScopeReadStampAsync(string projectionName, IEnumerable<string> scopeKeys)
        {
            var scopes = MarketDataDbContextExtensions.AddProjectionGuardScopes(scopeKeys);
            var globalGeneration = await context.GetProjectionReadGenerationAsync(projectionName);
            if (globalGeneration is null)
                return null;
            var states = await context.GetProjectionScopeStatesAsync(projectionName, scopes);
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
        internal async Task<bool> IsProjectionScopeReadStampValidAsync(MarketDataProjectionScopeReadStamp stamp)
        {
            if (!await context.IsProjectionReadGenerationValidAsync(stamp.ProjectionName, stamp.GlobalGeneration))
            {
                return false;
            }

            var states = await context.GetProjectionScopeStatesAsync(stamp.ProjectionName, stamp.Scopes.Select(static scope => scope.ScopeKey).ToArray());
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
        internal async Task ExecuteMaintainedProjectionMutationAsync(string projectionName, IEnumerable<string> scopeKeys, Func<Task> mutation)
        {
            var scopes = MarketDataDbContextExtensions.AddProjectionGuardScopes(scopeKeys);
            if (scopes.Length == 0)
            {
                await mutation();
                return;
            }

            var globalGeneration = await context.GetProjectionReadGenerationAsync(projectionName);
            var initialStates = await context.GetProjectionScopeStatesAsync(projectionName, scopes);
            var restorableScopes = globalGeneration is null ? new HashSet<string>(StringComparer.Ordinal) : scopes.Where(scope => initialStates[scope] is null || initialStates[scope]!.Value.CanRead).ToHashSet(StringComparer.Ordinal);
            var mutationId = Guid.NewGuid();
            var activeOperations = new HashSet<Guid>
            {
                mutationId
            };
            var db = context._dbFactory.MarketDataDb;
            var scopeActivationAcknowledged = false;
            var mutationSubmissionStarted = false;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                .SetParameters(scopes.Select(scope => new InsertMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId, DateTime.UtcNow)))
                .ExecuteCommandAsync();
            try
            {
                async Task ActivateScopesAsync() => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)
                    .SetParameters(scopes.Select(scope => new BeginMarketDataProjectionScopeOperationV3(projectionName, scope, mutationId, activeOperations)))
                    .ExecuteCommandAsync();
                if (context.MaintainedProjectionScopeActivationForTestingAsync is { } scopeActivation)
                    await scopeActivation(ActivateScopesAsync);
                else
                    await ActivateScopesAsync();
                scopeActivationAcknowledged = true;
                mutationSubmissionStarted = true;
                if (context.MaintainedProjectionMutationSubmittingForTestingAsync is { } mutationSubmitting)
                    await mutationSubmitting();
                await mutation();
                var globalStillValid = globalGeneration.HasValue && await context.IsProjectionReadGenerationValidAsync(projectionName, globalGeneration.Value);
                var scopesToEnd = new List<string>();
                foreach (var scopeBatch in scopes.Chunk(MarketDataDbContext.ProjectionReadConcurrency))
                {
                    var completions = scopeBatch.Select(async scope =>
                    {
                        if (!globalStillValid || !restorableScopes.Contains(scope))
                            return (Scope: scope, Completed: false);
                        var completed = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)
                            .SetParameters(new CompleteMarketDataProjectionScopeOperationV3(projectionName, scope, mutationId, activeOperations, DateTime.UtcNow, activeOperations))
                            .ExecuteSingleAsync(MarketDataDbContext.MapToBoolean) == true;
                        return (Scope: scope, Completed: completed);
                    }).ToArray();
                    foreach (var completion in await Task.WhenAll(completions))
                    {
                        if (!completion.Completed)
                            scopesToEnd.Add(completion.Scope);
                    }
                }

                await MarketDataDbContextExtensions.EndProjectionScopeOperationsAsync(db, projectionName, scopesToEnd, activeOperations);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                    .SetParameters(scopes.Select(scope => new DeleteMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId)))
                    .ExecuteCommandAsync();
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
                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)
                        .SetParameters(scopes.Select(scope => new FailMarketDataProjectionScopeMutationV3(projectionName, scope, mutationId, DateTime.UnixEpoch)))
                        .ExecuteCommandAsync();
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
        internal async Task ExecuteAtomicTickWriteAsync(string scopeKey, IReadOnlyCollection<InsertFuturesTickData> canonicalRows, IReadOnlyCollection<InsertFuturesTickDataByTime> projectionRows)
        {
            if (canonicalRows.Count == 0)
                return;
            if (canonicalRows.Count != projectionRows.Count || canonicalRows.Count > MarketDataDbContext.TickAtomicBatchRowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(canonicalRows), canonicalRows.Count, $"Atomic tick writes require matching collections of at most {MarketDataDbContext.TickAtomicBatchRowCount} rows.");
            }

            await context.ExecuteGuardedAtomicTickMutationAsync(scopeKey, db => [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickData)}", MarketDataDbCql.InsertFuturesTickData)
                .SetParameters(canonicalRows)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickDataByTime)}", MarketDataDbCql.InsertFuturesTickDataByTime)
                .SetParameters(projectionRows)
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)}", MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)
                .SetParameters(new MarkMarketDataProjectionScopeAtomicWriteV3(MarketDataDbContext.FuturesTickByTimeProjection, scopeKey, Guid.NewGuid()))
                .QueueCommand()]);
        }

        /// <summary>
        /// Performs the <c>ExecuteGuardedAtomicTickMutationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
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
                .QueueCommand()];
            try
            {
                async Task RegisterGuardAsync() => await db.ExecuteQueuedCommandsAsync(registrationCommands, useTransaction: true);
                if (context.TickProjectionGuardRegistrationForTestingAsync is { } registration)
                    await registration(RegisterGuardAsync);
                else
                    await RegisterGuardAsync();
            }
            catch
            {
                // A timed-out logged registration batch may still be replayed server-side.
                // Preserve its original journal timestamp so automatic recovery cannot
                // remove the marker before a delayed guard activation is applied.
                await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.RegistrationResponseUnknown);
                throw;
            }

            if (context.TickProjectionGuardRegisteredForTestingAsync is { } guardRegistered)
            {
                try
                {
                    await guardRegistered();
                }
                catch
                {
                    // No data request has started, so this operation is safe for automatic
                    // recovery even if its registration response was delayed.
                    await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission);
                    throw;
                }
            }

            try
            {
                await db.ExecuteQueuedCommandsAsync(createDataCommands(db), useTransaction: true);
            }
            catch
            {
                // A logged data batch timeout is ambiguous: batchlog replay can still apply
                // canonical data after this catch. Keep the original nonfailed journal row
                // and active guard ID. Only an explicit cutoff after writers are drained may
                // reclaim it; automatic failed-operation recovery would reopen a race.
                await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.DataBatchResponseUnknown);
                throw;
            }

            bool guardCompleted;
            try
            {
                guardCompleted = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionGuardOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionGuardOperationV3)
                    .SetParameters(new CompleteMarketDataProjectionGuardOperationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, Guid.NewGuid(), activeOperations, DateTime.UtcNow, activeOperations))
                    .ExecuteSingleAsync(MarketDataDbContext.MapToBoolean) == true;
            }
            catch
            {
                await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged);
                throw;
            }

            if (guardCompleted)
            {
                try
                {
                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                        .SetParameters(new DeleteMarketDataProjectionScopeMutationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, operationId))
                        .ExecuteCommandAsync();
                }
                catch
                {
                    // The data and guard are committed. Retain a recoverable failed marker
                    // instead of making the caller retry a successful tick write.
                    await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged);
                }

                return;
            }

            MarketDataProjectionScopeStateReadModel? guardState;
            try
            {
                var states = await context.GetProjectionScopeStatesAsync(MarketDataDbContext.FuturesTickByTimeProjection, new[] { guardScopeKey });
                guardState = states[guardScopeKey];
            }
            catch
            {
                await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged);
                return;
            }

            if (guardState is { Blocked: true })
            {
                // Backfill owns the guard. Leaving this ID active makes its conditional
                // release fail; the failed marker lets the next repair reclaim it exactly.
                await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged);
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
                    .QueueCommand()];
                await db.ExecuteQueuedCommandsAsync(cleanupCommands, useTransaction: true);
            }
            catch
            {
                await MarketDataDbContextExtensions.TryClassifyTickGuardOperationFailureAsync(db, guardScopeKey, operationId, TickProjectionGuardFailureStage.AfterDataAcknowledged);
            }
        }

        /// <summary>
        /// Performs the <c>UpsertFuturesEodProjectionAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task UpsertFuturesEodProjectionAsync(FuturesEodDataV2ReadModel e, decimal openPrice)
        {
            var db = context._dbFactory.MarketDataDb;
            var yearMonth = MarketDataDbContextExtensions.ToYearMonth(e.ValueDate);
            List<object> commands = [db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                .SetParameters(MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(e, openPrice))
                .QueueCommand(), db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(new InsertMarketDataProjectionMonth(MarketDataDbContext.FuturesEodProjection, yearMonth))
                .QueueCommand()];
            await db.ExecuteQueuedCommandsAsync(commands);
        }

        /// <summary>
        /// Performs the <c>UpsertVixFuturesContractIndexAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task UpsertVixFuturesContractIndexAsync(string contractId) => await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertVixFuturesContractIndex)}", MarketDataDbCql.InsertVixFuturesContractIndex)
            .SetParameters(new InsertVixFuturesContractIndex(MarketDataDbContextExtensions.GetVixContractBucket(contractId), contractId))
            .ExecuteCommandAsync();

        /// <summary>
        /// Performs the <c>InsertFuturesEodBatchAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task InsertFuturesEodBatchAsync(ICollection<FuturesEodDataV2ReadModel> batch)
        {
            if (batch.Count == 0)
                return;
            MarketDataDbContextExtensions.EnsureDistinctFuturesEodWrites(batch);
            await context.ExecuteMaintainedProjectionMutationAsync(MarketDataDbContext.FuturesEodProjection, batch.Select(static e => MarketDataDbContextExtensions.GetFuturesEodScopeKey(e.ValueDate)), async () =>
            {
                var db = context._dbFactory.MarketDataDb;
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodData)}", MarketDataDbCql.InsertFuturesEodData)
                    .SetParameters(batch.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataParameters(e, e.OpenPrice)))
                    .ExecuteCommandAsync();
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                    .SetParameters(batch.Select(e => MarketDataDbContextExtensions.CreateFuturesEodDataByMonthParameters(e, e.OpenPrice)))
                    .ExecuteCommandAsync();
                var projectionMonths = batch.Select(e => MarketDataDbContextExtensions.ToYearMonth(e.ValueDate)).Distinct().ToArray();
                if (context.FuturesEodProjectionMonthSubmittingForTestingAsync is { } projectionMonthSubmitting)
                    await projectionMonthSubmitting();
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                    .SetParameters(projectionMonths.Select(yearMonth => new InsertMarketDataProjectionMonth(MarketDataDbContext.FuturesEodProjection, yearMonth)))
                    .ExecuteCommandAsync();
            });
        }

        /// <summary>
        /// Performs the <c>ReadLegacyCurrentFuturesEodDataAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<FuturesEodDataV2ReadModel?> ReadLegacyCurrentFuturesEodDataAsync(DateOnly valueDate)
        {
            FuturesEodDataV2ReadModel? latest = null;
            await foreach (var row in context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
                .ExecuteStreamAsync(MarketDataDbContext.MapToFuturesEodData!))
            {
                if (row.ValueDate > valueDate)
                    continue;
                if (latest is null || row.ValueDate > latest.ValueDate || row.ValueDate == latest.ValueDate && string.CompareOrdinal(row.ContractId, latest.ContractId) < 0 || row.ValueDate == latest.ValueDate && row.ContractId == latest.ContractId && string.CompareOrdinal(row.Symbol, latest.Symbol) < 0)
                {
                    latest = row;
                }
            }

            return latest;
        }

        /// <summary>
        /// Performs the <c>ReadLegacyFuturesEodDataByMonthsAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<ICollection<FuturesEodDataV2ReadModel>> ReadLegacyFuturesEodDataByMonthsAsync(DateOnly startDate, DateOnly endDate, IReadOnlySet<int> yearMonths)
        {
            List<FuturesEodDataV2ReadModel> results = [];
            await foreach (var row in context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
                .ExecuteStreamAsync(MarketDataDbContext.MapToFuturesEodData!))
            {
                if (row.ValueDate >= startDate && row.ValueDate <= endDate && yearMonths.Contains(MarketDataDbContextExtensions.ToYearMonth(row.ValueDate)))
                    results.Add(row);
            }

            return results;
        }

        /// <summary>
        /// Performs the <c>ReadLegacyVixFuturesEodDataByValueDateAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<ICollection<VixFuturesEodDataReadModel>> ReadLegacyVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
        {
            List<VixFuturesEodDataReadModel> results = [];
            await foreach (var row in context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataAll)}", MarketDataDbCql.GetVixFuturesEodDataAll)
                .ExecuteStreamAsync(MarketDataDbContext.MapToVixFuturesEodData))
            {
                if (row.ValueDate <= valueDate)
                    results.Add(row);
            }

            return [.. results.OrderByDescending(static row => row.ValueDate).ThenBy(static row => row.ContractId, StringComparer.Ordinal)];
        }

        /// <summary>
        /// Performs the <c>ReadIndexedVixFuturesEodDataByValueDateAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<ICollection<VixFuturesEodDataReadModel>> ReadIndexedVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
        {
            var db = context._dbFactory.MarketDataDb;
            HashSet<string> contractIds = new(StringComparer.Ordinal);
            for (var firstBucket = 0; firstBucket < MarketDataDbContext.VixContractBucketCount; firstBucket += MarketDataDbContext.ProjectionReadConcurrency)
            {
                var bucketCount = Math.Min(MarketDataDbContext.ProjectionReadConcurrency, MarketDataDbContext.VixContractBucketCount - firstBucket);
                var bucketReads = Enumerable.Range(firstBucket, bucketCount).Select(async bucket => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesContractIds)}", MarketDataDbCql.GetVixFuturesContractIds)
                    .SetParameters(new GetVixFuturesContractIds(bucket))
                    .ExecuteQueryAsync(MarketDataDbContext.MapToString)).ToArray();
                foreach (var bucket in await Task.WhenAll(bucketReads))
                    contractIds.UnionWith(bucket);
            }

            List<VixFuturesEodDataReadModel> results = [];
            var orderedContractIds = contractIds.Order(StringComparer.Ordinal).ToArray();
            for (var offset = 0; offset < orderedContractIds.Length; offset += MarketDataDbContext.ProjectionReadConcurrency)
            {
                var count = Math.Min(MarketDataDbContext.ProjectionReadConcurrency, orderedContractIds.Length - offset);
                var contractReads = orderedContractIds.AsSpan(offset, count).ToArray().Select(async contractId => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataThroughDate)}", MarketDataDbCql.GetVixFuturesEodDataThroughDate)
                    .SetParameters(new GetVixFuturesEodDataThroughDate(contractId, valueDate))
                    .ExecuteQueryAsync(MarketDataDbContext.MapToVixFuturesEodData)).ToArray();
                foreach (var rows in await Task.WhenAll(contractReads))
                    results.AddRange(rows);
            }

            return [.. results.OrderByDescending(static row => row.ValueDate).ThenBy(static row => row.ContractId, StringComparer.Ordinal)];
        }

        /// <summary>
        /// Performs the <c>GetYieldCurveRatesCoreAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesCoreAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
        {
            if (endDate < startDate)
                return [];
            if (endDate.DayNumber - startDate.DayNumber + 1 > MarketDataDbContext.YieldCurveMaximumRangeDays)
            {
                throw new ArgumentOutOfRangeException(nameof(endDate), $"Yield-curve ranges may span at most {MarketDataDbContext.YieldCurveMaximumRangeDays} days.");
            }

            return await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRates)}", MarketDataDbCql.GetYieldCurveRates)
                .SetParameters(new GetYieldCurveRates(startDate, endDate))
                .ExecuteQueryAsync(MarketDataDbContext.MapToYieldCurveRate, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the <c>GetIntrinsicTimeModes</c> operation for MarketDataDb persistence.
        /// </summary>
        internal List<string> GetIntrinsicTimeModes() => [IntrinsicTimeModeType.TrendExtremeChanged.ToStringFast(), IntrinsicTimeModeType.TrendReversalChanged.ToStringFast(), IntrinsicTimeModeType.TrendDirectionChanged.ToStringFast()];

        /// <summary>
        /// Performs the <c>ReadCanonicalFuturesItiSignalsAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadCanonicalFuturesItiSignalsAsync(IReadOnlyCollection<string> contractIds, DateOnly startDate, DateOnly endDate)
        {
            var db = context._dbFactory.MarketDataDb;
            List<FuturesItiSignalV2ReadModel> rows = [];
            foreach (var batch in contractIds.Chunk(MarketDataDbContext.ProjectionReadConcurrency))
            {
                var reads = batch.Select(async contractId => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)}", MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)
                    .SetParameters(new GetFuturesItiSignalsCanonicalByContract(contractId))
                    .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!));
                foreach (var values in await Task.WhenAll(reads))
                    rows.AddRange(values);
            }

            return [.. rows.Where(row => row.ValueDate >= startDate && row.ValueDate <= endDate).OrderBy(static row => row.ValueDate).ThenBy(static row => row.SequenceId)];
        }

        /// <summary>
        /// Performs the <c>ReadFuturesItiSignalsByDateRangeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiSignalsByDateRangeAsync(IReadOnlyCollection<string> contractIds, DateOnly startDate, DateOnly endDate)
        {
            if (contractIds.Count == 0 || endDate < startDate)
                return [];
            var yearMonths = MarketDataDbContextExtensions.GetYearMonths(startDate, endDate).ToArray();
            var scopes = contractIds.SelectMany(contractId => yearMonths.Select(yearMonth => MarketDataDbContextExtensions.GetFuturesItiMonthScopeKey(contractId, yearMonth))).ToArray();
            var stamp = await context.GetProjectionScopeReadStampAsync(MarketDataDbContext.FuturesItiSignalQueryProjection, scopes);
            if (stamp is null)
                return await context.ReadCanonicalFuturesItiSignalsAsync(contractIds, startDate, endDate);
            var db = context._dbFactory.MarketDataDb;
            var partitions = contractIds.SelectMany(contractId => yearMonths.Select(yearMonth => (contractId, yearMonth))).ToArray();
            List<FuturesItiSignalV2ReadModel> rows = [];
            foreach (var batch in partitions.Chunk(MarketDataDbContext.ProjectionReadConcurrency))
            {
                var requests = batch.Select(async partition =>
                {
                    var monthStart = MarketDataDbContextExtensions.GetMonthStart(partition.yearMonth);
                    var monthEnd = MarketDataDbContextExtensions.GetMonthEnd(partition.yearMonth);
                    return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsByContractMonth)}", MarketDataDbCql.GetFuturesItiSignalsByContractMonth)
                        .SetParameters(new GetFuturesItiSignalsByContractMonth(partition.contractId, partition.yearMonth, startDate > monthStart ? startDate : monthStart, endDate < monthEnd ? endDate : monthEnd))
                        .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!);
                });
                foreach (var values in await Task.WhenAll(requests))
                    rows.AddRange(values);
            }

            if (!await context.IsProjectionScopeReadStampValidAsync(stamp.Value))
                return await context.ReadCanonicalFuturesItiSignalsAsync(contractIds, startDate, endDate);
            return [.. rows.OrderBy(static row => row.ValueDate).ThenBy(static row => row.SequenceId)];
        }

        /// <summary>
        /// Performs the <c>ReadFuturesItiDayModeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiDayModeAsync(string contractId, DateOnly valueDate, IntrinsicTimeModeType intrinsicTimeMode, long? afterSequenceId = null, CancellationToken cancellationToken = default)
        {
            var stamp = await context.GetProjectionScopeReadStampAsync(MarketDataDbContext.FuturesItiSignalQueryProjection, [MarketDataDbContextExtensions.GetFuturesItiDayScopeKey(contractId, valueDate)]);
            if (stamp is not null)
            {
                var mode = intrinsicTimeMode.ToStringFast();
                var query = afterSequenceId.HasValue ? context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsByContractDayModeAfterSequence)}", MarketDataDbCql.GetFuturesItiSignalsByContractDayModeAfterSequence)
                    .SetParameters(new GetFuturesItiSignalsByContractDayModeAfterSequence(contractId, valueDate, mode, afterSequenceId.Value)) : context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsByContractDayMode)}", MarketDataDbCql.GetFuturesItiSignalsByContractDayMode)
                    .SetParameters(new GetFuturesItiSignalsByContractDayMode(contractId, valueDate, mode));
                var projected = await query.ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!, cancellationToken)
                    .ConfigureAwait(false);
                if (await context.IsProjectionScopeReadStampValidAsync(stamp.Value))
                    return projected;
            }

            var canonical = await context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsCanonicalByContractDay)}", MarketDataDbCql.GetFuturesItiSignalsCanonicalByContractDay)
                .SetParameters(new GetFuturesItiSignalsCanonicalByContractDay(contractId, valueDate))
                .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!, cancellationToken)
                .ConfigureAwait(false);
            return [.. canonical.Where(row => row.IntrinsicTimeMode == intrinsicTimeMode && (!afterSequenceId.HasValue || row.SequenceId > afterSequenceId.Value)).OrderByDescending(static row => row.SequenceId)];
        }

        /// <summary>
        /// Performs the <c>ReadLastFuturesItiTrendModeAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<FuturesItiSignalV2ReadModel?> ReadLastFuturesItiTrendModeAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, IntrinsicTimeModeType intrinsicTimeMode, CancellationToken cancellationToken = default)
        {
            var db = context._dbFactory.MarketDataDb;
            var targetMonth = MarketDataDbContextExtensions.ToYearMonth(valueDate);
            var months = (await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMonths)}", MarketDataDbCql.GetMarketDataProjectionMonths)
                .SetParameters(new GetMarketDataProjectionMonths(MarketDataDbContext.FuturesItiSignalQueryProjection, targetMonth))
                .ExecuteQueryAsync(MarketDataDbContext.MapToYearMonth, cancellationToken)
                .ConfigureAwait(false)).ToArray();
            var trend = intrinsicTimeTrend.ToStringFast();
            var mode = intrinsicTimeMode.ToStringFast();
            var scopes = months.Select(month => MarketDataDbContextExtensions.GetFuturesItiTimelineScopeKey(contractId, trend, mode, month)).Concat(context.GetProjectionGuardScopeKeys()).ToArray();
            var stamp = await context.GetProjectionScopeReadStampAsync(MarketDataDbContext.FuturesItiSignalQueryProjection, scopes);
            if (stamp is not null)
            {
                FuturesItiSignalV2ReadModel? projected = null;
                foreach (var yearMonth in months)
                {
                    projected = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.GetLastFuturesItiSignalByTrendModeMonth)
                        .SetParameters(new GetLastFuturesItiSignalByTrendModeMonth(contractId, trend, mode, yearMonth, yearMonth == targetMonth ? valueDate : MarketDataDbContextExtensions.GetMonthEnd(yearMonth)))
                        .ExecuteSingleAsync(MarketDataDbContext.MapToFuturesItiSignal!, cancellationToken)
                        .ConfigureAwait(false);
                    if (projected is not null)
                        break;
                }

                if (await context.IsProjectionScopeReadStampValidAsync(stamp.Value))
                    return projected;
            }

            return (await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)}", MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)
                .SetParameters(new GetFuturesItiSignalsCanonicalByContract(contractId))
                .ExecuteQueryAsync(MarketDataDbContext.MapToFuturesItiSignal!, cancellationToken)
                .ConfigureAwait(false)).Where(row => row.ValueDate <= valueDate && row.IntrinsicTimeTrend == intrinsicTimeTrend && row.IntrinsicTimeMode == intrinsicTimeMode).OrderByDescending(static row => row.ValueDate).ThenByDescending(static row => row.SequenceId).FirstOrDefault();
        }

        /// <summary>
        /// Performs the <c>ReadLatestAsync</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal Task<T?> ReadMonthAsync<T>(MarketSeriesIdentity seriesIdentity, DateOnly partitionMonth, DateTime marketDataAsOf, string configurationId, string cql, string operation, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken, TimeFrameType timeFrame = TimeFrameType.Daily)
            where T : class => context._dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{operation}", cql)
                .SetParameters(new GetLatestFuturesRegimeSignal(seriesIdentity.Format(), timeFrame.ToString(), configurationId, MarketDataDbContextExtensions.Bucket(partitionMonth), marketDataAsOf))
                .ExecuteSingleAsync(map, cancellationToken);

        /// <summary>
        /// Performs the <c>ReadObservationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<OptionIvObservation?> ReadObservationAsync(string environment, string id, CancellationToken token) => await context.Database.Use("OptionVolatility.Observation.Id.Read", MarketDataDbCql.SelectObservationById)
            .SetParameters(new OptionVolatilityParameters([environment, id]))
            .ExecuteSingleAsync(row => MarketDataDbContextExtensions.Deserialize<OptionIvObservation>(row.GetBytes(0)), token)
            .ConfigureAwait(false);

        /// <summary>
        /// Performs the <c>ReadLatestAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task<OptionIvLatestPointer?> ReadLatestAsync(VolatilityStorageScope scope, CancellationToken token) => await context.Database.Use("OptionVolatility.Latest.Read", MarketDataDbCql.SelectLatest)
            .SetParameters(new OptionVolatilityParameters([scope.Environment, scope.Series.SeriesId, scope.Series.MethodologyVersion, scope.MetricPolicyVersion]))
            .ExecuteSingleAsync(row => new OptionIvLatestPointer(scope, row.GetString(0), row.GetString(1), row.GetLong(2), new DateTimeOffset(DateTime.SpecifyKind(row.GetDateTime(3), DateTimeKind.Utc))), token)
            .ConfigureAwait(false);
    }

    extension(DateOnly valueDate)
    {
        /// <summary>
        /// Performs the <c>ToYearMonth</c> operation for MarketDataDb persistence.
        /// </summary>
        internal int ToYearMonth() => checked(valueDate.Year * 100 + valueDate.Month);

        /// <summary>
        /// Performs the <c>GetFuturesEodScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetFuturesEodScopeKey() => MarketDataDbContextExtensions.GetFuturesEodScopeKey(MarketDataDbContextExtensions.ToYearMonth(valueDate));

        /// <summary>
        /// Performs the <c>GetFuturesItiCalendarBucketStart</c> operation for MarketDataDb persistence.
        /// </summary>
        internal DateOnly GetFuturesItiCalendarBucketStart(TimeFrameType period) => period switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(-(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => valueDate
        };

        /// <summary>
        /// Performs the <c>Bucket</c> operation for MarketDataDb persistence.
        /// </summary>
        internal int Bucket() => (valueDate.Year * 100) + valueDate.Month;
    }

    extension(int yearMonth)
    {
        /// <summary>
        /// Performs the <c>GetMonthStart</c> operation for MarketDataDb persistence.
        /// </summary>
        internal DateOnly GetMonthStart() => new(yearMonth / 100, yearMonth % 100, 1);

        /// <summary>
        /// Performs the <c>GetMonthEnd</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal string GetFuturesEodScopeKey() => yearMonth.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    extension(DateOnly startDate)
    {
        /// <summary>
        /// Performs the <c>GetYearMonths</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal ulong GetVixContractIdentity() => MarketDataDbContextExtensions.GetVixContractIdentity(MarketDataDbContextExtensions.GetVixContractBucket(contractId), contractId);

        /// <summary>
        /// Performs the <c>GetVixContractBucket</c> operation for MarketDataDb persistence.
        /// </summary>
        internal int GetVixContractBucket() => (int)(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), contractId) % MarketDataDbContext.VixContractBucketCount);

        /// <summary>
        /// Performs the <c>GetFuturesTickScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetFuturesTickScopeKey(DateOnly valueDate) => string.Concat(contractId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", contractId, ":", valueDate.DayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// Performs the <c>GetVixContractIndexScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetVixContractIndexScopeKey() => MarketDataDbContextExtensions.GetVixContractIndexScopeKey(MarketDataDbContextExtensions.GetVixContractBucket(contractId));

        /// <summary>
        /// Performs the <c>IsFuturesContractForSymbol</c> operation for MarketDataDb persistence.
        /// </summary>
        internal bool IsFuturesContractForSymbol(string symbol)
        {
            if (!contractId.StartsWith(symbol, StringComparison.Ordinal))
                return false;
            var suffix = contractId.AsSpan(symbol.Length);
            if (suffix.Length == 8 && DateOnly.TryParseExact(suffix, "yyyyMMdd", null, DateTimeStyles.None, out _))
                return true;
            return suffix.Length is >= 2 and <= 5 && "FGHJKMNQUVXZ".Contains(suffix[0]) && suffix[1..].IndexOfAnyExceptInRange('0', '9') < 0;
        }

        /// <summary>
        /// Performs the <c>GetFuturesItiDayScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetFuturesItiDayScopeKey(DateOnly valueDate) => $"day:{contractId.Length}:{contractId}:{valueDate:yyyyMMdd}";

        /// <summary>
        /// Performs the <c>GetFuturesItiMonthScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetFuturesItiMonthScopeKey(int yearMonth) => $"month:{contractId.Length}:{contractId}:{yearMonth}";

        /// <summary>
        /// Performs the <c>GetFuturesItiTimelineScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetFuturesItiTimelineScopeKey(string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth) => $"timeline:{contractId.Length}:{contractId}:{intrinsicTimeTrend.Length}:{intrinsicTimeTrend}:{intrinsicTimeMode.Length}:{intrinsicTimeMode}:{yearMonth}";

        /// <summary>
        /// Performs the <c>GetFuturesItiProjectionScopeKeys</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string[] GetFuturesItiProjectionScopeKeys(DateOnly valueDate, string intrinsicTimeTrend, string intrinsicTimeMode)
        {
            var yearMonth = MarketDataDbContextExtensions.ToYearMonth(valueDate);
            return [MarketDataDbContextExtensions.GetFuturesItiDayScopeKey(contractId, valueDate), MarketDataDbContextExtensions.GetFuturesItiMonthScopeKey(contractId, yearMonth), MarketDataDbContextExtensions.GetFuturesItiTimelineScopeKey(contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth)];
        }
    }

    extension(int bucket)
    {
        /// <summary>
        /// Performs the <c>GetVixContractIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
        internal ulong GetVixContractIdentity(string contractId)
        {
            var hash = MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), bucket);
            return MarketDataProjectionHash.Add(hash, contractId);
        }

        /// <summary>
        /// Performs the <c>GetVixContractIndexScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetVixContractIndexScopeKey() => bucket.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    extension(string scopeKey)
    {
        /// <summary>
        /// Performs the <c>GetProjectionGuardScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetProjectionGuardScopeKey() => string.Concat(MarketDataDbContext.ProjectionGuardScopePrefix, (MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), scopeKey) % MarketDataDbContext.ProjectionGuardScopeCount).ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// Performs the <c>IsProjectionGuardScopeKey</c> operation for MarketDataDb persistence.
        /// </summary>
        internal bool IsProjectionGuardScopeKey() => scopeKey.StartsWith(MarketDataDbContext.ProjectionGuardScopePrefix, StringComparison.Ordinal);
    }

    extension(IEnumerable<string> scopeKeys)
    {
        /// <summary>
        /// Performs the <c>AddProjectionGuardScopes</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal Task TryClassifyTickGuardOperationFailureAsync(string guardScopeKey, Guid operationId, TickProjectionGuardFailureStage stage) => MarketDataDbContextExtensions.IsTickGuardFailureAutomaticallyRecoverable(stage) ? MarketDataDbContextExtensions.TryFailTickGuardOperationAsync(db, guardScopeKey, operationId) : Task.CompletedTask;

        /// <summary>
        /// Performs the <c>TryFailTickGuardOperationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal async Task TryFailTickGuardOperationAsync(string guardScopeKey, Guid operationId)
        {
            try
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                    .SetParameters(new InsertMarketDataProjectionScopeMutationV3(MarketDataDbContext.FuturesTickByTimeProjection, guardScopeKey, operationId, DateTime.UnixEpoch))
                    .ExecuteCommandAsync();
            }
            catch
            {
                // Never remove ambiguous recovery evidence after another storage failure.
            }
        }

        /// <summary>
        /// Performs the <c>EndProjectionOperationAsync</c> operation for MarketDataDb persistence.
        /// </summary>
        internal Task<long[]> EndProjectionOperationAsync(string projectionName, HashSet<Guid> activeOperations, CancellationToken cancellationToken = default) => db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.EndMarketDataProjectionOperation)}", MarketDataDbCql.EndMarketDataProjectionOperation)
            .SetParameters(new EndMarketDataProjectionOperation(projectionName, Guid.NewGuid(), activeOperations))
            .ExecuteCommandAsync(cancellationToken);
    }

    extension(TickProjectionGuardFailureStage stage)
    {
        /// <summary>
        /// Performs the <c>IsTickGuardFailureAutomaticallyRecoverable</c> operation for MarketDataDb persistence.
        /// </summary>
        internal bool IsTickGuardFailureAutomaticallyRecoverable() => stage is TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission or TickProjectionGuardFailureStage.AfterDataAcknowledged;
    }

    extension(FuturesEodDataV2ReadModel e)
    {
        /// <summary>
        /// Performs the <c>CreateFuturesEodDataParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        internal InsertFuturesEodData CreateFuturesEodDataParameters(decimal openPrice) => new(contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA);

        /// <summary>
        /// Performs the <c>CreateFuturesEodDataByMonthParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        internal InsertFuturesEodDataByMonth CreateFuturesEodDataByMonthParameters(decimal openPrice) => new(yearMonth: MarketDataDbContextExtensions.ToYearMonth(e.ValueDate), contractId: e.ContractId, valueDate: e.ValueDate, symbol: e.Symbol, openPrice, highPrice: e.HighPrice, lowPrice: e.LowPrice, closePrice: e.ClosePrice, volume: e.Volume, dailyPercentChange: e.DailyPercentChange, dailyStdDev: e.DailyStdDev, dailyStdDevAmount: e.DailyStdDevAmount, upperBand: e.UpperBand, mean: e.Mean, lowerBand: e.LowerBand, marketDirection: e.MarketDirection.ToStringFast(), marketVolatility: e.MarketVolatility.ToStringFast(), priceDirection: e.PriceDirection.ToStringFast(), priceVolatility: e.PriceVolatility.ToStringFast(), marketDirectionIndicator: e.MarketDirectionIndicator, windowSize: e.WindowSize, fiftyDMA: e.FiftyDMA, twoHundredDMA: e.TwoHundredDMA);
    }

    extension(IEnumerable<double> source)
    {
        /// <summary>
        /// Performs the <c>CalculateStatistics</c> operation for MarketDataDb persistence.
        /// </summary>
        internal FuturesItiTrendModelDataStatistics CalculateStatistics()
        {
            var values = source.OrderBy(e => e).ToArray();
            if (values.Length == 0)
                return FuturesItiTrendModelDataStatistics.Empty;
            var mean = values.Average();
            var variance = values.Average(e => Math.Pow(e - mean, 2));
            var stdDev = Math.Sqrt(variance);
            var median = values.Length % 2 == 0 ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2 : values[values.Length / 2];
            var skewness = stdDev == 0 ? 0 : values.Average(e => Math.Pow((e - mean) / stdDev, 3));
            return new FuturesItiTrendModelDataStatistics(values.Length, values[^1], mean, median, values[0], skewness, stdDev, variance);
        }
    }

    extension(MarketDataDownloadCursor cursor)
    {
        /// <summary>
        /// Performs the <c>ValidateDownloadCursor</c> operation for MarketDataDb persistence.
        /// </summary>
        internal void ValidateDownloadCursor()
        {
            if (cursor.ImportCommandId == Guid.Empty || cursor.RequestedAtUtc == default || cursor.RequestedAtUtc != MarketDataDownloadOutcome.MillisecondUtc(cursor.RequestedAtUtc))
                throw new ArgumentException("Invalid download attempt cursor.");
        }
    }

    extension(IObjectDataRecord row)
    {
        /// <summary>
        /// Performs the <c>GetNullableString</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string? GetNullableString(int index) => row.IsNull(index) ? null : row.GetString(index);
    }

    extension(DateTime value)
    {
        /// <summary>
        /// Performs the <c>NormalizeEconomicCalendarTimestamp</c> operation for MarketDataDb persistence.
        /// </summary>
        internal DateTime NormalizeEconomicCalendarTimestamp()
        {
            var utc = ProjectionMutationSafety.AsUtc(value);
            return new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        }

        /// <summary>
        /// Performs the <c>EconomicCalendarMonthBucket</c> operation for MarketDataDb persistence.
        /// </summary>
        internal int EconomicCalendarMonthBucket() => value.Year * 100 + value.Month;

        /// <summary>
        /// Performs the <c>Utc</c> operation for MarketDataDb persistence.
        /// </summary>
        internal DateTimeOffset Utc() => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    extension(DateTime startDate)
    {
        /// <summary>
        /// Performs the <c>EconomicCalendarMonthBucketsDescending</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal bool IsAfterCursor(CalendarPageToken cursor) => cursor.LastEventDateTicks is null || row.EventDate.Ticks < cursor.LastEventDateTicks.Value || (row.EventDate.Ticks == cursor.LastEventDateTicks.Value && string.CompareOrdinal(row.EventName, cursor.LastEventName) > 0);

        /// <summary>
        /// Performs the <c>GetEconomicCalendarProjectionIdentity</c> operation for MarketDataDb persistence.
        /// </summary>
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
            catch (Exception ex) when (ex is FormatException or JsonException)
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
        internal bool IsNewer(FuturesTradeSignalRepairRow current) => (candidate.ValueDate.DayNumber, candidate.Timestamp.Ticks, candidate.SequenceId).CompareTo((current.ValueDate.DayNumber, current.Timestamp.Ticks, current.SequenceId)) > 0;
    }

    extension(string payload)
    {
        /// <summary>
        /// Performs the <c>ParseFuturesTradeSignalRepairRow</c> operation for MarketDataDb persistence.
        /// </summary>
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
                if (string.IsNullOrWhiteSpace(contractId) || contractId.Contains(','))
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
        internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    extension<TDataRecord>(TDataRecord row) where TDataRecord : IObjectDataRecord
    {
        /// <summary>
        /// Performs the <c>MapJsonPayload</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string MapJsonPayload() => row.GetString(0);
    }

    extension(JsonElement root)
    {
        /// <summary>
        /// Performs the <c>GetString</c> operation for MarketDataDb persistence.
        /// </summary>
        internal string GetString(string name)
        {
            if (!MarketDataDbContextExtensions.TryGetProperty(root, name, out var property) || property.ValueKind == JsonValueKind.Null)
                return string.Empty;
            return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
        }

        /// <summary>
        /// Performs the <c>GetInt64</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal FuturesItiSignalMDIV2ReadModel ToFuturesItiSignalMdi() => new(contractId: row.ContractId, valueDate: row.ValueDate, intrinsicTime: row.IntrinsicTime, trendType: row.IntrinsicTimeTrend, mdi: row.IntrinsicPrice);
    }

    extension(FuturesItiSignalV2ReadModel e)
    {
        /// <summary>
        /// Performs the <c>CreateFuturesItiSignalParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        internal InsertFuturesItiSignal CreateFuturesItiSignalParameters(long sequenceId) => new(e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandLevel, e.ReversalLevel);

        /// <summary>
        /// Performs the <c>CreateFuturesItiSignalMonthParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        internal InsertFuturesItiSignalByContractMonth CreateFuturesItiSignalMonthParameters(long sequenceId) => new(MarketDataDbContextExtensions.ToYearMonth(e.ValueDate), e.ContractId, e.ValueDate, e.TimePeriod.ToStringFast(), sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandLevel, e.ReversalLevel);

        /// <summary>
        /// Performs the <c>CreateFuturesItiTimeFrameStateParameters</c> operation for MarketDataDb persistence.
        /// </summary>
        internal UpsertFuturesItiTimeFrameState CreateFuturesItiTimeFrameStateParameters(long sequenceId) => new(e.ContractId, e.TimePeriod.ToStringFast(), MarketDataDbContextExtensions.GetFuturesItiCalendarBucketStart(e.ValueDate, e.TimePeriod), e.TimeFrameStartValueDate == default ? e.ValueDate : e.TimeFrameStartValueDate, e.ValueDate, sequenceId, e.IntrinsicTime, e.IntrinsicTimeGroupId, e.IntrinsicTimeLength, e.IntrinsicPrice, e.IntrinsicTimeTrend.ToStringFast(), e.IntrinsicTimeMode.ToStringFast(), e.TrendPrice, e.TrendExtreme, e.TrendReversal, e.TrendDelta, e.TargetDelta, e.Lambda, e.TradingDays, e.Threshold, e.UpTrendTrigger, e.DownTrendTrigger, e.TradeState.ToStringFast(), e.BandAnchorPrice == 0 ? e.IntrinsicPrice : e.BandAnchorPrice, e.BandPercentage == 0 ? 0.15 : e.BandPercentage, e.BandSize == 0 ? e.Threshold * 0.15 : e.BandSize, e.BandLevel, e.ReversalLevel);
    }

    extension(IObjectDataRecord value)
    {
        /// <summary>
        /// Performs the <c>Decimal</c> operation for MarketDataDb persistence.
        /// </summary>
        internal decimal? Decimal(int index) => value.IsNull(index) ? null : value.GetDecimal(index);
    }

    extension(OptionIvLatestPointer pointer)
    {
        /// <summary>
        /// Performs the <c>LatestValues</c> operation for MarketDataDb persistence.
        /// </summary>
        internal OptionVolatilityParameters LatestValues() => new([pointer.Scope.Environment, pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion, pointer.Scope.MetricPolicyVersion, pointer.SnapshotId, pointer.SnapshotDigest, pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime]);

        /// <summary>
        /// Performs the <c>AdvanceLatestValues</c> operation for MarketDataDb persistence.
        /// </summary>
        internal OptionVolatilityParameters AdvanceLatestValues() => new([pointer.SnapshotId, pointer.SnapshotDigest, pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime, pointer.Scope.Environment, pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion, pointer.Scope.MetricPolicyVersion]);
    }

    extension(VolatilityHistoryPageRequest request)
    {
        /// <summary>
        /// Performs the <c>HistoryValues</c> operation for MarketDataDb persistence.
        /// </summary>
        internal OptionVolatilityParameters HistoryValues(bool includePolicy) => includePolicy ? new([request.Scope.Environment, request.Scope.Series.SeriesId, request.Scope.Series.MethodologyVersion, request.Scope.MetricPolicyVersion, request.Bucket.Value, request.FromValueDate, request.ToValueDate]) : new([request.Scope.Environment, request.Scope.Series.SeriesId, request.Scope.Series.MethodologyVersion, request.Bucket.Value, request.FromValueDate, request.ToValueDate]);

        /// <summary>
        /// Performs the <c>ValidatePage</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal OptionIvLatestPointer Pointer()
        {
            var snapshot = publication.Metric.Snapshot;
            return new(new(publication.Environment, snapshot.Series, snapshot.MetricPolicyVersion), snapshot.SnapshotId, snapshot.SnapshotDigest, publication.Metric.PublicationSequence, snapshot.AvailableAtUtc);
        }

        /// <summary>
        /// Performs the <c>ValidatePublication</c> operation for MarketDataDb persistence.
        /// </summary>
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
        internal void ValidateEnvironment() => ArgumentException.ThrowIfNullOrWhiteSpace(environment);
    }

    extension(DateOnly value)
    {
        /// <summary>
        /// Performs the <c>YearMonth</c> operation for MarketDataDb persistence.
        /// </summary>
        internal int YearMonth() => checked(value.Year * 100 + value.Month);
    }

}
