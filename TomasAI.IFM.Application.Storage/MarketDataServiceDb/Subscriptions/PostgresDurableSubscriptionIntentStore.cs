using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions;

/// <summary>
/// Isolated G03 current-intent persistence. Each call owns its repository/transaction so concurrent
/// calls never share mutable transaction state. No schema creation, registration, authority lookup,
/// market-data subscription or production enablement is performed here.
/// </summary>
public sealed class PostgresDurableSubscriptionIntentStore : IDurableSubscriptionIntentStore
{
    private readonly IDbConnectionSetting _connection;
    private readonly ILogger<DbProvider> _logger;
    private readonly TimeProvider _time;
    private readonly Action<DurableStoreWriteStage>? _writeObserver;
    private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 32 };

    public PostgresDurableSubscriptionIntentStore(IDbConnectionSettings settings, ILogger<DbProvider> logger,
        TimeProvider? timeProvider = null) : this(settings, logger, timeProvider, null) { }

    internal PostgresDurableSubscriptionIntentStore(IDbConnectionSettings settings, ILogger<DbProvider> logger,
        TimeProvider? timeProvider, Action<DurableStoreWriteStage>? writeObserver)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _connection = settings[MarketDataServiceDbContext.MarketDataServiceDbConnection];
        if (_connection.ProviderName != "System.Data.Postgres")
            throw new ArgumentException("Stage 4 durable intent requires the PostgreSQL provider.");
        // The shared transaction provider opens/commits synchronously. Reject unbounded connection
        // and command settings; async statement work still observes the caller cancellation token.
        var options = new NpgsqlConnectionStringBuilder(_connection.ConnectionString);
        if (options.Timeout is <= 0 or > 30 || options.CommandTimeout is <= 0 or > 30)
            throw new ArgumentException("Stage 4 persistence requires 1-30 second connection/command timeouts.");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _time = timeProvider ?? TimeProvider.System;
        _writeObserver = writeObserver;
    }

    public async Task<DurableSubscriptionSnapshot> ReadAsync(string scope, string dataset,
        CancellationToken cancellationToken = default)
    {
        DurableSubscriptionContract.ValidateScope(scope, dataset);
        var row = await ReadCurrentAsync(NewRepository(), scope, dataset, false, cancellationToken).ConfigureAwait(false);
        return row ?? Empty(scope, dataset);
    }

    public async Task<DurableIntentResult?> FindOperationAsync(string scope, string dataset, Guid operationId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(scope, dataset, operationId);
        return (await ReadOperationAsync(NewRepository(), scope, dataset, operationId, cancellationToken)
            .ConfigureAwait(false))?.Result;
    }

    public async Task<DurableIntentResult> ApplyAsync(DurableAuthorityMutation mutation,
        CancellationToken cancellationToken = default)
    {
        var input = DurableSubscriptionContract.Freeze(mutation);
        var requestDigest = DurableSubscriptionContract.RequestDigest(input);
        var factDigest = DurableSubscriptionContract.FactDigest(input);
        cancellationToken.ThrowIfCancellationRequested();
        var repository = NewRepository();
        var transaction = repository.BeginTransaction() as PostgresObjectDataRepositoryTransaction<Repository>
            ?? throw new InvalidOperationException("PostgreSQL transaction was not created.");
        try
        {
            _ = await repository.Use("Stage4Intent.BoundTransaction", """
                    SET LOCAL lock_timeout = '5s';
                    SET LOCAL statement_timeout = '10s';
                    SET LOCAL idle_in_transaction_session_timeout = '15s';
                    """).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            // Insert or wait for the first writer; SELECT FOR UPDATE then serializes this dataset.
            _ = await repository.Use("Stage4Intent.EnsureCurrent", """
                    INSERT INTO market_data_service.stage4_intent_current(scope,dataset,revision,snapshot)
                    VALUES($1,$2,0,$3::jsonb) ON CONFLICT(scope,dataset) DO NOTHING;
                    """).SetParameters(new Parameters(Values(Text(input.Scope), Text(input.Dataset),
                        Text(JsonSerializer.Serialize(Empty(input.Scope, input.Dataset))))))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            var current = await ReadCurrentAsync(repository, input.Scope, input.Dataset, true, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("Locked current intent is missing.");
            var prior = await ReadOperationAsync(repository, input.Scope, input.Dataset, input.OperationId, cancellationToken)
                .ConfigureAwait(false);
            if (prior is not null)
            {
                transaction.Commit();
                return prior.Digest == requestDigest ? prior.Result
                    : new(input.OperationId, DurableIntentResultCode.OperationConflict, prior.Result.Revision, null);
            }

            var code = TryApply(current, input, factDigest, out var next);
            var currentIds = current.Authorities.SelectMany(value => value.Leases).Select(value => value.LeaseId).ToHashSet();
            var additions = input.Adds.Where(value => !currentIds.Contains(value.LeaseId)).ToArray();
            if (code == DurableIntentResultCode.Committed && additions.Length != 0)
            {
                var reserved = await ReadSingleAsync(repository, "Stage4Intent.CheckLeaseIdentity", """
                        SELECT EXISTS(SELECT 1 FROM market_data_service.stage4_lease_identity
                        WHERE scope=$1 AND dataset=$2 AND lease_id IN
                          (SELECT jsonb_array_elements_text($3::jsonb)::uuid));
                        """, Values(Text(input.Scope), Text(input.Dataset),
                            Text(JsonSerializer.Serialize(additions.Select(value => value.LeaseId)))),
                        row => new ExistsRow(row.GetBool(0)), cancellationToken).ConfigureAwait(false);
                if (reserved?.Exists == true)
                {
                    code = DurableIntentResultCode.LeaseConflict;
                    next = current;
                }
            }
            var now = _time.GetUtcNow();
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
                    var changed = await repository.Use("Stage4Intent.UpdateCurrent", """
                            UPDATE market_data_service.stage4_intent_current
                            SET revision=$3,snapshot=$4::jsonb WHERE scope=$1 AND dataset=$2 AND revision=$5;
                            """).SetParameters(new Parameters(Values(Text(input.Scope), Text(input.Dataset),
                                Bigint(next.Revision), Text(serialized), Bigint(current.Revision))))
                        .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                    if (changed.Sum() != 1) throw new InvalidOperationException("Current revision changed while locked.");
                    _writeObserver?.Invoke(DurableStoreWriteStage.CurrentIntent);

                    if (additions.Length != 0)
                    {
                        var identities = additions.Select(lease => new Parameters(Values(Text(input.Scope), Text(input.Dataset),
                            Uuid(lease.LeaseId), Text(input.SourceId), Text(DurableSubscriptionContract.Digest(input.Owner)),
                            Text(DurableSubscriptionContract.Digest(lease)), Bigint(next.Revision)))).ToArray();
                        _ = await repository.Use("Stage4Intent.ReserveLeaseIdentity", """
                                INSERT INTO market_data_service.stage4_lease_identity
                                (scope,dataset,lease_id,source_id,owner_digest,lease_digest,created_revision)
                                VALUES($1,$2,$3,$4,$5,$6,$7);
                                """).SetParameters(identities).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                    }
                    var remainingIds = next.Authorities.SelectMany(value => value.Leases).Select(value => value.LeaseId).ToHashSet();
                    var removedIds = currentIds.Where(value => !remainingIds.Contains(value)).ToArray();
                    if (removedIds.Length != 0)
                    {
                        var retired = await repository.Use("Stage4Intent.RetireLeaseIdentity", """
                                UPDATE market_data_service.stage4_lease_identity SET released_revision=$3
                                WHERE scope=$1 AND dataset=$2 AND released_revision IS NULL AND lease_id IN
                                  (SELECT jsonb_array_elements_text($4::jsonb)::uuid);
                                """).SetParameters(new Parameters(Values(Text(input.Scope), Text(input.Dataset), Bigint(next.Revision),
                                    Text(JsonSerializer.Serialize(removedIds)))))
                            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                        if (retired.Sum() != removedIds.Length)
                            throw new InvalidDataException("Current intent is missing its immutable lease identity reservation.");
                    }
                    _writeObserver?.Invoke(DurableStoreWriteStage.LeaseIdentity);
                }
            }

            _ = await repository.Use("Stage4Intent.InsertOperation", """
                    INSERT INTO market_data_service.stage4_intent_operation
                    (scope,dataset,operation_id,request_digest,result,created_at_utc) VALUES($1,$2,$3,$4,$5::jsonb,$6);
                    """).SetParameters(new Parameters(Values(Text(input.Scope), Text(input.Dataset), Uuid(input.OperationId),
                        Text(requestDigest), Text(JsonSerializer.Serialize(result)), TimestampTz(now.UtcDateTime))))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            _writeObserver?.Invoke(DurableStoreWriteStage.OperationResult);

            if (code == DurableIntentResultCode.Committed)
            {
                var outbox = new DurableSubscriptionOutboxItem(transitionId!.Value, input.OperationId, input.CorrelationId,
                    next.Revision, input.SourceId, input.SourceVersion, input.Status, input.ReasonCode, now);
                _ = await repository.Use("Stage4Intent.InsertOutbox", """
                        INSERT INTO market_data_service.stage4_intent_outbox
                        (scope,dataset,transition_id,operation_id,revision,payload,created_at_utc)
                        VALUES($1,$2,$3,$4,$5,$6::jsonb,$7);
                        """).SetParameters(new Parameters(Values(Text(input.Scope), Text(input.Dataset), Uuid(outbox.TransitionId),
                            Uuid(input.OperationId), Bigint(next.Revision), Text(JsonSerializer.Serialize(outbox)), TimestampTz(now.UtcDateTime))))
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                _writeObserver?.Invoke(DurableStoreWriteStage.Outbox);
                _ = await repository.Use("Stage4Intent.UpdateWatermark", """
                        INSERT INTO market_data_service.stage4_authority_watermark
                        (scope,dataset,source_id,source_version,source_event_id,fact_digest,owner_digest)
                        VALUES($1,$2,$3,$4,$5,$6,$7)
                        ON CONFLICT(scope,dataset,source_id) DO UPDATE SET
                          source_version=EXCLUDED.source_version,source_event_id=EXCLUDED.source_event_id,
                          fact_digest=EXCLUDED.fact_digest,owner_digest=EXCLUDED.owner_digest;
                        """).SetParameters(new Parameters(Values(Text(input.Scope), Text(input.Dataset), Text(input.SourceId),
                            Bigint(input.SourceVersion), Uuid(input.SourceEventId), Text(factDigest),
                            Text(DurableSubscriptionContract.Digest(input.Owner)))))
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                _writeObserver?.Invoke(DurableStoreWriteStage.AuthorityWatermark);
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

    public async Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingOutboxAsync(string scope, string dataset,
        int pageSize = 100, CancellationToken cancellationToken = default)
    {
        DurableSubscriptionContract.ValidateScope(scope, dataset);
        if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var items = await NewRepository().Use("Stage4Intent.ReadOutbox", """
                SELECT payload::text FROM market_data_service.stage4_intent_outbox
                WHERE scope=$1 AND dataset=$2 AND delivered_at_utc IS NULL ORDER BY revision LIMIT $3;
                """).SetParameters(new Parameters(Values(Text(scope), Text(dataset), Integer(pageSize))))
            .ExecuteQueryAsync(row => Deserialize<DurableSubscriptionOutboxItem>(row.GetString(0), 4096), cancellationToken)
            .ConfigureAwait(false);
        return Array.AsReadOnly(items.ToArray());
    }

    public async Task<bool> AcknowledgeOutboxAsync(string scope, string dataset, Guid transitionId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(scope, dataset, transitionId);
        var changed = await NewRepository().Use("Stage4Intent.AcknowledgeOutbox", """
                UPDATE market_data_service.stage4_intent_outbox SET delivered_at_utc=COALESCE(delivered_at_utc,$4)
                WHERE scope=$1 AND dataset=$2 AND transition_id=$3;
                """).SetParameters(new Parameters(Values(Text(scope), Text(dataset), Uuid(transitionId), TimestampTz(_time.GetUtcNow().UtcDateTime))))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return changed.Sum() == 1;
    }

    private static DurableIntentResultCode TryApply(DurableSubscriptionSnapshot current, DurableAuthorityMutation input,
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
        if (input.SourceVersion != (source?.SourceVersion ?? 0) + 1) return DurableIntentResultCode.AuthorityGap;
        if (input.ExpectedRevision != current.Revision) return DurableIntentResultCode.RevisionConflict;
        if (current.Revision == long.MaxValue) return DurableIntentResultCode.CapacityExceeded;
        var leases = source?.Leases.ToDictionary(value => value.LeaseId) ?? [];
        if (input.Status == DurableAuthorityStatus.Terminal) leases.Clear();
        else if (input.Status == DurableAuthorityStatus.Active)
        {
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

    private static async Task<DurableSubscriptionSnapshot?> ReadCurrentAsync(Repository repository, string scope, string dataset,
        bool lockRow, CancellationToken cancellationToken)
    {
        var row = await ReadSingleAsync(repository, "Stage4Intent.ReadCurrent", """
                SELECT snapshot::text,revision FROM market_data_service.stage4_intent_current WHERE scope=$1 AND dataset=$2
                """ + (lockRow ? " FOR UPDATE;" : ";"), Values(Text(scope), Text(dataset)),
            record => new CurrentRow(record.GetString(0), record.GetLong(1)), cancellationToken).ConfigureAwait(false);
        if (row is null) return null;
        var result = DurableSubscriptionContract.Freeze(Deserialize<DurableSubscriptionSnapshot>(row.Json,
            DurableSubscriptionContract.MaximumSnapshotBytes));
        if (result.Scope != scope || result.Dataset != dataset || result.Revision != row.Revision)
            throw new InvalidDataException("Persisted intent scope or revision does not match its row.");
        return result;
    }

    private static Task<OperationRow?> ReadOperationAsync(Repository repository, string scope, string dataset, Guid operationId,
        CancellationToken cancellationToken) => ReadSingleAsync(repository, "Stage4Intent.ReadOperation", """
            SELECT request_digest,result::text FROM market_data_service.stage4_intent_operation
            WHERE scope=$1 AND dataset=$2 AND operation_id=$3;
            """, Values(Text(scope), Text(dataset), Uuid(operationId)),
        row => new OperationRow(row.GetString(0), Deserialize<DurableIntentResult>(row.GetString(1), 4096)), cancellationToken);

    private static async Task<T?> ReadSingleAsync<T>(Repository repository, string commandName, string sql,
        NpgsqlParameter[] parameters, Func<IObjectDataRecord, T> map, CancellationToken cancellationToken) where T : class
    {
        using var context = repository.Use(commandName, sql).SetParameters(new Parameters(parameters));
        await using var ambient = repository.InTransaction() as NpgsqlCommand;
        if (ambient is null) return await context.ExecuteSingleAsync(map, cancellationToken).ConfigureAwait(false);
        // The current shared provider's query methods always open a separate connection, unlike its
        // command methods. Keep these transactional reads on this repository's owned transaction.
        // Do not use CommandBehavior.CloseConnection: only the transaction owner can close it.
        context.SetCommand(ambient);
        ambient.Parameters.AddRange(parameters);
        await using var reader = await ambient.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? map(new AdoNetDataRecord().SetReader(reader)) : null;
    }

    private static T Deserialize<T>(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) > maximumBytes) throw new InvalidDataException("Persisted payload exceeds its bound.");
        return JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new InvalidDataException("Persisted payload is invalid.");
    }

    private static DurableSubscriptionSnapshot Empty(string scope, string dataset) => new(1, scope, dataset, 0, Array.Empty<DurableAuthorityState>());
    private static void ValidateIdentity(string scope, string dataset, Guid id)
    {
        DurableSubscriptionContract.ValidateScope(scope, dataset);
        if (id == Guid.Empty) throw new ArgumentException("A non-empty identifier is required.");
    }
    private Repository NewRepository() => new(_connection, _logger);
    private sealed record CurrentRow(string Json, long Revision);
    private sealed record OperationRow(string Digest, DurableIntentResult Result);
    private sealed record ExistsRow(bool Exists);
    private readonly record struct Parameters(NpgsqlParameter[] Items) : IBindValue { public object Bind() => Items; }
    private sealed class Repository(IDbConnectionSetting connection, ILogger<DbProvider> logger)
        : ObjectDataRepository<Repository>(connection, logger)
    {
        public override IObjectRepository Database => this;
    }
}

internal enum DurableStoreWriteStage { CurrentIntent, OperationResult, Outbox, AuthorityWatermark, LeaseIdentity }
