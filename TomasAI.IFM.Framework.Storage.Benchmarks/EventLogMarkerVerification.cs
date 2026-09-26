using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>Untimed semantic/rollback checks run against every baseline and candidate fixture.</summary>
public static class EventLogMarkerVerification
{
    internal static async Task VerifyAsync(NpgsqlConnection connection, IEventLogAppender appender,
        EventLogSqlLayout layout, string table, int eventNameId)
    {
        const string streamName = "Benchmark.MarkerSemantics";
        var streamId = Convert.ToInt64(await Scalar(connection,
            "INSERT INTO event_stream_id(eventstream) VALUES($1) RETURNING eventstreamid", streamName));
        await Scalar(connection, """
            INSERT INTO event_projector_stream_checkpoint
                (projectorname,eventstreamid,lastappliedstreamversion,lastappliedeventid)
            VALUES ('Behind',$1,0,0),('Equal',$1,2,0),('Ahead',$1,4,0)
            """, streamId);
        var commandId = Guid.NewGuid();
        var names = new[] { "Behind", "Equal", "Ahead", "Missing", "NoMarker", "Behind" };
        var entries = names.Select((name, index) => new EventLogAppendEntry(eventNameId,
            new EventLogV2Benchmark.BenchmarkEvent
            {
                CommandId = commandId, AggregateId = streamName, Value = index + 1,
                RequiresDurableProjection = name != "NoMarker", ProjectionName = name,
                InitialStage = index == 3 ? EventProjectorStageType.PublishProcessingEvent : EventProjectorStageType.ApplyProjection
            })).ToArray();
        var envelope = CommandAuditEnvelope.Create(new EventLogV2Benchmark.BenchmarkCommand
            { CommandId = commandId, StreamId = streamName, Value = 6 }, new CommandAuditMessagePackCodec());
        var request = new EventLogAppendRequest(streamName, streamId, commandId, entries, 0, DateTime.UtcNow,
            appender.WriteMode == EventLogWriteMode.BinaryCopy ? envelope : null);
        var result = await appender.AppendAsync(request);
        await using (var read = new NpgsqlCommand("""
            SELECT streamversion,projectorname,outcome,stage,errormessage,attemptnumber,isreplay,
                revision,retrycount,sourceeventname,createdtimestamp=updatedtimestamp,actorname
            FROM event_projector_state WHERE eventstreamid=$1 ORDER BY streamversion
            """, connection))
        {
            read.Parameters.AddWithValue(streamId);
            await using var reader = await read.ExecuteReaderAsync();
            var expected = new[]
            {
                (1L, "Behind", "Processing", "ApplyProjection", ""),
                (2L, "Equal", "AlreadyCompleted", "Completed", "stream-checkpoint-covered"),
                (3L, "Ahead", "Superseded", "Completed", "stream-checkpoint-covered"),
                (4L, "Missing", "Processing", "PublishProcessingEvent", ""),
                (6L, "Behind", "Processing", "ApplyProjection", "")
            };
            foreach (var row in expected)
            {
                if (!await reader.ReadAsync() || reader.GetInt64(0) != row.Item1 || reader.GetString(1) != row.Item2 ||
                    reader.GetString(2) != row.Item3 || reader.GetString(3) != row.Item4 || reader.GetString(4) != row.Item5 ||
                    reader.GetInt32(5) != 0 || reader.GetBoolean(6) || reader.GetInt64(7) != 0 ||
                    reader.GetInt32(8) != 0 || reader.GetString(9) != nameof(EventLogV2Benchmark.BenchmarkEvent) ||
                    !reader.GetBoolean(10) || reader.GetString(11) != "BenchmarkActor")
                    throw new InvalidOperationException("Marker checkpoint/stage/default field semantics changed.");
            }
            if (await reader.ReadAsync()) throw new InvalidOperationException("Nonmaterial event acquired a durable marker.");
        }

        // A missing event-name join must roll back the whole append, including audit and stream counters.
        var before = await Snapshot(connection, table);
        var missingId = Guid.NewGuid();
        var invalid = request with
        {
            CommandId = missingId, ExpectedStreamVersion = 6,
            Events = [
                new EventLogAppendEntry(eventNameId, ((EventLogV2Benchmark.BenchmarkEvent)entries[0].DomainEvent)
                    with { EventId = 0, CommandId = missingId, Value = 7 }),
                new EventLogAppendEntry(int.MaxValue, ((EventLogV2Benchmark.BenchmarkEvent)entries[1].DomainEvent)
                    with { EventId = 0, CommandId = missingId, Value = 8 })],
            CommandAudit = request.CommandAudit is null ? null : CommandAuditEnvelope.Create(
                new EventLogV2Benchmark.BenchmarkCommand { CommandId = missingId, StreamId = streamName, Value = 7 },
                new CommandAuditMessagePackCodec())
        };
        await MustReject(() => appender.AppendAsync(invalid).AsTask());
        if (before != await Snapshot(connection, table))
            throw new InvalidOperationException("Incomplete marker join left a partially committed append.");

        // Inserting one new marker then finding an existing marker must still roll back the entire set.
        var requirement = new DurableProjectionRequirement("BenchmarkActor", "Behind", EventProjectorStageType.ApplyProjection);
        var newMarker = new ProjectionMarker(result.Assignments[4].EventVersion, requirement);
        var existing = new ProjectionMarker(result.Assignments[0].EventVersion, requirement);
        await RollbackRejectedSet(connection, layout, table, [newMarker, existing]);
        // Also reject a missing event and duplicate inputs instead of silently accepting a partial set.
        await RollbackRejectedSet(connection, layout, table, [newMarker, new ProjectionMarker(long.MaxValue, requirement)]);
        await RollbackRejectedSet(connection, layout, table, [newMarker, newMarker]);
        await using var emptyTransaction = await connection.BeginTransactionAsync();
        await Insert(connection, emptyTransaction, layout, []);
        await emptyTransaction.RollbackAsync();
        if (before != await Snapshot(connection, table))
            throw new InvalidOperationException("Marker failure/empty-set checks changed durable state.");
    }

