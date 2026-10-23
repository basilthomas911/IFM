
using TomasAI.IFM.Application.Storage.EventSourceDb.Diagnostics;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
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
    int completed;
    internal EnlistedEventTransaction(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EventLogMessagePackCodec eventLogCodec,
        EventLogSqlLayout eventLogSqlLayout, PostgresQueryTimeout? defaultTimeout = null)
    {
        (this.connection, this.transaction, this.eventLogCodec, this.eventLogSqlLayout) =
            (connection, transaction, eventLogCodec, eventLogSqlLayout);
        timeouts = new(connection, transaction, defaultTimeout);
    }

    /// <summary>Executes parameterized SQL on the enlisted connection.</summary>
    public async Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        RequireActive();
        await ConfigureTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);
        var command = CreateCommand(sql, parameters);
        await using var commandDisposal = command.ConfigureAwait(false);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a scalar on the same transaction; null remains explicit.</summary>
    public async Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        RequireActive();
        await ConfigureTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);
        var command = CreateCommand(sql, parameters);
        await using var commandDisposal = command.ConfigureAwait(false);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    /// <summary>Materializes a bounded storage query before disposing its reader.</summary>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
        Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(parameters);
        RequireActive();
        await ConfigureTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);
        var command = CreateCommand(sql, parameters);
        await using var commandDisposal = command.ConfigureAwait(false);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var values = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) values.Add(map(reader));
        return values;
    }

    /// <summary>Appends to the existing event schema using its optimistic stream version and standard serialization.</summary>
    /// <summary>Reserves the original command audit on this transaction, preserving existing payload-conflict and duplicate checks.</summary>
    public async Task ReserveCommandAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        RequireActive();
        await ConfigureTimeoutAsync(null, cancellationToken).ConfigureAwait(false);
        var envelope=CommandAudit.CommandAuditEnvelope.Create(command,new CommandAudit.CommandAuditMessagePackCodec());
        var results=await CommandAudit.CommandAuditPostgres.ReserveAsync(connection,transaction,[envelope],cancellationToken).ConfigureAwait(false);
        if(results[0].PayloadConflict) throw new CommandAudit.CommandAuditPayloadConflictException(command.CommandId);
        if (!results[0].Accepted)
        {
            if (command is IRecoverableCommandAudit)
            {
                // ReserveAsync already verified the exact retry payload. Serialize orphan recovery
                // against another recovery attempt; committed events always remain duplicates.
                await ScalarAsync("SELECT commandid FROM command_log WHERE commandid=$1 FOR UPDATE;", [command.CommandId], cancellationToken).ConfigureAwait(false);
                var committed = await ScalarAsync("SELECT EXISTS(SELECT 1 FROM event_log WHERE commandid=$1);", [command.CommandId], cancellationToken).ConfigureAwait(false);
                if (committed is false) return;
            }
            // An audit reservation alone is not proof of execution: old writers may have
            // committed it separately. Only matching source facts can acknowledge a retry.
            var hasCommittedEvents = await ScalarAsync(
                "SELECT EXISTS(SELECT 1 FROM event_log l JOIN event_stream_id s ON l.eventstreamid=s.eventstreamid WHERE l.commandid=$1 AND s.eventstream=$2);",
                [command.CommandId, command.StreamId], cancellationToken).ConfigureAwait(false);
            throw new CommandAudit.CommandAuditDuplicateException(command.CommandId, hasCommittedEvents is true);
        }
    }

    public async Task<long> AppendAsync(string stream, Guid commandId, IEvent domainEvent,
        long expectedStreamVersion, CancellationToken cancellationToken)
    {
        RequireActive();
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.append");
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (commandId == Guid.Empty || expectedStreamVersion < 0) throw new ArgumentException("Invalid event identity/version.",nameof(commandId));
        var streamId = Convert.ToInt64(await ScalarAsync("SELECT eventstreamid FROM event_stream_id WHERE eventstream=$1;", [stream], cancellationToken).ConfigureAwait(false)
            ?? await ScalarAsync(EventSourceDbSql.InsertEventStreamId, [stream], cancellationToken).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
        var type = domainEvent.GetType();
        // The existing upsert takes an UPDATE lock even when the registry row is unchanged.
        // Reading pre-registered metadata avoids serializing unrelated Portfolio transactions on an event name.
        var nameId = (int)(await ScalarAsync("SELECT eventnameid FROM event_name_id WHERE eventname=$1 AND eventtypename=$2;",
            [type.Name, type.AssemblyQualifiedName!], cancellationToken).ConfigureAwait(false)
            ?? await ScalarAsync(EventSourceDbSql.InsertEventNameId, [type.Name, type.AssemblyQualifiedName!], cancellationToken).ConfigureAwait(false))!;
        using var serializeTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.serialize");
        var payloadBytes = eventLogCodec.Serialize(domainEvent);
        var payload = new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bytea, Value = payloadBytes };
        serializeTrace?.SetTag("event.payload.bytes", payloadBytes.Length);
        serializeTrace?.Stop();
        using var insertTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.event.insert");
        var eventId = await ScalarAsync(EventSourceDbSql.InsertEventLogExpectedVersion,
            [streamId, nameId, payload, commandId, DateTime.UtcNow, expectedStreamVersion], cancellationToken).ConfigureAwait(false);
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
            var marker = await ScalarAsync(EventSourceDbSql.TryCreateEventProjectorExecutionState,
                [id, rule.ActorName, rule.ProjectorName, false, 0, "Processing", rule.InitialStage.ToString(),
                    string.Empty, $"{now:o}", $"{now:o}", now], cancellationToken).ConfigureAwait(false);
            if (marker is null) throw new InvalidOperationException("Required durable projection marker was not persisted.");
        }
        return id;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    NpgsqlCommand CreateCommand(string sql, object?[] parameters)
    {
        var command = new NpgsqlCommand(eventLogSqlLayout.Resolve(sql), connection, transaction) { CommandTimeout = 0 };
        foreach (var parameter in parameters)
            command.Parameters.Add(parameter is NpgsqlParameter typed ? typed : new NpgsqlParameter { Value = parameter ?? DBNull.Value });
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
