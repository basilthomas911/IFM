using System.Data;
using System.Diagnostics;
using Npgsql;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

public sealed class SequentialEventLogAppender : IEventLogAppender
{
    const string InsertReturningVersion = EventSourceDbSql.InsertEventLog;
    const string InsertExpectedReturningEventVersion = EventSourceDbSql.InsertEventLogExpectedVersion;
    readonly EventLogPersistenceOptions _options;
    readonly EventLogMessagePackCodec _codec;
    readonly string _connectionString;
    readonly string _insertSql;
    readonly string _expectedInsertSql;
    readonly string _projectionSql;

    public SequentialEventLogAppender(string connectionString, bool useLz4Compression, EventLogPersistenceOptions? options = null)
        : this(connectionString, useLz4Compression, CreateProductionConfiguration(options, useLz4Compression)) { }

    internal SequentialEventLogAppender(string connectionString, bool useLz4Compression,
        EventLogPersistenceOptions? options, EventLogSqlLayout layout)
        : this(connectionString, useLz4Compression,
            new AppenderConfiguration((options ?? new EventLogPersistenceOptions { UseLz4Compression = useLz4Compression }).Validate(), layout))
    { }

    SequentialEventLogAppender(string connectionString, bool useLz4Compression, AppenderConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var layout = configuration.Layout;
        layout.ValidateConnection(connectionString);
        _insertSql = layout.Resolve(InsertReturningVersion);
        _expectedInsertSql = layout.Resolve(InsertExpectedReturningEventVersion);
        _projectionSql = layout.Resolve(EventSourceDbSql.TryCreateEventProjectorExecutionState);
        _options = configuration.Options;
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

    static AppenderConfiguration CreateProductionConfiguration(EventLogPersistenceOptions? options, bool useLz4Compression)
    {
        var validated = (options ?? new EventLogPersistenceOptions { UseLz4Compression = useLz4Compression }).Validate();
        return new(validated, EventLogSqlLayout.ForProduction(validated));
    }

    sealed record AppenderConfiguration(EventLogPersistenceOptions Options, EventLogSqlLayout Layout);

    public EventLogWriteMode WriteMode => EventLogWriteMode.Sequential;
    public bool UseLz4Compression { get; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
    public async ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var tags = EventLogPersistenceMetrics.Tags(WriteMode, UseLz4Compression);
        var prepared = EventLogAppenderSupport.Prepare(request, _codec, _options);
        if (prepared.Events.Count == 1 && prepared.Events[0].RequiredProjection is null)
            return await AppendSingleAutocommitAsync(prepared, tags, started, cancellationToken)
            .ConfigureAwait(false);

        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        var assignments = new EventLogAssignment[prepared.Events.Count];
        try
        {
            for (var index = 0; index < prepared.Events.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = prepared.Events[index];
                var expected = prepared.ExpectedStreamVersion.HasValue ? prepared.ExpectedStreamVersion.Value + index : (long?)null;
                assignments[index] = await InsertAsync(connection, transaction, prepared, entry, expected, cancellationToken)
            .ConfigureAwait(false);
                EventInitHelper.SetProperty(entry.DomainEvent, nameof(IEvent.EventId), assignments[index].EventVersion);
                if (entry.RequiredProjection is not null)
                    await EventLogAppenderSupport.InsertProjectionMarkerAsync(connection, transaction, assignments[index].EventVersion, entry.RequiredProjection, cancellationToken, _projectionSql)
            .ConfigureAwait(false);
            }
            try
            {
                await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
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
            try { await transaction.RollbackAsync(CancellationToken.None)
            .ConfigureAwait(false); } catch { /* Preserve the original database failure; cleanup is best effort. */ }
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
            await connection.OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        }
        catch
        {
            EventLogPersistenceMetrics.Failed(tags, started);
            throw;
        }

        await using (connection.ConfigureAwait(false))
        {
            try
            {
                var assignment = await InsertAsync(
                    connection, null, prepared, prepared.Events[0], prepared.ExpectedStreamVersion, cancellationToken)
            .ConfigureAwait(false);
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

    async Task<EventLogAssignment> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        PreparedEventLogRequest request,
        PreparedEventLogEntry entry,
        long? expectedStreamVersion,
        CancellationToken cancellationToken)
    {
        if (expectedStreamVersion.HasValue)
        {
            var expectedCommand = EventLogAppenderSupport.Command(
                connection, transaction, _expectedInsertSql);
            await using var expectedCommandDisposal = expectedCommand.ConfigureAwait(false);
            var parameters = new InsertEventLogExpectedVersion(request.EventStreamId, entry.EventNameId, entry.Payload,
                request.CommandId, $"{request.EventTimestampUtc:o}", expectedStreamVersion.Value, entry.MessageOriginJson);
            PostgresValueBinding.AddParameters(expectedCommand, (object?[])parameters.Bind());
            var scalar = await expectedCommand.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
            if (scalar is not long eventVersion)
                throw new ConcurrencyException(
                    $"Event stream {request.EventStream} is no longer at expected version {expectedStreamVersion}.");
            return new EventLogAssignment(eventVersion, expectedStreamVersion.Value + 1);
        }

        var command = EventLogAppenderSupport.Command(
            connection,
            transaction,
            _insertSql);
        await using var commandDisposal = command.ConfigureAwait(false);
        var insertParameters = new InsertEventLog(request.EventStreamId, entry.EventNameId, entry.Payload,
            request.CommandId, $"{request.EventTimestampUtc:o}", entry.MessageOriginJson);
        PostgresValueBinding.AddParameters(command, (object?[])insertParameters.Bind());
        var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
            .ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken)
            .ConfigureAwait(false))
            throw new InvalidOperationException($"Event stream {request.EventStream} was not found.");
        return new EventLogAssignment(reader.GetInt64(0), reader.GetInt64(1));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