    static async Task RollbackRejectedSet(NpgsqlConnection connection, EventLogSqlLayout layout,
        string table, ProjectionMarker[] markers)
    {
        var before = await Snapshot(connection, table);
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await MustReject(() => Insert(connection, transaction, layout, markers));
            await transaction.RollbackAsync();
        }
        if (before != await Snapshot(connection, table))
            throw new InvalidOperationException("Rejected marker set left durable rows.");
    }

    static async Task Insert(NpgsqlConnection connection, NpgsqlTransaction transaction,
        EventLogSqlLayout layout, ProjectionMarker[] markers)
    {
        if (layout.BatchProjectionMarkers)
            await BatchedProjectionMarkerWriter.InsertAsync(connection, transaction, markers,
                layout.Resolve(BatchedProjectionMarkerWriter.Sql), CancellationToken.None);
        else
            foreach (var marker in markers)
                await EventLogAppenderSupport.InsertProjectionMarkerAsync(connection, transaction, marker.EventId,
                    marker.Requirement, CancellationToken.None, layout.Resolve(EventSourceDbSql.TryCreateEventProjectorExecutionState));
    }

    static async Task MustReject(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Expected marker rejection was not observed.");
    }

    static async Task<string> Snapshot(NpgsqlConnection connection, string table) =>
        (string)(await Scalar(connection, $"""
            SELECT (SELECT count(*) FROM {table})::text || '/' ||
                (SELECT count(*) FROM command_log)::text || '/' ||
                (SELECT count(*) FROM event_projector_state)::text || '/' ||
                (SELECT sum(currentversion) FROM event_stream_id)::text
            """))!;

    static async Task<object?> Scalar(NpgsqlConnection connection, string sql, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
        return await command.ExecuteScalarAsync();
    }
}
