using TomasAI.IFM.Application.Storage.EventSourceDb;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Serializes an accepted import with committed CRUD work for its destination streams.</summary>
public static class MarketDataImportStreamFence
{
    /// <summary>Reserves the destination heads before the caller mutates the projected tables.</summary>
    public static async Task<IReadOnlyDictionary<string, long>> LockAsync(
        EnlistedEventTransaction transaction, IEnumerable<string> streams, string projector, CancellationToken token)
    {
        var versions = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var stream in streams.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await transaction.ExecuteAsync("""
                INSERT INTO event_stream_id (eventstream) VALUES ($1)
                ON CONFLICT (eventstream) DO NOTHING;
                """, [stream], token).ConfigureAwait(false);
            var head = await transaction.QueryAsync("""
                SELECT eventstreamid, currentversion FROM event_stream_id
                WHERE eventstream = $1 FOR UPDATE;
                """, [stream], row => (Id: row.GetInt64(0), Version: row.GetInt64(1)), token).ConfigureAwait(false);
            var (id, version) = head.Single();
            var pending = await transaction.ScalarAsync("""
                SELECT EXISTS (
                    SELECT 1 FROM event_log source
                    JOIN event_projector_state projection ON projection.eventid = source.eventversion
                    WHERE source.eventstreamid = $1 AND projection.projectorname = $2
                      AND (projection.stage IS DISTINCT FROM 'Completed'
                        OR projection.outcome IS NULL
                        OR projection.outcome NOT IN ('Completed', 'AlreadyCompleted', 'Superseded'))
                );
                """, [id, projector], token).ConfigureAwait(false);
            if (pending is true)
                throw new InvalidOperationException($"MarketData import requires completed CRUD projection for stream {stream}.");
            versions.Add(stream, version);
        }
        return versions;
    }
}
