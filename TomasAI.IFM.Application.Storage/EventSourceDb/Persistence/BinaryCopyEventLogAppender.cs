using System.Data;
using System.Diagnostics;
using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Application.Storage.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

public sealed class BinaryCopyEventLogAppender : IEventLogAppender
{
    const string CopySql = """
        COPY event_log
            (EventStreamId, EventNameId, EventVersion, StreamVersion, EventPayload, CommandId, EventTimestamp)
        FROM STDIN (FORMAT BINARY)
        """;
    readonly string _connectionString;
    readonly EventLogPersistenceOptions _options;
    readonly EventLogMessagePackCodec _codec;
    readonly Channel<PendingAppend> _queue;
    readonly CancellationTokenSource _lifetime = new();
    readonly Task _consumer;
    readonly System.Diagnostics.TagList _metricTags;
    NpgsqlConnection? _connection;
    int _disposed;

    public BinaryCopyEventLogAppender(string connectionString, bool useLz4Compression, EventLogPersistenceOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _options = (options ?? new EventLogPersistenceOptions
        {
            WriteMode = EventLogWriteMode.BinaryCopy,
            UseLz4Compression = useLz4Compression
        }).Validate();
        UseLz4Compression = useLz4Compression;
        _codec = new EventLogMessagePackCodec(useLz4Compression);
        _metricTags = EventLogPersistenceMetrics.Tags(WriteMode, UseLz4Compression);
        _queue = Channel.CreateBounded<PendingAppend>(new BoundedChannelOptions(_options.QueueCommandCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _consumer = ConsumeAsync(_lifetime.Token);
    }

    public EventLogWriteMode WriteMode => EventLogWriteMode.BinaryCopy;
    public bool UseLz4Compression { get; }

    public async ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var prepared = EventLogAppenderSupport.Prepare(request, _codec, _options);
        var pending = new PendingAppend(prepared);
        EventLogPersistenceMetrics.Queued(_metricTags);
        try
        {
            await _queue.Writer.WriteAsync(pending, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            EventLogPersistenceMetrics.Dequeued(_metricTags);
            throw;
        }
        // Once admitted, the append receives a durable terminal result even if caller cancellation arrives later.
        return await pending.Completion.Task.ConfigureAwait(false);
    }

    async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        PendingAppend? carry = null;
        try
        {
            while (carry is not null || await _queue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var first = carry ?? (_queue.Reader.TryRead(out var queued) ? queued : null);
                carry = null;
                if (first is null) continue;
                var batch = new List<PendingAppend> { first };
                var eventCount = first.Request.Events.Count;
                var byteCount = first.Request.PayloadBytes;
                using var flushDelay = new CancellationTokenSource();
                var delay = Task.Delay(_options.MaximumOldestRequestDelay, flushDelay.Token);

                while (eventCount < _options.MaximumEventsPerBatch && byteCount < _options.MaximumBatchBytes)
                {
                    if (_queue.Reader.TryRead(out var next))
                    {
                        var exceeds = batch.Count > 0 &&
                            (eventCount + next.Request.Events.Count > _options.MaximumEventsPerBatch ||
                             byteCount + next.Request.PayloadBytes > _options.MaximumBatchBytes);
                        if (exceeds)
                        {
                            carry = next;
                            break;
                        }
                        batch.Add(next);
                        eventCount += next.Request.Events.Count;
                        byteCount += next.Request.PayloadBytes;
                        continue;
                    }

                    var data = _queue.Reader.WaitToReadAsync(cancellationToken).AsTask();
                    var completed = await Task.WhenAny(data, delay).ConfigureAwait(false);
                    if (completed == delay) break;
                    if (!await data.ConfigureAwait(false)) break;
                }
                flushDelay.Cancel();
                await PersistBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var failure = new OperationCanceledException("Binary event-log writer stopped before persistence completed.", cancellationToken);
            if (carry is not null) carry.Completion.TrySetException(failure);
            while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(failure);
        }
        catch (Exception exception)
        {
            if (carry is not null) carry.Completion.TrySetException(exception);
            while (_queue.Reader.TryRead(out var pending)) pending.Completion.TrySetException(exception);
        }
    }

    async Task PersistBatchAsync(IReadOnlyList<PendingAppend> batch, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        foreach (var _ in batch) EventLogPersistenceMetrics.Dequeued(_metricTags);
        try
        {
            var connection = await GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
            try
            {
                var audited = batch.Where(static item => item.Request.CommandAudit is not null)
                    .Select(static item => item.Request.CommandAudit!).ToArray();
                if (audited.Length > 0)
                {
                    var reservations = await CommandAuditPostgres.ReserveAsync(
                        connection, transaction, audited, cancellationToken).ConfigureAwait(false);
                    for (var index = 0; index < reservations.Length; index++)
                    {
                        if (!reservations[index].Accepted)
                            throw new CommandAuditDuplicateException(audited[index].CommandId);
                    }
                }
                var current = await LockStreamsAsync(connection, transaction, batch, cancellationToken).ConfigureAwait(false);
                var planned = Plan(batch, current);
                await UpdateStreamVersionsAsync(connection, transaction, current, cancellationToken).ConfigureAwait(false);
                await ReserveEventIdsAsync(connection, transaction, planned, cancellationToken).ConfigureAwait(false);
                await CopyEventsAsync(connection, planned, cancellationToken).ConfigureAwait(false);
                foreach (var request in planned)
                {
                    for (var index = 0; index < request.Pending.Request.Events.Count; index++)
                    {
                        var projection = request.Pending.Request.Events[index].RequiredProjection;
                        if (projection is not null)
                            await EventLogAppenderSupport.InsertProjectionMarkerAsync(connection, transaction,
                                request.Assignments[index].EventVersion, projection, cancellationToken).ConfigureAwait(false);
                    }
                }
                try
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    throw new EventLogCommitOutcomeUnknownException(
                        $"PostgreSQL did not confirm an event-log batch commit containing {planned.Length} commands.", exception);
                }
                var committedAt = DateTime.UtcNow;
                foreach (var request in planned)
                    request.Pending.Completion.TrySetResult(new EventLogAppendResult(request.Assignments, committedAt));
                EventLogPersistenceMetrics.Committed(
                    _metricTags,
                    batch.Count,
                    batch.Sum(static item => item.Request.Events.Count),
                    batch.Sum(static item => (long)item.Request.PayloadBytes),
                    started);
            }
            catch
            {
                try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                throw;
            }
        }
        catch (Exception exception)
        {
            EventLogPersistenceMetrics.Failed(_metricTags, started);
            if (_connection is not { FullState: ConnectionState.Open })
            {
                if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
            foreach (var pending in batch) pending.Completion.TrySetException(exception);
        }
    }

    async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { FullState: ConnectionState.Open }) return _connection;
        if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
        _connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(_connectionString);
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return _connection;
    }

