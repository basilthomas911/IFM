using TomasAI.IFM.Framework.Storage.Postgres;

using TomasAI.IFM.Application.Storage.EventSourceDb.Diagnostics;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using Npgsql;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Exceptions;



namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>SQL/event operations explicitly enlisted in a single request-owned transaction.</summary>
/// <remarks>Only storage implementations use this surface; actors never execute SQL.</remarks>
public sealed class EnlistedEventTransaction : IEnlistedPostgresTransaction
{
    readonly NpgsqlConnection connection;
    readonly NpgsqlTransaction transaction;
    readonly EventLogMessagePackCodec eventLogCodec;
    readonly EventLogSqlLayout eventLogSqlLayout;
    readonly PostgresQueryTimeoutScope timeouts;
    internal (NpgsqlConnection Connection, NpgsqlTransaction Transaction) GetConnectionAndTransaction()
    {
        RequireActive();
        return (connection, transaction);
    }

    int completed;
    readonly TimeProvider businessClock;
    /// <summary>Business time shared with the owner; database execution deadlines remain physical.</summary>
    public TimeProvider BusinessTime => businessClock;
    internal EnlistedEventTransaction(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EventLogMessagePackCodec eventLogCodec,
        EventLogSqlLayout eventLogSqlLayout, PostgresQueryTimeout? defaultTimeout = null, TimeProvider? timeProvider = null)
    {
        (this.connection, this.transaction, this.eventLogCodec, this.eventLogSqlLayout) =
            (connection, transaction, eventLogCodec, eventLogSqlLayout);
        timeouts = new(connection, transaction, defaultTimeout);
        businessClock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Executes parameterized SQL on the enlisted connection.</summary>
    public async Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        RequireActive();
        await ConfigureTimeoutAsync(timeout, cancellationToken)
            .ConfigureAwait(false);
        var command = CreateCommand(sql, parameters);
        await using var commandDisposal = command.ConfigureAwait(false);
        return await command.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sends ordered SQL statements in one round trip on the enlisted transaction.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "Batch SQL comes from application storage contracts and values are bound separately.")]
    public async Task ExecuteBatchAsync(IReadOnlyList<(string Sql, object?[] Parameters)> statements, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statements);
        RequireActive();
        await ConfigureTimeoutAsync(null, cancellationToken)
            .ConfigureAwait(false);
        var batch = new NpgsqlBatch(connection, transaction) { Timeout = 0 };
        await using var disposal = batch.ConfigureAwait(false);
        foreach (var (sql, parameters) in statements)
        {
            var command = new NpgsqlBatchCommand(eventLogSqlLayout.Resolve(sql));
            PostgresValueBinding.AddParameters(command, parameters);
            batch.BatchCommands.Add(command);
        }
        await batch.ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reads a scalar on the same transaction; null remains explicit.</summary>
    public async Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        RequireActive();
        await ConfigureTimeoutAsync(timeout, cancellationToken)
            .ConfigureAwait(false);
        var command = CreateCommand(sql, parameters);
        await using var commandDisposal = command.ConfigureAwait(false);
        var value = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    /// <summary>Materializes a bounded storage query before disposing its reader.</summary>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
        Func<System.Data.Common.DbDataReader, T> map, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(parameters);
        RequireActive();
        await ConfigureTimeoutAsync(timeout, cancellationToken)
            .ConfigureAwait(false);
        var command = CreateCommand(sql, parameters);
        await using var commandDisposal = command.ConfigureAwait(false);
        var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var values = new List<T>();
        while (await reader.ReadAsync(cancellationToken)
            .ConfigureAwait(false)) values.Add(map(reader));
        return values;
    }

    /// <summary>Reads the highest committed event version visible in this transaction.</summary>
    public async Task<long> ReadCommittedEventCutAsync(CancellationToken cancellationToken)
        => (long)(await ScalarAsync(EventSourceDbSql.ReadCommittedEventCut, [], cancellationToken)
            .ConfigureAwait(false))!;

    /// <summary>Reads the committed head revision for the requested stream in this transaction.</summary>
    public async Task<long> ReadCommittedStreamRevisionAsync(string stream, CancellationToken cancellationToken)
        => (long)(await ScalarAsync(EventSourceDbSql.ReadCommittedStreamRevision, [stream], cancellationToken)
            .ConfigureAwait(false))!;

    /// <summary>Reads the latest committed event and its original payload for the requested stream.</summary>
    public async Task<EventStreamReadModel?> ReadLatestCommittedEventAsync(string stream, CancellationToken cancellationToken)
        => (await QueryAsync(EventSourceDbSql.ReadLatestCommittedEvent, [stream], MapToLatestCommittedEvent, cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();

    private static EventStreamReadModel MapToLatestCommittedEvent(System.Data.Common.DbDataReader row)
        => new() { EventTypeName = row.GetString(0), EventVersion = row.GetInt64(1), EventData = row.GetFieldValue<byte[]>(2) };

    /// <summary>Commits a full sidecar checkpoint at a known source revision without changing the source stream.</summary>
    public async Task<bool> AppendAggregateCheckpointAsync(string stream, string sidecar, long sourceRevision,
        IEvent snapshot, CancellationToken cancellationToken)
    {
        var head = await ScalarAsync(EventSourceDbSql.LockAggregateCheckpointSource, [stream], cancellationToken)
            .ConfigureAwait(false);
        if (head is not long committed || sourceRevision <= 0 || sourceRevision > committed)
            throw new InvalidOperationException("A checkpoint must identify a committed source revision.");
        if (await ScalarAsync(EventSourceDbSql.ReadAggregateCheckpointVersion, [stream, sourceRevision], cancellationToken)
            .ConfigureAwait(false) is not null)
            return false;
        var current = await ScalarAsync(EventSourceDbSql.ReadAggregateCheckpointHead, [sidecar], cancellationToken)
            .ConfigureAwait(false);
        var checkpoint = await AppendAsync(new EnlistedEventAppend(sidecar, snapshot.CommandId, snapshot, current is long version ? version : 0),
            cancellationToken)
            .ConfigureAwait(false);
        await ExecuteAsync(EventSourceDbSql.RegisterAggregateCheckpoint, [stream, sourceRevision, checkpoint], cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>Reserves a prepared audit envelope under the transaction and returns its technical receipt.</summary>
    public async Task<CommandAudit.CommandAuditReservationOutcome> ReserveCommandAuditAsync(
        CommandAudit.CommandAuditEnvelope envelope, CancellationToken cancellationToken = default)
    {
        RequireActive();
        await ConfigureTimeoutAsync(null, cancellationToken).ConfigureAwait(false);
        var results = await CommandAudit.CommandAuditPostgres.ReserveAsync(connection, transaction, [envelope], cancellationToken)
            .ConfigureAwait(false);
        var result = results[0];
        return result.PayloadConflict ? CommandAudit.CommandAuditReservationOutcome.PayloadConflict
            : result.Accepted ? CommandAudit.CommandAuditReservationOutcome.Accepted
            : CommandAudit.CommandAuditReservationOutcome.Existing;
    }

    /// <summary>Locks the existing command audit row and checks whether any event committed for that command.</summary>
    public async Task<bool> HasCommittedAuditEventsUnderLockAsync(Guid commandId, CancellationToken cancellationToken)
    {
        _ = await ScalarAsync(EventSourceDbSql.EnlistedEventTransaction_ReserveCommandAsync_1, [commandId], cancellationToken)
            .ConfigureAwait(false);
        return await ScalarAsync(EventSourceDbSql.EnlistedEventTransaction_ReserveCommandAsync_2, [commandId], cancellationToken)
            .ConfigureAwait(false) is true;
    }

    /// <summary>Checks for an event from this command on the requested stream.</summary>
    public async Task<bool> HasCommittedCommandEventsAsync(Guid commandId, string streamId, CancellationToken cancellationToken)
        => await ScalarAsync(EventSourceDbSql.EnlistedEventTransaction_ReserveCommandAsync_3, [commandId, streamId], cancellationToken)
            .ConfigureAwait(false) is true;

    /// <summary>Appends to the event stream using its optimistic version and standard serialization.</summary>
    public async Task<long> AppendAsync(EnlistedEventAppend request, CancellationToken cancellationToken)
    {
        RequireActive();
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.append");
        var (stream, commandId, domainEvent, expectedStreamVersion) =
            (request.Stream, request.CommandId, request.DomainEvent, request.ExpectedStreamVersion);
        var streamId = Convert.ToInt64(await ScalarAsync(EventSourceDbSql.EnlistedEventTransaction_AppendAsync_1, [stream], cancellationToken)
            .ConfigureAwait(false)
            ?? await ScalarAsync(EventSourceDbSql.InsertEventStreamId, [stream], cancellationToken)
            .ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
        var type = domainEvent.GetType();
        var identity = EventWriteIdentityRegistry.Default.GetIdentity(type);
        // The existing upsert takes an UPDATE lock even when the registry row is unchanged.
        // Reading pre-registered metadata avoids serializing unrelated Portfolio transactions on an event name.
        var nameId = (int)(await ScalarAsync(EventSourceDbSql.EnlistedEventTransaction_AppendAsync_2, [identity.EventName, identity.EventTypeName], cancellationToken)
            .ConfigureAwait(false)
            ?? await ScalarAsync(EventSourceDbSql.InsertEventNameId, [identity.EventName, identity.EventTypeName], cancellationToken)
            .ConfigureAwait(false))!;
        using var serializeTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.serialize");
        MessageOrigins.ForPublication(domainEvent, businessClock);
        var payloadBytes = eventLogCodec.Serialize(domainEvent);
        serializeTrace?.SetTag("event.payload.bytes", payloadBytes.Length);
        serializeTrace?.Stop();
        using var insertTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.insert");
        var parameters = new InsertEventLogExpectedVersion(streamId, nameId, payloadBytes, commandId,
            $"{businessClock.GetUtcNow().UtcDateTime:o}", expectedStreamVersion, MessageOriginJson.Serialize(MessageOrigins.Get(domainEvent)));
        var eventId = await ScalarAsync(EventSourceDbSql.InsertEventLogExpectedVersion, (object?[])parameters.Bind(), cancellationToken)
            .ConfigureAwait(false);
        insertTrace?.Stop();
        if (eventId is not long id) throw new ConcurrencyException($"Event stream {stream} is not at expected version {expectedStreamVersion}.");
        // EventId is transport/storage metadata. The immutable business event identity remains domainEvent.Id.
        EventInitHelper.SetProperty(domainEvent, nameof(IEvent.EventId), id);
        if (domainEvent is IRequireDurableProjection { RequiresDurableProjection: true } required)
        {
            var rule = required.RequiredProjection;
            if (rule.InitialStage is not (TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.PublishProcessingEvent
                or TomasAI.IFM.Shared.EventProjector.EventProjectorStageType.ApplyProjection))
                throw new ArgumentException("Invalid durable projection stage.", nameof(domainEvent));
            var now = DateTime.UtcNow;
            var marker = await ScalarAsync(EventSourceDbSql.TryCreateEventProjectorExecutionState, [id, rule.ActorName, rule.ProjectorName, false, 0, "Processing", rule.InitialStage.ToString(), string.Empty, $"{now:o}", $"{now:o}", now], cancellationToken)
            .ConfigureAwait(false);
            if (marker is null) throw new InvalidOperationException("Required durable projection marker was not persisted.");
        }
        return id;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    NpgsqlCommand CreateCommand(string sql, object?[] parameters)
    {
        var command = new NpgsqlCommand(eventLogSqlLayout.Resolve(sql), connection, transaction) { CommandTimeout = 0 };
        foreach (var parameter in parameters)
            PostgresValueBinding.AddParameter(command, parameter);
        return command;
    }

    internal Task ConfigureTimeoutAsync(PostgresQueryTimeout? timeout, CancellationToken token)
    {
        RequireActive();
        return timeouts.ApplyAsync(timeout, token);
    }

    internal void Complete() => Interlocked.Exchange(ref completed, 1);

    void RequireActive()
    {
        if (Volatile.Read(ref completed) != 0)
            throw new ObjectDisposedException(nameof(EnlistedEventTransaction), "The enlisted transaction scope has completed.");
    }
}
