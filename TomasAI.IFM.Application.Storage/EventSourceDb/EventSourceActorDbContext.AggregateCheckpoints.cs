using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

public sealed partial class EventSourceActorDbContext
{
    /// <summary>Reads an aggregate head, its newest indexed sidecar checkpoint and the bounded source tail.</summary>
    public async Task<AggregateCheckpointWindow> ReadAggregateCheckpointWindowAsync(
        string stream, string sidecar, CancellationToken cancellationToken, long through = long.MaxValue,
        long maximumCheckpointRevision = long.MaxValue, long replayAfter = -1)
    {
        var rows = await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ReadAggregateCheckpointWindow)}",
                EventSourceDbSql.ReadAggregateCheckpointWindow)
            .SetParameters(new AggregateWindowParameters(stream, sidecar, through, maximumCheckpointRevision, replayAfter))
            .ExecuteQueryAsync(static row => new AggregateCheckpointRow(row.GetLong(0),
                row.IsNull(1) ? AggregateCheckpointRowKind.None : (AggregateCheckpointRowKind)row.GetInt(1),
                row.IsNull(2) ? 0 : row.GetLong(2),
                row.IsNull(3) ? null : MapAggregateEvent(row, 3)), cancellationToken)
            .ConfigureAwait(false);
        return new AggregateCheckpointWindow(rows.FirstOrDefault()?.Head ?? 0, rows.ToArray());
    }

    /// <summary>Resolves the stream and reads at most one creation event or two matching command events.</summary>
    public async Task<IReadOnlyList<EventStreamReadModel>> FindAggregateEventsAsync(
        string stream, Guid? commandId, CancellationToken cancellationToken)
    {
        var sql = commandId.HasValue
            ? EventSourceDbSql.ReadAggregateEventByCommand
            : EventSourceDbSql.ReadAggregateCreation;
        var name = commandId.HasValue
            ? nameof(EventSourceDbSql.ReadAggregateEventByCommand)
            : nameof(EventSourceDbSql.ReadAggregateCreation);
        var rows = await _dbFactory.ActorEventSourceDb.Use($"{nameof(EventSourceDbSql)}.{name}", sql)
            .SetParameters(new AggregateFindParameters(stream, commandId))
            .ExecuteQueryAsync(static row => MapAggregateEvent(row, 0), cancellationToken)
            .ConfigureAwait(false);
        return rows.ToArray();
    }

    /// <summary>Atomically appends a sidecar snapshot and indexes its authoritative source revision.</summary>
    public Task<bool> SaveAggregateCheckpointAsync(string stream, string sidecar, long sourceRevision,
        IEvent snapshot, CancellationToken cancellationToken)
        => _aggregateCheckpointTransactions.Value.ExecuteAsync(
            (db, token) => db.AppendAggregateCheckpointAsync(stream, sidecar, sourceRevision, snapshot, token),
            cancellationToken);

    /// <summary>Appends an aggregate transition and sidecar checkpoint under one optimistic source fence.</summary>
    public Task<bool> SaveAggregateSourceWithCheckpointAsync(string stream, long expectedRevision,
        IEvent source, IEvent checkpoint, CancellationToken cancellationToken)
        => _aggregateCheckpointTransactions.Value.ExecuteAsync(async (db, token) =>
        {
            await db.AppendAsync(stream, source.CommandId, source, expectedRevision, token).ConfigureAwait(false);
            return await db.AppendAggregateCheckpointAsync(stream, $"{stream}.Snapshot", checked(expectedRevision + 1),
                checkpoint, token).ConfigureAwait(false);
        }, cancellationToken);

    static EventStreamReadModel MapAggregateEvent(IObjectDataRecord row, int offset)
        => new()
        {
            EventVersion = row.GetLong(offset),
            EventTypeName = row.GetString(offset + 1),
            StreamVersion = row.GetLong(offset + 2),
            EventData = row.GetBytes(offset + 3)
        };

    readonly record struct AggregateWindowParameters(
        string Stream, string Sidecar, long Through, long MaximumCheckpointRevision, long ReplayAfter) : IBindValue
    {
        public object Bind() => BindValues.Values(Stream, Sidecar, Through, MaximumCheckpointRevision, ReplayAfter);
    }

    readonly record struct AggregateFindParameters(string Stream, Guid? CommandId) : IBindValue
    {
        public object Bind() => CommandId is { } id ? BindValues.Values(Stream, id) : BindValues.Values(Stream);
    }
}

/// <summary>The source head and one statement's selected checkpoint, legacy candidates or replay tail.</summary>
public sealed record AggregateCheckpointWindow(long Head, IReadOnlyCollection<AggregateCheckpointRow> Rows);

/// <summary>A versioned checkpoint or event selected in an aggregate window.</summary>
public sealed record AggregateCheckpointRow(long Head, AggregateCheckpointRowKind Kind, long SourceRevision, EventStreamReadModel? Event);

public enum AggregateCheckpointRowKind
{
    None,
    IndexedCheckpoint,
    LegacyCheckpoint,
    SourceEvent
}
