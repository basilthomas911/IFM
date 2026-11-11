using System.Diagnostics;
using Npgsql;
using TomasAI.IFM.Shared.Storage;


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

/// <summary>Experimental set-based equivalent of TryCreateEventProjectorExecutionState.
/// Only enabled through the guarded benchmark layout; caller owns commit/rollback.</summary>
internal static class BatchedProjectionMarkerWriter
{
    internal const string Sql = EventSourceDbSql.BatchedProjectionMarkerWriter_Statement_1;

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
        var command = EventLogAppenderSupport.Command(connection, transaction, sql);
        await using var commandDisposal = command.ConfigureAwait(false);
        EventLogAppenderSupport.Add(command, ids);
        EventLogAppenderSupport.Add(command, actors);
        EventLogAppenderSupport.Add(command, projectors);
        EventLogAppenderSupport.Add(command, stages);
        EventLogAppenderSupport.Add(command, BindValues.Text($"{now:o}"));
        EventLogAppenderSupport.Add(command, BindValues.TimestampTz(now));
        var inserted = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
        if (inserted is not long count || count != markers.Count)
            throw new InvalidOperationException("Not every required durable projection marker was persisted.");
        EventLogPersistenceMetrics.ProjectionMarkersWritten(markers.Count, started);
    }
}

