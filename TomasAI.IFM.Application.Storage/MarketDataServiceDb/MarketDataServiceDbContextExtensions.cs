using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

internal static class MarketDataServiceDbContextExtensions
{
    static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 32 };

    extension(MarketDataServiceDbContext context)
    {
        /// <summary>Writes an accepted recovery row under its immutable record and digest fence.</summary>
        internal async Task WriteRecoveryLogRowAsync(RecoveryLogRow row, CancellationToken token)
        {
            await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
            await connection.OpenAsync(token)
            .ConfigureAwait(false);
            await using var command = new NpgsqlCommand(MarketDataServiceDbSql.InsertRecoveryLog, connection) { CommandTimeout = 120 };
            PostgresValueBinding.AddParameters(command, (object?[])new RecoveryLogInsertParameter(row).Bind());
            if (await command.ExecuteScalarAsync(token)
            .ConfigureAwait(false) is null)
                throw new InvalidDataException("Recovery projection RecordId/digest conflict.");
        }

        /// <summary>Reads one ordered page of committed recovery payloads.</summary>
        internal async Task<IReadOnlyList<string>> ReadRecoveryLogRowsAsync(RecoveryLogReadParameter selection, CancellationToken token)
        {
            await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
            await connection.OpenAsync(token)
            .ConfigureAwait(false);
            await using var command = new NpgsqlCommand(selection.Statement, connection) { CommandTimeout = 120 };
            PostgresValueBinding.AddParameters(command, (object?[])selection.Bind());
            var rows = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
            while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
                rows.Add(MarketDataServiceDbContext.MapToRecoveryLogPayload(reader));
            return rows;
        }

        /// <summary>Gets a watchdog observation by its durable identity.</summary>
        /// <param name="id">The observation identity.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The matching observation, or <see langword="null"/> when none exists.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task<DatabentoWatchdogObservation?> GetObservationByIdentityAsync(Guid id, CancellationToken cancellationToken)
            => context.Database
                .Use("MarketDataService.GetObservationByIdentity", MarketDataServiceDbSql.GetObservationByIdentity)
            .SetParameters(new IdentityParameter(id))
            .ExecuteSingleAsync<DatabentoWatchdogObservation?>(MarketDataServiceDbContext.MapToObservation, cancellationToken);

        /// <summary>
        /// Performs the <c>ReadDurableSubscriptionAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<DurableSubscriptionSnapshot> ReadDurableSubscriptionAsync(string scope, string dataset,
            CancellationToken cancellationToken = default)
        {
            var row = await context.ReadDurableCurrentAsync(context.CreateDurableRepository(), scope, dataset, false, cancellationToken)
            .ConfigureAwait(false);
            return row ?? context.CreateEmptyDurableSnapshot(scope, dataset);
        }

        /// <summary>
        /// Performs the <c>FindDurableOperationAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<DurableIntentResult?> FindDurableOperationAsync(string scope, string dataset, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            return (await context.ReadDurableOperationAsync(context.CreateDurableRepository(), scope, dataset, operationId, cancellationToken)
            .ConfigureAwait(false))?.Result;
        }

        /// <summary>
        /// Performs the <c>ApplyDurableSubscriptionAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<DurableIntentResult> ApplyDurableSubscriptionAsync(DurableSubscriptionCommand prepared,
            CancellationToken cancellationToken = default)
        {
            var input = prepared.Input;
            var requestDigest = prepared.RequestDigest;
            var factDigest = prepared.FactDigest;
            cancellationToken.ThrowIfCancellationRequested();
            var repository = context.CreateDurableRepository();
            var transaction = repository.BeginTransaction() as PostgresObjectDataRepositoryTransaction<MarketDataServiceTransactionRepository>
                ?? throw new InvalidOperationException("PostgreSQL transaction was not created.");
            try
            {
                _ = await repository.Use("Stage4Intent.BoundTransaction", MarketDataServiceDbSql.ConfigureDurableTransaction)
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                var initialSnapshotParameters = new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset),
                            Text(JsonSerializer.Serialize(context.CreateEmptyDurableSnapshot(input.Scope, input.Dataset)))));
                // Insert or wait for the first writer; SELECT FOR UPDATE then serializes this dataset.
                _ = await repository.Use("Stage4Intent.EnsureCurrent", MarketDataServiceDbSql.EnsureDurableCurrent)
                    .SetParameters(initialSnapshotParameters)
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                var current = await context.ReadDurableCurrentAsync(repository, input.Scope, input.Dataset, true, cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidDataException("Locked current intent is missing.");
                var prior = await context.ReadDurableOperationAsync(repository, input.Scope, input.Dataset, input.OperationId, cancellationToken)
                    .ConfigureAwait(false);
                if (prior is not null)
                {
                    transaction.Commit();
                    return string.Equals(prior.Digest,requestDigest,StringComparison.Ordinal) ? prior.Result
                        : new(input.OperationId, DurableIntentResultCode.OperationConflict, prior.Result.Revision, null);
                }

                var plan = prepared.Plan(current);
                var code = plan.Code;
                var nextRevision = plan.Revision;
                var additions = plan.Additions;
                if (code == DurableIntentResultCode.Committed && additions.Count != 0)
                {
                    var reserved = await context.ReadDurableSingleAsync(repository, "Stage4Intent.CheckLeaseIdentity", MarketDataServiceDbSql.CheckDurableLeaseIdentity, Values(Text(input.Scope), Text(input.Dataset),
                                Text(JsonSerializer.Serialize(additions.Select(value => value.LeaseId)))),
                            MarketDataServiceDbContext.MapToDurableExists, cancellationToken)
                                .ConfigureAwait(false);
                    if (reserved?.Exists == true)
                    {
                        code = DurableIntentResultCode.LeaseConflict;
                        nextRevision = current.Revision;
                    }
                }
                var now = context._time.GetUtcNow();
                var transitionId = code == DurableIntentResultCode.Committed ? Guid.NewGuid() : (Guid?)null;
                var result = new DurableIntentResult(input.OperationId, code, nextRevision, transitionId);
                if (code == DurableIntentResultCode.Committed)
                {
                    if (plan.ExceedsSnapshotCapacity)
                    {
                        code = DurableIntentResultCode.CapacityExceeded;
                        result = new(input.OperationId, code, current.Revision, null);
                    }
                    else
                    {
                        var updateParameters = new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset),
                                    Bigint(nextRevision), Text(plan.SnapshotJson), Bigint(current.Revision)));
                        var changed = await repository.Use("Stage4Intent.UpdateCurrent", MarketDataServiceDbSql.UpdateDurableCurrent)
                            .SetParameters(updateParameters)
                            .ExecuteCommandAsync(cancellationToken)
                            .ConfigureAwait(false);
                        if (changed.Sum() != 1) throw new InvalidOperationException("Current revision changed while locked.");
                        context._writeObserver?.Invoke(DurableStoreWriteStage.CurrentIntent);

                        if (additions.Count != 0)
                        {
                            var identities = additions.Select(lease => new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset),
                                Uuid(lease.LeaseId), Text(input.SourceId), Text(DurableSubscriptionContract.Digest(input.Owner)),
                                Text(DurableSubscriptionContract.Digest(lease)), Bigint(nextRevision)))).ToArray();
                            _ = await repository.Use("Stage4Intent.ReserveLeaseIdentity", MarketDataServiceDbSql.ReserveDurableLeaseIdentity)
                                .SetParameters(identities)
                                .ExecuteCommandAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }
                        var removedIds = plan.Removed;
                        if (removedIds.Count != 0)
                        {
                            var retireParameters = new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Bigint(nextRevision),
                                        Text(JsonSerializer.Serialize(removedIds))));
                            var retired = await repository.Use("Stage4Intent.RetireLeaseIdentity", MarketDataServiceDbSql.RetireDurableLeaseIdentity)
                                .SetParameters(retireParameters)
                                .ExecuteCommandAsync(cancellationToken)
                                .ConfigureAwait(false);
                            if (retired.Sum() != removedIds.Count)
                                throw new InvalidDataException("Current intent is missing its immutable lease identity reservation.");
                        }
                        context._writeObserver?.Invoke(DurableStoreWriteStage.LeaseIdentity);
                    }
                }
                var receiptParameters = new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Uuid(input.OperationId),
                            Text(requestDigest), Text(JsonSerializer.Serialize(result)), TimestampTz(now.UtcDateTime)));

                _ = await repository.Use("Stage4Intent.InsertOperation", MarketDataServiceDbSql.InsertDurableOperation)
                    .SetParameters(receiptParameters)
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                context._writeObserver?.Invoke(DurableStoreWriteStage.OperationResult);

                if (code == DurableIntentResultCode.Committed)
                {
                    var outbox = new DurableSubscriptionOutboxItem(transitionId!.Value, input.OperationId, input.CorrelationId,
                        nextRevision, input.SourceId, input.SourceVersion, input.Status, input.ReasonCode, now);
                    var outboxParameters = new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Uuid(outbox.TransitionId),
                                Uuid(input.OperationId), Bigint(nextRevision), Text(JsonSerializer.Serialize(outbox)), TimestampTz(now.UtcDateTime)));
                    _ = await repository.Use("Stage4Intent.InsertOutbox", MarketDataServiceDbSql.InsertDurableOutbox)
                        .SetParameters(outboxParameters)
                        .ExecuteCommandAsync(cancellationToken)
                        .ConfigureAwait(false);
                    context._writeObserver?.Invoke(DurableStoreWriteStage.Outbox);
                    var watermarkParameters = new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Text(input.SourceId),
                                Bigint(input.SourceVersion), Uuid(input.SourceEventId), Text(factDigest),
                                Text(DurableSubscriptionContract.Digest(input.Owner))));
                    _ = await repository.Use("Stage4Intent.UpdateWatermark", MarketDataServiceDbSql.UpdateDurableWatermark)
                        .SetParameters(watermarkParameters)
                        .ExecuteCommandAsync(cancellationToken)
                        .ConfigureAwait(false);
                    context._writeObserver?.Invoke(DurableStoreWriteStage.AuthorityWatermark);
                }
                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                return result;
            }
            catch
            {
                // Preserve the original failure when a broken connection has already rolled back.
                try { transaction.Rollback(); } catch { /* Preserve the original database failure; cleanup is best effort. */ }
                throw;
            }
            finally
            {
                // The shared provider normally closes and clears these in Commit/Rollback. A transport
                // failure can throw before that cleanup; dispose only this call's remaining owned state.
                // Never replace the original database/cancellation exception with a cleanup failure.
                try { if (transaction.Transaction is { } ownedTransaction) await ownedTransaction.DisposeAsync()
                    .ConfigureAwait(false); } catch { /* Preserve the original database failure; cleanup is best effort. */ }
                try { if (transaction.Connection is { } ownedConnection) await ownedConnection.DisposeAsync()
                    .ConfigureAwait(false); } catch { /* Preserve the original database failure; cleanup is best effort. */ }
            }
        }

        /// <summary>
        /// Performs the <c>ReadPendingDurableOutboxAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingDurableOutboxAsync(string scope, string dataset,
            int pageSize = 100, CancellationToken cancellationToken = default)
        {
        var preparedParameters7 = new DurableSubscriptionParameters(Values(Text(scope), Text(dataset), Integer(pageSize)));
            var items = await context.CreateDurableRepository()
            .Use("Stage4Intent.ReadOutbox", MarketDataServiceDbSql.ReadDurableOutbox)
            .SetParameters(preparedParameters7)
            .ExecuteQueryAsync(MarketDataServiceDbContext.MapToDurableOutboxItem, cancellationToken)
            .ConfigureAwait(false);
            return Array.AsReadOnly(items.ToArray());
        }

        /// <summary>
        /// Performs the <c>AcknowledgeDurableOutboxAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<bool> AcknowledgeDurableOutboxAsync(string scope, string dataset, Guid transitionId,
            CancellationToken cancellationToken = default)
        {
        var preparedParameters8 = new DurableSubscriptionParameters(Values(Text(scope), Text(dataset), Uuid(transitionId), TimestampTz(context._time.GetUtcNow().UtcDateTime)));
            var changed = await context.CreateDurableRepository()
            .Use("Stage4Intent.AcknowledgeOutbox", MarketDataServiceDbSql.AcknowledgeDurableOutbox)
            .SetParameters(preparedParameters8)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
            return changed.Sum() == 1;
        }

        /// <summary>
        /// Performs the <c>ReadDurableCurrentAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<DurableSubscriptionSnapshot?> ReadDurableCurrentAsync(MarketDataServiceTransactionRepository repository, string scope, string dataset,
            bool lockRow, CancellationToken cancellationToken)
        {
            var sql = lockRow
                ? MarketDataServiceDbSql.ReadDurableCurrentForUpdate
                : MarketDataServiceDbSql.ReadDurableCurrent;
            var row = await context.ReadDurableSingleAsync(repository, "Stage4Intent.ReadCurrent", sql, Values(Text(scope), Text(dataset)),
                MarketDataServiceDbContext.MapToDurableCurrentRow, cancellationToken)
            .ConfigureAwait(false);
            if (row is null) return null;
            var result = DurableSubscriptionContract.Freeze(row.Json.DeserializeDurable<DurableSubscriptionSnapshot>(
                DurableSubscriptionContract.MaximumSnapshotBytes));
            if (!string.Equals(result.Scope,scope,StringComparison.Ordinal) || !string.Equals(result.Dataset,dataset,StringComparison.Ordinal) || result.Revision != row.Revision)
                throw new InvalidDataException("Persisted intent scope or revision does not match its row.");
            return result;
        }

        /// <summary>
        /// Performs the <c>ReadDurableOperationAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task<DurableOperationRow?> ReadDurableOperationAsync(MarketDataServiceTransactionRepository repository, string scope, string dataset, Guid operationId,
            CancellationToken cancellationToken) => context.ReadDurableSingleAsync(repository, "Stage4Intent.ReadOperation", MarketDataServiceDbSql.ReadDurableOperation, Values(Text(scope), Text(dataset), Uuid(operationId)),
            MarketDataServiceDbContext.MapToDurableOperation, cancellationToken);

        /// <summary>
        /// Performs the <c>ReadDurableSingleAsync</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task<T?> ReadDurableSingleAsync<T>(MarketDataServiceTransactionRepository repository, string commandName, string sql,
            object?[] parameters, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken) where T : class
        {
            using var commandContext = repository.Use(commandName, sql)
            .SetParameters(new DurableSubscriptionParameters(parameters));
            var ambient = repository.InTransaction() as NpgsqlCommand;
            if (ambient is null) return await commandContext.ExecuteSingleAsync(map, cancellationToken)
            .ConfigureAwait(false);
            await using var ambientDisposal = ambient.ConfigureAwait(false);
            // The current shared provider's query methods always open a separate connection, unlike its
            // command methods. Keep these transactional reads on this repository's owned transaction.
            // Do not use CommandBehavior.CloseConnection: only the transaction owner can close it.
            commandContext.SetCommand(ambient);
            PostgresValueBinding.AddParameters(ambient, parameters);
            var reader = await ambient.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken)
            .ConfigureAwait(false) ? map(new AdoNetDataRecord().SetReader(reader)) : null;
        }

        /// <summary>
        /// Performs the <c>CreateEmptyDurableSnapshot</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DurableSubscriptionSnapshot CreateEmptyDurableSnapshot(string scope, string dataset) => new(1, scope, dataset, 0, Array.Empty<DurableAuthorityState>());
        /// <summary>
        /// Performs the <c>CreateDurableRepository</c> operation for Market Data Service persistence.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal MarketDataServiceTransactionRepository CreateDurableRepository() => new(context._connection, context._logger);


    }

    extension(FuturesRolloverContractAssignment assignment)
    {
        /// <summary>Builds the PostgreSQL bind values for a contract assignment mutation.</summary>
        /// <param name="expectedRowVersion">The optimistic-concurrency row version.</param>
        /// <returns>The ordered PostgreSQL parameters.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal object?[] BindAssignment(long expectedRowVersion) => Values(
            Text(assignment.ContractRole.ToString()),
            Text(assignment.RootSymbol),
            Text(assignment.ContractId),
            Text(assignment.Description),
            Text(assignment.LocalSymbol),
            Text(assignment.SecurityType),
            Text(assignment.Currency),
            Text(assignment.Exchange),
            Text(assignment.Multiplier),
            Date(assignment.LastTradeDate),
            Date(assignment.NextRolloverDate),
            Text(assignment.SourceContractHash),
            TimestampTz(assignment.CreatedOnUtc),
            Text(assignment.CreatedBy),
            TimestampTz(assignment.UpdatedOnUtc),
            Text(assignment.UpdatedBy),
            Bigint(expectedRowVersion));
    }

    extension(string value)
    {
        /// <summary>Decodes a stored JSON document only if it fits the accepted size bound.</summary>
        internal T DeserializeDurable<T>(int maximumBytes)
        {
            if (Encoding.UTF8.GetByteCount(value) > maximumBytes) throw new InvalidDataException("Persisted payload exceeds its bound.");
            return JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new InvalidDataException("Persisted payload is invalid.");
        }
    }

    extension(DateTime value)
    {
        /// <summary>Marks a persisted PostgreSQL timestamp as UTC.</summary>
        /// <returns>The timestamp with <see cref="DateTimeKind.Utc"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateTime AsUtc() => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}

/// <summary>Persisted snapshot and optimistic revision.</summary>
internal sealed record DurableCurrentRow(string Json, long Revision);

/// <summary>Persisted identity existence query result.</summary>
internal sealed record DurableExistsRow(bool Exists);

/// <summary>Persisted idempotency digest and operation result.</summary>
internal sealed record DurableOperationRow(
    string Digest,
    TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.DurableIntentResult Result);

/// <summary>Write boundaries used by isolated fault-injection tests.</summary>
internal enum DurableStoreWriteStage
{
    CurrentIntent,
    OperationResult,
    Outbox,
    AuthorityWatermark,
    LeaseIdentity
}

/// <summary>Per-operation provider repository owning one subscription transaction.</summary>
internal sealed class MarketDataServiceTransactionRepository(
    IDbConnectionSetting connection,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<MarketDataServiceTransactionRepository>(connection, logger)
{
    public override IObjectRepository Database => this;
}
