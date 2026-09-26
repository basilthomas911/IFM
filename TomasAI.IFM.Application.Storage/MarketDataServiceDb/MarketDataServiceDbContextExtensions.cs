using System.Text;
using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

internal static class MarketDataServiceDbContextExtensions
{
    extension(MarketDataServiceDbContext context)
    {
        /// <summary>Gets a watchdog observation by its durable identity.</summary>
        /// <param name="id">The observation identity.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
        /// <returns>The matching observation, or <see langword="null"/> when none exists.</returns>
        internal Task<DatabentoWatchdogObservation?> GetObservationByIdentityAsync(Guid id, CancellationToken cancellationToken)
            => context.Database
                .Use("MarketDataService.GetObservationByIdentity", MarketDataServiceDbSql.GetObservationByIdentity)
                .SetParameters(new IdentityParameter(id))
                .ExecuteSingleAsync<DatabentoWatchdogObservation?>(MarketDataServiceDbContext.MapToObservation, cancellationToken);

        /// <summary>
        /// Performs the <c>ReadDurableSubscriptionAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal async Task<DurableSubscriptionSnapshot> ReadDurableSubscriptionAsync(string scope, string dataset,
            CancellationToken cancellationToken = default)
        {
            DurableSubscriptionContract.ValidateScope(scope, dataset);
            var row = await context.ReadDurableCurrentAsync(context.CreateDurableRepository(), scope, dataset, false, cancellationToken)
                .ConfigureAwait(false);
            return row ?? context.CreateEmptyDurableSnapshot(scope, dataset);
        }

