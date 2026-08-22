using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Journal;

public sealed class SqliteDatabaseBackupExecutionJournal : IDatabaseBackupExecutionJournal
{
    static int _sqliteInitialized;
    readonly DatabaseBackupJournalOptions _options;
    readonly DatabaseBackupHostId _hostId;
    readonly ILogger<SqliteDatabaseBackupExecutionJournal> _logger;
    readonly string _databasePath;
    readonly string _connectionString;

    public SqliteDatabaseBackupExecutionJournal(
        DatabaseBackupJournalOptions options,
        DatabaseBackupHostOptions hostOptions,
        ILogger<SqliteDatabaseBackupExecutionJournal> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(hostOptions);
        hostOptions.Validate();
        _hostId = new DatabaseBackupHostId(hostOptions.HostId);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databasePath = options.ValidateAndResolvePath();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = Math.Max(1, options.BusyTimeoutMilliseconds / 1_000)
        }.ToString();
        InitializeNativeSqlite();
    }

    public string DatabasePath => _databasePath;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_databasePath)
            ?? throw new InvalidOperationException("The DatabaseBackup journal path has no parent directory.");
        Directory.CreateDirectory(directory);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA foreign_keys=ON;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, $"PRAGMA busy_timeout={_options.BusyTimeoutMilliseconds};", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, _options.EnableWriteAheadLog ? "PRAGMA journal_mode=WAL;" : "PRAGMA journal_mode=DELETE;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA synchronous=FULL;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, DatabaseBackupJournalSchema.Sql, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("DatabaseBackup execution journal initialized at configured path.");
    }

    public async ValueTask VerifyIntegrityAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The DatabaseBackup execution journal failed its SQLite integrity check.");
    }

    public async ValueTask<JournalAdmissionResult> AdmitAsync(
        DatabaseExecutionIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.Validate();
        var serialized = DatabaseBackupContractSerializer.Serialize(intent.ExecutionEvent);
        var definitionHash = DatabaseBackupContractSerializer.DefinitionHash(intent.ExecutionEvent);
        var now = DateTimeOffset.UtcNow;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        var existingInboxHash = await ScalarStringAsync(
            connection, transaction,
            "SELECT content_hash FROM journal_inbox WHERE event_id=$event_id;",
            cancellationToken,
            ("$event_id", intent.ExecutionEvent.Id.ToString("D"))).ConfigureAwait(false);
        if (existingInboxHash is not null)
        {
            if (!StringComparer.Ordinal.Equals(existingInboxHash, serialized.Hash))
                throw new DatabaseExecutionConflictException("An execution event ID was replayed with conflicting content.");
            transaction.Commit();
            return new JournalAdmissionResult(intent.OperationId, JournalAdmissionOutcome.ExactDuplicate);
        }

        var operationId = intent.OperationId.Value.ToString("D");
        var existingDefinitionHash = await ScalarStringAsync(
            connection, transaction,
            "SELECT definition_hash FROM journal_operation WHERE operation_id=$operation_id;",
            cancellationToken,
            ("$operation_id", operationId)).ConfigureAwait(false);
        if (existingDefinitionHash is not null && !StringComparer.Ordinal.Equals(existingDefinitionHash, definitionHash))
            throw new DatabaseExecutionConflictException("An operation ID was replayed with a conflicting immutable definition.");

        if (existingDefinitionHash is null)
        {
            await ExecuteAsync(connection, transaction, """
INSERT INTO journal_operation
    (operation_id, source, operation_kind, protection_set_id, definition_hash,
     intent_event_id, intent_type, intent_json, phase, terminal, admitted_utc, updated_utc)
VALUES ($operation_id,$source,$kind,$protection_set,$definition_hash,
        $event_id,$event_type,$event_json,$phase,0,$now,$now);
""", cancellationToken,
                ("$operation_id", operationId),
                ("$source", (short)intent.Source),
                ("$kind", (short)intent.ExecutionEvent.Source.OperationKind),
                ("$protection_set", intent.ExecutionEvent.Source.ProtectionSetId.Value),
                ("$definition_hash", definitionHash),
                ("$event_id", intent.ExecutionEvent.Id.ToString("D")),
                ("$event_type", serialized.TypeName),
                ("$event_json", serialized.Payload),
                ("$phase", (short)DatabaseRecoveryPhase.Admitted),
                ("$now", Format(now))).ConfigureAwait(false);
        }

        await ExecuteAsync(connection, transaction, """
INSERT INTO journal_inbox (event_id, operation_id, content_hash, admitted_utc)
VALUES ($event_id,$operation_id,$content_hash,$now);
""", cancellationToken,
            ("$event_id", intent.ExecutionEvent.Id.ToString("D")),
            ("$operation_id", operationId),
            ("$content_hash", serialized.Hash),
            ("$now", Format(now))).ConfigureAwait(false);

        if (existingDefinitionHash is null)
        {
            var accepted = DatabaseBackupServiceEventFactory.Accepted(intent, _hostId);
            await InsertOutboxAsync(connection, transaction, accepted, cancellationToken).ConfigureAwait(false);
        }

        transaction.Commit();
        return new JournalAdmissionResult(intent.OperationId, JournalAdmissionOutcome.Admitted);
    }

    public async ValueTask<JournalLease?> TryAcquireLeaseAsync(
        DatabaseRecoveryOperationId operationId,
        DatabaseBackupHostId hostId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var values = await ReadLeaseStateAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false);
        if (values is null || values.Value.Terminal
            || (values.Value.ExpiresUtc > DateTimeOffset.UtcNow
                && !StringComparer.Ordinal.Equals(values.Value.HostId, hostId.Value)))
        {
            transaction.Commit();
            return null;
        }

        var token = checked(values.Value.FencingToken + 1);
        var expires = DateTimeOffset.UtcNow.Add(leaseDuration);
        var changed = await ExecuteAsync(connection, transaction, """
UPDATE journal_operation
SET lease_host_id=$host_id, lease_expires_utc=$expires, fencing_token=$token, updated_utc=$now
WHERE operation_id=$operation_id AND terminal=0 AND fencing_token=$expected_token;
""", cancellationToken,
            ("$host_id", hostId.Value), ("$expires", Format(expires)), ("$token", token),
            ("$now", Format(DateTimeOffset.UtcNow)), ("$operation_id", operationId.Value.ToString("D")),
            ("$expected_token", values.Value.FencingToken)).ConfigureAwait(false);
        if (changed != 1)
        {
            transaction.Rollback();
            return null;
        }
        transaction.Commit();
        return new JournalLease(operationId, hostId, token, expires, leaseDuration);
    }

    public async ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var expires = DateTimeOffset.UtcNow.Add(lease.LeaseDuration);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var changed = await ExecuteAsync(connection, null, """
UPDATE journal_operation
SET lease_expires_utc=$expires, updated_utc=$now
WHERE operation_id=$operation_id AND lease_host_id=$host_id AND fencing_token=$token AND terminal=0;
""", cancellationToken,
            ("$expires", Format(expires)), ("$now", Format(DateTimeOffset.UtcNow)),
            ("$operation_id", lease.OperationId.Value.ToString("D")), ("$host_id", lease.HostId.Value),
            ("$token", lease.FencingToken)).ConfigureAwait(false);
        if (changed != 1) throw new DatabaseLeaseLostException(lease.OperationId);
    }

    public async ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var changed = await ExecuteAsync(connection, transaction, """
UPDATE journal_operation
SET phase=$phase, terminal=$terminal,
    lease_host_id=CASE WHEN $terminal=1 THEN NULL ELSE lease_host_id END,
    lease_expires_utc=CASE WHEN $terminal=1 THEN NULL ELSE lease_expires_utc END,
    updated_utc=$observed
WHERE operation_id=$operation_id AND lease_host_id=$host_id AND fencing_token=$token AND terminal=0;
""", cancellationToken,
            ("$phase", (short)checkpoint.Phase), ("$terminal", checkpoint.Terminal ? 1 : 0),
            ("$observed", Format(checkpoint.ObservedUtc)), ("$operation_id", checkpoint.OperationId.Value.ToString("D")),
            ("$host_id", checkpoint.HostId.Value), ("$token", checkpoint.FencingToken)).ConfigureAwait(false);
        if (changed != 1) throw new DatabaseLeaseLostException(checkpoint.OperationId);
        await ExecuteAsync(connection, transaction, """
INSERT INTO journal_checkpoint
    (operation_id, fencing_token, phase, terminal, safe_diagnostic_reference, observed_utc)
VALUES ($operation_id,$token,$phase,$terminal,$diagnostic,$observed)
ON CONFLICT (operation_id, fencing_token, phase) DO UPDATE SET
    terminal=excluded.terminal,
    safe_diagnostic_reference=excluded.safe_diagnostic_reference,
    observed_utc=excluded.observed_utc;
""", cancellationToken,
            ("$operation_id", checkpoint.OperationId.Value.ToString("D")), ("$token", checkpoint.FencingToken),
            ("$phase", (short)checkpoint.Phase), ("$terminal", checkpoint.Terminal ? 1 : 0),
            ("$diagnostic", checkpoint.SafeDiagnosticReference), ("$observed", Format(checkpoint.ObservedUtc))).ConfigureAwait(false);
        transaction.Commit();
    }

    public async ValueTask EnqueueServiceEventAsync(
        DatabaseServiceEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        envelope.Event.Validate();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await InsertOutboxAsync(connection, transaction, envelope.Event, cancellationToken).ConfigureAwait(false);
        transaction.Commit();
    }

    public async IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(
        int maximumCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (maximumCount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCount));
        var pending = new List<PendingServiceEvent>();
        await using (var connection = await OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
SELECT event_id, operation_id, service_sequence, event_type, event_json, publish_attempts
FROM journal_outbox
WHERE published=0
ORDER BY operation_id, service_sequence
LIMIT $maximum_count;
""";
            command.Parameters.AddWithValue("$maximum_count", maximumCount);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var @event = DatabaseBackupContractSerializer.Deserialize(reader.GetString(3), reader.GetString(4));
                pending.Add(new PendingServiceEvent(
                    Guid.Parse(reader.GetString(0)),
                    new DatabaseRecoveryOperationId(Guid.Parse(reader.GetString(1))),
                    reader.GetInt64(2),
                    (DatabaseBackupServiceEventContract)@event,
                    reader.GetInt32(5)));
            }
        }

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public async ValueTask MarkServiceEventPublishedAsync(
        Guid eventId,
        DateTimeOffset publishedUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var changed = await ExecuteAsync(connection, transaction, """
UPDATE journal_outbox
SET published=1, published_utc=$published_utc, publish_attempts=publish_attempts+1
WHERE event_id=$event_id AND published=0;
UPDATE journal_run_stats
SET published=1
WHERE (operation_id, statistics_revision) = (
    SELECT operation_id, service_sequence FROM journal_outbox WHERE event_id=$event_id
);
""", cancellationToken,
            ("$published_utc", Format(publishedUtc)), ("$event_id", eventId.ToString("D"))).ConfigureAwait(false);
        if (changed == 0)
        {
            var published = await ScalarLongAsync(connection, transaction,
                "SELECT published FROM journal_outbox WHERE event_id=$event_id;", cancellationToken,
                ("$event_id", eventId.ToString("D"))).ConfigureAwait(false);
            if (published != 1) throw new InvalidOperationException("The DatabaseBackup service event is not present in the outbox.");
        }
        transaction.Commit();
    }

    public async IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT intent_type, intent_json, phase, last_service_sequence, fencing_token
FROM journal_operation
WHERE terminal=0
ORDER BY admitted_utc, operation_id;
""";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var @event = DatabaseBackupContractSerializer.Deserialize(reader.GetString(0), reader.GetString(1));
            yield return new RecoverableJournalOperation(
                new DatabaseExecutionIntent { ExecutionEvent = @event },
                (DatabaseRecoveryPhase)reader.GetInt32(2),
                reader.GetInt64(3),
                reader.GetInt64(4));
        }
    }

    public async ValueTask MarkCoreAcknowledgedAsync(
        DatabaseRecoveryOperationId operationId,
        long domainRevision,
        CancellationToken cancellationToken)
    {
        if (domainRevision <= 0) throw new ArgumentOutOfRangeException(nameof(domainRevision));
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, """
INSERT INTO journal_reconciliation (operation_id, core_domain_revision, acknowledged_utc)
VALUES ($operation_id,$revision,$acknowledged_utc)
ON CONFLICT (operation_id) DO UPDATE SET
    core_domain_revision=MAX(journal_reconciliation.core_domain_revision, excluded.core_domain_revision),
    acknowledged_utc=excluded.acknowledged_utc;
""", cancellationToken,
            ("$operation_id", operationId.Value.ToString("D")), ("$revision", domainRevision),
            ("$acknowledged_utc", Format(DateTimeOffset.UtcNow))).ConfigureAwait(false);
    }

    async ValueTask InsertOutboxAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DatabaseBackupServiceEventContract @event,
        CancellationToken cancellationToken)
    {
        var serialized = DatabaseBackupContractSerializer.Serialize(@event);
        var sequence = @event.Source.SourceRevisionOrSequence;
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(@event), "A positive service sequence is required.");
        var existingHash = await ScalarStringAsync(connection, transaction, """
SELECT content_hash FROM journal_outbox
WHERE event_id=$event_id OR (operation_id=$operation_id AND service_sequence=$sequence);
""", cancellationToken,
            ("$event_id", @event.Id.ToString("D")),
            ("$operation_id", @event.Source.OperationId.Value.ToString("D")),
            ("$sequence", sequence)).ConfigureAwait(false);
        if (existingHash is not null)
        {
            if (!StringComparer.Ordinal.Equals(existingHash, serialized.Hash))
                throw new DatabaseExecutionConflictException("A service-event outbox identity was replayed with conflicting content.");
            return;
        }

        await ExecuteAsync(connection, transaction, """
INSERT INTO journal_outbox
    (event_id, operation_id, service_sequence, event_type, event_json, content_hash, created_utc)
VALUES ($event_id,$operation_id,$sequence,$event_type,$event_json,$content_hash,$created_utc);
UPDATE journal_operation
SET last_service_sequence=MAX(last_service_sequence,$sequence), updated_utc=$created_utc
WHERE operation_id=$operation_id;
""", cancellationToken,
            ("$event_id", @event.Id.ToString("D")),
            ("$operation_id", @event.Source.OperationId.Value.ToString("D")),
            ("$sequence", sequence), ("$event_type", serialized.TypeName), ("$event_json", serialized.Payload),
            ("$content_hash", serialized.Hash), ("$created_utc", Format(DateTimeOffset.UtcNow))).ConfigureAwait(false);
        if (@event.Statistics is not null)
            await ExecuteAsync(connection, transaction, """
INSERT INTO journal_run_stats
    (operation_id, statistics_revision, statistics_json)
VALUES ($operation_id,$sequence,$statistics_json);
""", cancellationToken,
                ("$operation_id", @event.Source.OperationId.Value.ToString("D")),
                ("$sequence", sequence),
                ("$statistics_json", JsonSerializer.Serialize(@event.Statistics))).ConfigureAwait(false);
    }

    async ValueTask<LeaseState?> ReadLeaseStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DatabaseRecoveryOperationId operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT terminal, lease_host_id, lease_expires_utc, fencing_token
FROM journal_operation WHERE operation_id=$operation_id;
""";
        command.Parameters.AddWithValue("$operation_id", operationId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new LeaseState(
            reader.GetInt32(0) != 0,
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.GetInt64(3));
    }

    async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    static async ValueTask<int> ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    static async ValueTask<string?> ScalarStringAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    static async ValueTask<long?> ScalarLongAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("O");

    static void InitializeNativeSqlite()
    {
        if (Interlocked.Exchange(ref _sqliteInitialized, 1) == 0)
            SQLitePCL.Batteries_V2.Init();
    }

    readonly record struct LeaseState(bool Terminal, string? HostId, DateTimeOffset ExpiresUtc, long FencingToken);
}
