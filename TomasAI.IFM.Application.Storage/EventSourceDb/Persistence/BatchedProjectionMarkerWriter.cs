using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

internal readonly record struct ProjectionMarker(long EventId, DurableProjectionRequirement Requirement);

/// <summary>Experimental set-based equivalent of TryCreateEventProjectorExecutionState.
/// Only enabled through the guarded benchmark layout; caller owns commit/rollback.</summary>
internal static class BatchedProjectionMarkerWriter
{
    internal const string Sql = """
        WITH requested AS (
            SELECT * FROM unnest($1::bigint[], $2::text[], $3::text[], $4::text[])
                AS input(EventId, ActorName, ProjectorName, InitialStage)
        ), inserted AS (
            INSERT INTO event_projector_state (
                EventId, ActorName, ProjectorName, IsReplay, AttemptNumber,
                Outcome, Stage, ErrorMessage, CreatedTimestamp, UpdatedTimestamp,
                EventStreamId, SourceEventName, UpdatedAtUtc, StreamVersion
            )
            SELECT input.EventId, input.ActorName, input.ProjectorName, false, 0,
                CASE
                    WHEN checkpoint.LastAppliedStreamVersion IS NULL
                      OR checkpoint.LastAppliedStreamVersion < el.StreamVersion THEN 'Processing'
                    WHEN checkpoint.LastAppliedStreamVersion = el.StreamVersion THEN 'AlreadyCompleted'
                    ELSE 'Superseded'
                END,
                CASE WHEN checkpoint.LastAppliedStreamVersion >= el.StreamVersion
                    THEN 'Completed' ELSE input.InitialStage END,
                CASE WHEN checkpoint.LastAppliedStreamVersion >= el.StreamVersion
                    THEN 'stream-checkpoint-covered' ELSE '' END,
                $5, $5, el.EventStreamId, en.EventName, $6, el.StreamVersion
            FROM requested input
            JOIN event_log el ON el.EventVersion = input.EventId
            JOIN event_name_id en ON en.EventNameId = el.EventNameId
            LEFT JOIN event_projector_stream_checkpoint checkpoint
                ON checkpoint.ProjectorName = input.ProjectorName
               AND checkpoint.EventStreamId = el.EventStreamId
            ON CONFLICT (EventId, ProjectorName) DO NOTHING
            RETURNING EventId
        )
        SELECT count(*) FROM inserted;
        """;

    internal static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        IReadOnlyList<ProjectionMarker> markers, string sql, CancellationToken cancellationToken)
    {
        if (markers.Count == 0) return;
        var started = Stopwatch.GetTimestamp();
        var ids = new long[markers.Count];
        var actors = new string[markers.Count];
        var projectors = new string[markers.Count];
        var stages = new string[markers.Count];
        for (var i = 0; i < markers.Count; i++)
        {
            ids[i] = markers[i].EventId;
            actors[i] = markers[i].Requirement.ActorName;
            projectors[i] = markers[i].Requirement.ProjectorName;
            stages[i] = markers[i].Requirement.InitialStage.ToString();
        }
        var now = DateTime.UtcNow;
        await using var command = EventLogAppenderSupport.Command(connection, transaction, sql);
        EventLogAppenderSupport.Add(command, ids, NpgsqlDbType.Array | NpgsqlDbType.Bigint);
        EventLogAppenderSupport.Add(command, actors, NpgsqlDbType.Array | NpgsqlDbType.Text);
        EventLogAppenderSupport.Add(command, projectors, NpgsqlDbType.Array | NpgsqlDbType.Text);
        EventLogAppenderSupport.Add(command, stages, NpgsqlDbType.Array | NpgsqlDbType.Text);
        EventLogAppenderSupport.Add(command, $"{now:o}", NpgsqlDbType.Text);
        EventLogAppenderSupport.Add(command, now, NpgsqlDbType.TimestampTz);
        var inserted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (inserted is not long count || count != markers.Count)
            throw new InvalidOperationException("Not every required durable projection marker was persisted.");
        EventLogPersistenceMetrics.ProjectionMarkersWritten(markers.Count, started);
    }
}

