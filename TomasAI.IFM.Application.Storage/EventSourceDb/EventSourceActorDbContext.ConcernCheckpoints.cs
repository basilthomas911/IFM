using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

public sealed partial class EventSourceActorDbContext
{
    /// <summary>Reads a concern and its legacy prefix at one PostgreSQL statement boundary.</summary>
    public async Task ReadConcernCheckpointWindowAsync(string concernStream, string legacyStream,
        Type checkpointType, bool useCheckpoint, Action<IEnumerable<ConcernCheckpointRow>> reduce,
        CancellationToken cancellationToken)
    {
        var identities = EventTypeRegistry.Default.GetReadIdentities(checkpointType)
            .Distinct(StringComparer.Ordinal).ToArray();
        await _dbFactory.ActorEventSourceDb
            .Use($"{nameof(EventSourceDbSql)}.{nameof(EventSourceDbSql.ReadConcernCheckpointWindow)}",
                EventSourceDbSql.ReadConcernCheckpointWindow)
            .SetParameters(new ConcernWindowParameters(concernStream, legacyStream, identities, useCheckpoint))
            .ExecuteMapReduceAsync(static row => new ConcernCheckpointRow(row.GetLong(0), row.GetLong(1),
                row.GetLong(2), row.GetBool(3), row.GetBool(4), row.IsNull(5) ? null : MapAggregateEvent(row, 5)),
                reduce, cancellationToken).ConfigureAwait(false);
    }

    readonly record struct ConcernWindowParameters(string Concern, string Legacy, string[] Identities, bool UseCheckpoint) : IBindValue
    {
        public object Bind() => BindValues.Values(Concern, Legacy, Identities, UseCheckpoint);
    }
}

/// <summary>A source event and the paired concern and legacy heads observed by its read statement.</summary>
public sealed record ConcernCheckpointRow(long ConcernHead, long LegacyHead, long CheckpointVersion,
    bool IsLegacy, bool IsCheckpoint, EventStreamReadModel? Event);