    static async Task<Dictionary<long, long>> LockStreamsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<PendingAppend> batch,
        CancellationToken cancellationToken)
    {
        var ids = batch.Select(static request => request.Request.EventStreamId).Distinct().Order().ToArray();
        await using var command = EventLogAppenderSupport.Command(connection, transaction, """
            SELECT EventStreamId, CurrentVersion
            FROM event_stream_id
            WHERE EventStreamId = ANY($1)
            ORDER BY EventStreamId
            FOR UPDATE;
            """);
        EventLogAppenderSupport.Add(command, ids, NpgsqlDbType.Array | NpgsqlDbType.Bigint);
        var current = new Dictionary<long, long>(ids.Length);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            current[Convert.ToInt64(reader.GetValue(0))] = reader.GetInt64(1);
        if (current.Count != ids.Length) throw new InvalidOperationException("An event stream disappeared before persistence.");
        return current;
    }

    static PlannedAppend[] Plan(IReadOnlyList<PendingAppend> batch, Dictionary<long, long> cursors)
    {
        var planned = new PlannedAppend[batch.Count];
        var ordered = batch
            .Select(static (pending, ordinal) => (Pending: pending, Ordinal: ordinal))
            .OrderBy(static item => item.Pending.Request.EventStreamId)
            .ThenBy(static item => item.Pending.Request.ExpectedStreamVersion ?? long.MaxValue)
            .ThenBy(static item => item.Ordinal)
            .ToArray();
        for (var requestIndex = 0; requestIndex < ordered.Length; requestIndex++)
        {
            var pending = ordered[requestIndex].Pending;
            var cursor = cursors[pending.Request.EventStreamId];
            if (pending.Request.ExpectedStreamVersion is long expected && expected != cursor)
                throw new ConcurrencyException($"Event stream {pending.Request.EventStream} is no longer at expected version {expected}.");
            var assignments = new EventLogAssignment[pending.Request.Events.Count];
            for (var eventIndex = 0; eventIndex < assignments.Length; eventIndex++)
                assignments[eventIndex] = new EventLogAssignment(0, ++cursor);
            cursors[pending.Request.EventStreamId] = cursor;
            planned[requestIndex] = new PlannedAppend(pending, assignments);
        }
        return planned;
    }

    static async Task UpdateStreamVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyDictionary<long, long> finalVersions,
        CancellationToken cancellationToken)
    {
        var ordered = finalVersions.OrderBy(static pair => pair.Key).ToArray();
        await using var command = EventLogAppenderSupport.Command(connection, transaction, """
            UPDATE event_stream_id AS stream
            SET CurrentVersion = updates.FinalVersion
            FROM unnest($1::bigint[], $2::bigint[]) AS updates(EventStreamId, FinalVersion)
            WHERE stream.EventStreamId = updates.EventStreamId;
            """);
        EventLogAppenderSupport.Add(command, ordered.Select(static pair => pair.Key).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Bigint);
        EventLogAppenderSupport.Add(command, ordered.Select(static pair => pair.Value).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Bigint);
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != ordered.Length)
            throw new InvalidOperationException("Not every event stream version was reserved.");
    }

    static async Task ReserveEventIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<PlannedAppend> planned,
        CancellationToken cancellationToken)
    {
        var count = planned.Sum(static request => request.Assignments.Length);
        await using var command = EventLogAppenderSupport.Command(connection, transaction,
            "SELECT nextval('public.event_log_eventversion_seq') FROM generate_series(1, $1);");
        EventLogAppenderSupport.Add(command, count, NpgsqlDbType.Integer);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        foreach (var request in planned)
        {
            for (var index = 0; index < request.Assignments.Length; index++)
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new InvalidOperationException("PostgreSQL did not reserve every requested event ID.");
                request.Assignments[index] = request.Assignments[index] with { EventVersion = reader.GetInt64(0) };
                EventInitHelper.SetProperty(request.Pending.Request.Events[index].DomainEvent, nameof(IEvent.EventId),
                    request.Assignments[index].EventVersion);
            }
        }
    }

    static async Task CopyEventsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<PlannedAppend> planned,
        CancellationToken cancellationToken)
    {
        await using var importer = await connection.BeginBinaryImportAsync(CopySql, cancellationToken).ConfigureAwait(false);
        foreach (var request in planned)
        {
            for (var index = 0; index < request.Pending.Request.Events.Count; index++)
            {
                var source = request.Pending.Request;
                var entry = source.Events[index];
                var assignment = request.Assignments[index];
                await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
                await importer.WriteAsync(source.EventStreamId, NpgsqlDbType.Bigint, cancellationToken).ConfigureAwait(false);
                await importer.WriteAsync(entry.EventNameId, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
                await importer.WriteAsync(assignment.EventVersion, NpgsqlDbType.Bigint, cancellationToken).ConfigureAwait(false);
                await importer.WriteAsync(assignment.StreamVersion, NpgsqlDbType.Bigint, cancellationToken).ConfigureAwait(false);
                await importer.WriteAsync(entry.Payload, NpgsqlDbType.Bytea, cancellationToken).ConfigureAwait(false);
                await importer.WriteAsync(source.CommandId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                await importer.WriteAsync($"{source.EventTimestampUtc:o}", NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            }
        }
        await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _queue.Writer.TryComplete();
        var timeout = Task.Delay(_options.ShutdownDrainTimeout);
        if (await Task.WhenAny(_consumer, timeout).ConfigureAwait(false) != _consumer) _lifetime.Cancel();
        try { await _consumer.ConfigureAwait(false); } catch (OperationCanceledException) { }
        if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    sealed class PendingAppend(PreparedEventLogRequest request)
    {
        internal PreparedEventLogRequest Request { get; } = request;
        internal TaskCompletionSource<EventLogAppendResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed record PlannedAppend(PendingAppend Pending, EventLogAssignment[] Assignments);
}