        /// <summary>
        /// Performs the <c>FindDurableOperationAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal async Task<DurableIntentResult?> FindDurableOperationAsync(string scope, string dataset, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            context.ValidateDurableIdentity(scope, dataset, operationId);
            return (await context.ReadDurableOperationAsync(context.CreateDurableRepository(), scope, dataset, operationId, cancellationToken)
                .ConfigureAwait(false))?.Result;
        }

        /// <summary>
        /// Performs the <c>ApplyDurableSubscriptionAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal async Task<DurableIntentResult> ApplyDurableSubscriptionAsync(DurableAuthorityMutation mutation,
            CancellationToken cancellationToken = default)
        {
            var input = DurableSubscriptionContract.Freeze(mutation);
            var requestDigest = DurableSubscriptionContract.RequestDigest(input);
            var factDigest = DurableSubscriptionContract.FactDigest(input);
            cancellationToken.ThrowIfCancellationRequested();
            var repository = context.CreateDurableRepository();
            var transaction = repository.BeginTransaction() as PostgresObjectDataRepositoryTransaction<MarketDataServiceTransactionRepository>
                ?? throw new InvalidOperationException("PostgreSQL transaction was not created.");
            try
            {
                _ = await repository.Use("Stage4Intent.BoundTransaction", MarketDataServiceDbSql.ConfigureDurableTransaction)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
                // Insert or wait for the first writer; SELECT FOR UPDATE then serializes this dataset.
                _ = await repository.Use("Stage4Intent.EnsureCurrent", MarketDataServiceDbSql.EnsureDurableCurrent)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset),
                            Text(JsonSerializer.Serialize(context.CreateEmptyDurableSnapshot(input.Scope, input.Dataset))))))
                    .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
                var current = await context.ReadDurableCurrentAsync(repository, input.Scope, input.Dataset, true, cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidDataException("Locked current intent is missing.");
                var prior = await context.ReadDurableOperationAsync(repository, input.Scope, input.Dataset, input.OperationId, cancellationToken)
                    .ConfigureAwait(false);
                if (prior is not null)
                {
                    transaction.Commit();
                    return prior.Digest == requestDigest ? prior.Result
                        : new(input.OperationId, DurableIntentResultCode.OperationConflict, prior.Result.Revision, null);
                }

                var code = context.TryApplyDurableSubscription(current, input, factDigest, out var next);
                var currentIds = current.Authorities.SelectMany(value => value.Leases).Select(value => value.LeaseId).ToHashSet();
                var additions = input.Adds.Where(value => !currentIds.Contains(value.LeaseId)).ToArray();
                if (code == DurableIntentResultCode.Committed && additions.Length != 0)
                {
                    var reserved = await context.ReadDurableSingleAsync(repository, "Stage4Intent.CheckLeaseIdentity", """
                            SELECT EXISTS(SELECT 1 FROM market_data_service.stage4_lease_identity
                            WHERE scope=$1 AND dataset=$2 AND lease_id IN
                              (SELECT jsonb_array_elements_text($3::jsonb)::uuid));
                            """, Values(Text(input.Scope), Text(input.Dataset),
                                Text(JsonSerializer.Serialize(additions.Select(value => value.LeaseId)))),
                            row => new DurableExistsRow(row.GetBool(0)), cancellationToken)
                .ConfigureAwait(false);
                    if (reserved?.Exists == true)
                    {
                        code = DurableIntentResultCode.LeaseConflict;
                        next = current;
                    }
                }
                var now = context._time.GetUtcNow();
                var transitionId = code == DurableIntentResultCode.Committed ? Guid.NewGuid() : (Guid?)null;
                var result = new DurableIntentResult(input.OperationId, code, next.Revision, transitionId);
                if (code == DurableIntentResultCode.Committed)
                {
                    var serialized = JsonSerializer.Serialize(next);
                    if (Encoding.UTF8.GetByteCount(serialized) > DurableSubscriptionContract.MaximumSnapshotBytes)
                    {
                        code = DurableIntentResultCode.CapacityExceeded;
                        next = current;
                        result = new(input.OperationId, code, current.Revision, null);
                    }
                    else
                    {
                        var changed = await repository.Use("Stage4Intent.UpdateCurrent", MarketDataServiceDbSql.UpdateDurableCurrent)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset),
                                    Bigint(next.Revision), Text(serialized), Bigint(current.Revision))))
                            .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
                        if (changed.Sum() != 1) throw new InvalidOperationException("Current revision changed while locked.");
                        context._writeObserver?.Invoke(DurableStoreWriteStage.CurrentIntent);

                        if (additions.Length != 0)
                        {
                            var identities = additions.Select(lease => new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset),
                                Uuid(lease.LeaseId), Text(input.SourceId), Text(DurableSubscriptionContract.Digest(input.Owner)),
                                Text(DurableSubscriptionContract.Digest(lease)), Bigint(next.Revision)))).ToArray();
                            _ = await repository.Use("Stage4Intent.ReserveLeaseIdentity", MarketDataServiceDbSql.ReserveDurableLeaseIdentity)
                .SetParameters(identities)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
                        }
                        var remainingIds = next.Authorities.SelectMany(value => value.Leases).Select(value => value.LeaseId).ToHashSet();
                        var removedIds = currentIds.Where(value => !remainingIds.Contains(value)).ToArray();
                        if (removedIds.Length != 0)
                        {
                            var retired = await repository.Use("Stage4Intent.RetireLeaseIdentity", MarketDataServiceDbSql.RetireDurableLeaseIdentity)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Bigint(next.Revision),
                                        Text(JsonSerializer.Serialize(removedIds)))))
                                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
                            if (retired.Sum() != removedIds.Length)
                                throw new InvalidDataException("Current intent is missing its immutable lease identity reservation.");
                        }
                        context._writeObserver?.Invoke(DurableStoreWriteStage.LeaseIdentity);
                    }
                }

                _ = await repository.Use("Stage4Intent.InsertOperation", MarketDataServiceDbSql.InsertDurableOperation)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Uuid(input.OperationId),
                            Text(requestDigest), Text(JsonSerializer.Serialize(result)), TimestampTz(now.UtcDateTime))))
                    .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
                context._writeObserver?.Invoke(DurableStoreWriteStage.OperationResult);

                if (code == DurableIntentResultCode.Committed)
                {
                    var outbox = new DurableSubscriptionOutboxItem(transitionId!.Value, input.OperationId, input.CorrelationId,
                        next.Revision, input.SourceId, input.SourceVersion, input.Status, input.ReasonCode, now);
                    _ = await repository.Use("Stage4Intent.InsertOutbox", MarketDataServiceDbSql.InsertDurableOutbox)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Uuid(outbox.TransitionId),
                                Uuid(input.OperationId), Bigint(next.Revision), Text(JsonSerializer.Serialize(outbox)), TimestampTz(now.UtcDateTime))))
                        .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
                    context._writeObserver?.Invoke(DurableStoreWriteStage.Outbox);
                    _ = await repository.Use("Stage4Intent.UpdateWatermark", MarketDataServiceDbSql.UpdateDurableWatermark)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(input.Scope), Text(input.Dataset), Text(input.SourceId),
                                Bigint(input.SourceVersion), Uuid(input.SourceEventId), Text(factDigest),
                                Text(DurableSubscriptionContract.Digest(input.Owner)))))
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
                try { transaction.Rollback(); } catch { }
                throw;
            }
            finally
            {
                // The shared provider normally closes and clears these in Commit/Rollback. A transport
                // failure can throw before that cleanup; dispose only this call's remaining owned state.
                // Never replace the original database/cancellation exception with a cleanup failure.
                try { transaction.Transaction?.Dispose(); } catch { }
                try { transaction.Connection?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Performs the <c>ReadPendingDurableOutboxAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal async Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingDurableOutboxAsync(string scope, string dataset,
            int pageSize = 100, CancellationToken cancellationToken = default)
        {
            DurableSubscriptionContract.ValidateScope(scope, dataset);
            if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
            var items = await context.CreateDurableRepository().Use("Stage4Intent.ReadOutbox", MarketDataServiceDbSql.ReadDurableOutbox)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(scope), Text(dataset), Integer(pageSize))))
                .ExecuteQueryAsync(row => context.DeserializeDurable<DurableSubscriptionOutboxItem>(row.GetString(0), 4096), cancellationToken)
                .ConfigureAwait(false);
            return Array.AsReadOnly(items.ToArray());
        }

        /// <summary>
        /// Performs the <c>AcknowledgeDurableOutboxAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal async Task<bool> AcknowledgeDurableOutboxAsync(string scope, string dataset, Guid transitionId,
            CancellationToken cancellationToken = default)
        {
            context.ValidateDurableIdentity(scope, dataset, transitionId);
            var changed = await context.CreateDurableRepository().Use("Stage4Intent.AcknowledgeOutbox", MarketDataServiceDbSql.AcknowledgeDurableOutbox)
                .SetParameters(new DurableSubscriptionParameters(Values(Text(scope), Text(dataset), Uuid(transitionId), TimestampTz(context._time.GetUtcNow().UtcDateTime))))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            return changed.Sum() == 1;
        }

        /// <summary>
        /// Performs the <c>TryApplyDurableSubscription</c> operation for Market Data Service persistence.
        /// </summary>
        internal DurableIntentResultCode TryApplyDurableSubscription(DurableSubscriptionSnapshot current, DurableAuthorityMutation input,
            string factDigest, out DurableSubscriptionSnapshot next)
        {
            next = current;
            var source = current.Authorities.SingleOrDefault(value => value.SourceId == input.SourceId);
            if (source is not null && source.Owner != input.Owner
                || current.Authorities.Any(value => value.Owner == input.Owner && value.SourceId != input.SourceId))
                return DurableIntentResultCode.AuthorityConflict;
            if (source is not null && input.SourceVersion < source.SourceVersion) return DurableIntentResultCode.StaleAuthority;
            if (source is not null && input.SourceVersion == source.SourceVersion)
                return source.FactDigest == factDigest ? DurableIntentResultCode.AlreadyApplied : DurableIntentResultCode.AuthorityConflict;
            // Deltas require contiguous delivery. A separately identified complete committed snapshot
            // carries the real source watermark and can safely cover intermediate non-ownership events.
            if (!input.CompleteSourceSnapshot && input.SourceVersion != (source?.SourceVersion ?? 0) + 1)
                return DurableIntentResultCode.AuthorityGap;
            if (input.ExpectedRevision != current.Revision) return DurableIntentResultCode.RevisionConflict;
            if (current.Revision == long.MaxValue) return DurableIntentResultCode.CapacityExceeded;
            var leases = source?.Leases.ToDictionary(value => value.LeaseId) ?? [];
            if (input.Status == DurableAuthorityStatus.Terminal) leases.Clear();
            else if (input.Status == DurableAuthorityStatus.Active)
            {
                if (input.CompleteSourceSnapshot)
                    foreach (var id in leases.Keys.Where(id => !input.Adds.Any(x => x.LeaseId == id)).ToArray()) leases.Remove(id);
                foreach (var release in input.Releases)
                {
                    if (!leases.TryGetValue(release.LeaseId, out var existing) || existing.LeaseVersion != release.ExpectedLeaseVersion)
                        return DurableIntentResultCode.LeaseConflict;
                    leases.Remove(release.LeaseId);
                }
                foreach (var add in input.Adds)
                {
                    if (leases.TryGetValue(add.LeaseId, out var existing))
                    {
                        if (existing != add) return DurableIntentResultCode.LeaseConflict;
                        continue;
                    }
                    if (add.LeaseVersion != 1 || leases.Values.Any(value => value.Purpose == add.Purpose && value.Ticker == add.Ticker)
                        || current.Authorities.Where(value => value.SourceId != input.SourceId)
                            .Any(value => value.Leases.Any(lease => lease.LeaseId == add.LeaseId)))
                        return DurableIntentResultCode.LeaseConflict;
                    leases.Add(add.LeaseId, add);
                }
            }
            if (leases.Count > DurableSubscriptionContract.MaximumOwnerLeases
                || current.Authorities.Count(value => value.SourceId != input.SourceId) + 1 > DurableSubscriptionContract.MaximumAuthorities
                || current.Authorities.Where(value => value.SourceId != input.SourceId).Sum(value => value.Leases.Count) + leases.Count
                    > DurableSubscriptionContract.MaximumLeases)
                return DurableIntentResultCode.CapacityExceeded;
            var updated = new DurableAuthorityState(input.SourceId, input.SourceVersion, input.SourceEventId, factDigest,
                input.Owner, input.Status, input.ReasonCode, leases.Values.ToArray());
            next = DurableSubscriptionContract.Freeze(current with
            {
                Revision = current.Revision + 1,
                Authorities = current.Authorities.Where(value => value.SourceId != input.SourceId).Append(updated).ToArray()
            });
            return DurableIntentResultCode.Committed;
        }

        /// <summary>
        /// Performs the <c>ReadDurableCurrentAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal async Task<DurableSubscriptionSnapshot?> ReadDurableCurrentAsync(MarketDataServiceTransactionRepository repository, string scope, string dataset,
            bool lockRow, CancellationToken cancellationToken)
        {
            var sql = lockRow
                ? MarketDataServiceDbSql.ReadDurableCurrentForUpdate
                : MarketDataServiceDbSql.ReadDurableCurrent;
            var row = await context.ReadDurableSingleAsync(repository, "Stage4Intent.ReadCurrent", sql, Values(Text(scope), Text(dataset)),
                record => new DurableCurrentRow(record.GetString(0), record.GetLong(1)), cancellationToken)
                .ConfigureAwait(false);
            if (row is null) return null;
            var result = DurableSubscriptionContract.Freeze(context.DeserializeDurable<DurableSubscriptionSnapshot>(row.Json,
                DurableSubscriptionContract.MaximumSnapshotBytes));
            if (result.Scope != scope || result.Dataset != dataset || result.Revision != row.Revision)
                throw new InvalidDataException("Persisted intent scope or revision does not match its row.");
            return result;
        }

        /// <summary>
        /// Performs the <c>ReadDurableOperationAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal Task<DurableOperationRow?> ReadDurableOperationAsync(MarketDataServiceTransactionRepository repository, string scope, string dataset, Guid operationId,
            CancellationToken cancellationToken) => context.ReadDurableSingleAsync(repository, "Stage4Intent.ReadOperation", MarketDataServiceDbSql.ReadDurableOperation, Values(Text(scope), Text(dataset), Uuid(operationId)),
            row => new DurableOperationRow(row.GetString(0), context.DeserializeDurable<DurableIntentResult>(row.GetString(1), 4096)), cancellationToken);

        /// <summary>
        /// Performs the <c>ReadDurableSingleAsync</c> operation for Market Data Service persistence.
        /// </summary>
        internal async Task<T?> ReadDurableSingleAsync<T>(MarketDataServiceTransactionRepository repository, string commandName, string sql,
            NpgsqlParameter[] parameters, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken) where T : class
        {
            using var commandContext = repository.Use(commandName, sql)
                .SetParameters(new DurableSubscriptionParameters(parameters));
            await using var ambient = repository.InTransaction() as NpgsqlCommand;
            if (ambient is null) return await commandContext.ExecuteSingleAsync(map, cancellationToken)
                .ConfigureAwait(false);
            // The current shared provider's query methods always open a separate connection, unlike its
            // command methods. Keep these transactional reads on this repository's owned transaction.
            // Do not use CommandBehavior.CloseConnection: only the transaction owner can close it.
            commandContext.SetCommand(ambient);
            ambient.Parameters.AddRange(parameters);
            await using var reader = await ambient.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false) ? map(new AdoNetDataRecord().SetReader(reader)) : null;
        }

        /// <summary>
        /// Performs the <c>DeserializeDurable</c> operation for Market Data Service persistence.
        /// </summary>
        internal T DeserializeDurable<T>(string value, int maximumBytes)
        {
            if (Encoding.UTF8.GetByteCount(value) > maximumBytes) throw new InvalidDataException("Persisted payload exceeds its bound.");
            return JsonSerializer.Deserialize<T>(value, new JsonSerializerOptions { MaxDepth = 32 }) ?? throw new InvalidDataException("Persisted payload is invalid.");
        }

        /// <summary>
        /// Performs the <c>CreateEmptyDurableSnapshot</c> operation for Market Data Service persistence.
        /// </summary>
        internal DurableSubscriptionSnapshot CreateEmptyDurableSnapshot(string scope, string dataset) => new(1, scope, dataset, 0, Array.Empty<DurableAuthorityState>());
        /// <summary>
        /// Performs the <c>ValidateDurableIdentity</c> operation for Market Data Service persistence.
        /// </summary>
        internal void ValidateDurableIdentity(string scope, string dataset, Guid id)
        {
            DurableSubscriptionContract.ValidateScope(scope, dataset);
            if (id == Guid.Empty) throw new ArgumentException("A non-empty identifier is required.");
        }
        /// <summary>
        /// Performs the <c>CreateDurableRepository</c> operation for Market Data Service persistence.
        /// </summary>
        internal MarketDataServiceTransactionRepository CreateDurableRepository() => new(context._connection, context._logger);


    }

    extension(FuturesRolloverContractAssignment assignment)
    {
        /// <summary>Builds the PostgreSQL bind values for a contract assignment mutation.</summary>
        /// <param name="expectedRowVersion">The optimistic-concurrency row version.</param>
        /// <returns>The ordered PostgreSQL parameters.</returns>
        internal Npgsql.NpgsqlParameter[] BindAssignment(long expectedRowVersion) => Values(
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

    extension(DateTime value)
    {
        /// <summary>Marks a persisted PostgreSQL timestamp as UTC.</summary>
        /// <returns>The timestamp with <see cref="DateTimeKind.Utc"/>.</returns>
        internal DateTime AsUtc() => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
