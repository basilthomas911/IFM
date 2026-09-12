using System.Data;
using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

public sealed class SequentialEventLogAppender : IEventLogAppender
{
    const string InsertReturningVersion = """
        WITH next_stream_version AS (
            UPDATE event_stream_id
            SET CurrentVersion = CurrentVersion + 1
            WHERE EventStreamId = $1
            RETURNING CurrentVersion AS StreamVersion
        )
        INSERT INTO event_log (
            EventStreamId, EventNameId, StreamVersion, EventPayload, CommandId, EventTimestamp)
        SELECT $1, $2, next_stream_version.StreamVersion, $3, $4, $5
        FROM next_stream_version
        RETURNING EventVersion, StreamVersion;
        """;
    const string InsertExpectedReturningEventVersion = """
        WITH next_stream_version AS (
            UPDATE event_stream_id
            SET CurrentVersion = CurrentVersion + 1
            WHERE EventStreamId = $1 AND CurrentVersion = $6
            RETURNING CurrentVersion AS StreamVersion
        )
        INSERT INTO event_log (
            EventStreamId, EventNameId, StreamVersion, EventPayload, CommandId, EventTimestamp)
        SELECT $1, $2, next_stream_version.StreamVersion, $3, $4, $5
        FROM next_stream_version
        RETURNING EventVersion;
        """;
    readonly EventLogPersistenceOptions _options;
    readonly EventLogMessagePackCodec _codec;
    readonly string _connectionString;

    public SequentialEventLogAppender(string connectionString, bool useLz4Compression, EventLogPersistenceOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _options = (options ?? new EventLogPersistenceOptions { UseLz4Compression = useLz4Compression }).Validate();
        UseLz4Compression = useLz4Compression;
        _codec = new EventLogMessagePackCodec(useLz4Compression);
        var connectionOptions = new NpgsqlConnectionStringBuilder(connectionString);
        if (connectionOptions.MaxAutoPrepare == 0)
        {
            connectionOptions.MaxAutoPrepare = 16;
            connectionOptions.AutoPrepareMinUsages = 2;
        }
        _connectionString = connectionOptions.ConnectionString;
    }

    public EventLogWriteMode WriteMode => EventLogWriteMode.Sequential;
    public bool UseLz4Compression { get; }

    public async ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var tags = EventLogPersistenceMetrics.Tags(WriteMode, UseLz4Compression);
        var prepared = EventLogAppenderSupport.Prepare(request, _codec, _options);
        if (prepared.Events.Count == 1 && prepared.Events[0].RequiredProjection is null)
            return await AppendSingleAutocommitAsync(prepared, tags, started, cancellationToken).ConfigureAwait(false);

        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        var assignments = new EventLogAssignment[prepared.Events.Count];
        try
        {
            for (var index = 0; index < prepared.Events.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = prepared.Events[index];
                var expected = prepared.ExpectedStreamVersion.HasValue ? prepared.ExpectedStreamVersion.Value + index : (long?)null;
                assignments[index] = await InsertAsync(connection, transaction, prepared, entry, expected, cancellationToken).ConfigureAwait(false);
                EventInitHelper.SetProperty(entry.DomainEvent, nameof(IEvent.EventId), assignments[index].EventVersion);
                if (entry.RequiredProjection is not null)
                    await EventLogAppenderSupport.InsertProjectionMarkerAsync(connection, transaction, assignments[index].EventVersion, entry.RequiredProjection, cancellationToken).ConfigureAwait(false);
            }
            try
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new EventLogCommitOutcomeUnknownException(
                    $"PostgreSQL did not confirm the event-log commit for command {prepared.CommandId}.", exception);
            }
            EventLogPersistenceMetrics.Committed(tags, 1, prepared.Events.Count, prepared.PayloadBytes, started);
            return new EventLogAppendResult(assignments, DateTime.UtcNow);
        }
        catch
        {
            EventLogPersistenceMetrics.Failed(tags, started);
            try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            throw;
        }
    }

    async ValueTask<EventLogAppendResult> AppendSingleAutocommitAsync(
        PreparedEventLogRequest prepared,
        System.Diagnostics.TagList tags,
        long started,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection;
        try
        {
            connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            EventLogPersistenceMetrics.Failed(tags, started);
            throw;
        }

        await using (connection)
        {
            try
            {
                var assignment = await InsertAsync(
                    connection, null, prepared, prepared.Events[0], prepared.ExpectedStreamVersion, cancellationToken).ConfigureAwait(false);
                EventInitHelper.SetProperty(prepared.Events[0].DomainEvent, nameof(IEvent.EventId), assignment.EventVersion);
                EventLogPersistenceMetrics.Committed(tags, 1, 1, prepared.PayloadBytes, started);
                return new EventLogAppendResult([assignment], DateTime.UtcNow);
            }
            catch (NpgsqlException exception) when (exception is not PostgresException)
            {
                EventLogPersistenceMetrics.Failed(tags, started);
                throw new EventLogCommitOutcomeUnknownException(
                    $"PostgreSQL did not confirm the event-log statement for command {prepared.CommandId}.", exception);
            }
            catch
            {
                EventLogPersistenceMetrics.Failed(tags, started);
                throw;
            }
        }
    }

    static async Task<EventLogAssignment> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        PreparedEventLogRequest request,
        PreparedEventLogEntry entry,
        long? expectedStreamVersion,
        CancellationToken cancellationToken)
    {
        if (expectedStreamVersion.HasValue)
        {
            await using var expectedCommand = EventLogAppenderSupport.Command(
                connection, transaction, InsertExpectedReturningEventVersion);
            AddInsertParameters(expectedCommand, request, entry);
            EventLogAppenderSupport.Add(expectedCommand, expectedStreamVersion.Value, NpgsqlDbType.Bigint);
            var scalar = await expectedCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is not long eventVersion)
                throw new ConcurrencyException(
                    $"Event stream {request.EventStream} is no longer at expected version {expectedStreamVersion}.");
            return new EventLogAssignment(eventVersion, expectedStreamVersion.Value + 1);
        }

        await using var command = EventLogAppenderSupport.Command(
            connection,
            transaction,
            InsertReturningVersion);
        AddInsertParameters(command, request, entry);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"Event stream {request.EventStream} was not found.");
        return new EventLogAssignment(reader.GetInt64(0), reader.GetInt64(1));
    }

    static void AddInsertParameters(
        NpgsqlCommand command,
        PreparedEventLogRequest request,
        PreparedEventLogEntry entry)
    {
        EventLogAppenderSupport.Add(command, request.EventStreamId, NpgsqlDbType.Bigint);
        EventLogAppenderSupport.Add(command, entry.EventNameId, NpgsqlDbType.Integer);
        EventLogAppenderSupport.Add(command, entry.Payload, NpgsqlDbType.Bytea);
        EventLogAppenderSupport.Add(command, request.CommandId, NpgsqlDbType.Uuid);
        EventLogAppenderSupport.Add(command, $"{request.EventTimestampUtc:o}", NpgsqlDbType.Text);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
