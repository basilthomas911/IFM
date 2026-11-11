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
            await transaction.ExecuteAsync(MarketDataDbSql.MarketDataImportStreamFence_LockAsync_1, [stream], token)
            .ConfigureAwait(false);
            var head = await transaction.QueryAsync(MarketDataDbSql.MarketDataImportStreamFence_LockAsync_2, [stream], MapToLockRow1, token)
            .ConfigureAwait(false);
            var (id, version) = head.Single();
            var pending = await transaction.ScalarAsync(MarketDataDbSql.MarketDataImportStreamFence_LockAsync_3, [id, projector], token)
            .ConfigureAwait(false);
            if (pending is true)
                throw new InvalidOperationException($"MarketData import requires completed CRUD projection for stream {stream}.");
            versions.Add(stream, version);
        }
        return versions;
    }

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static (long Id, long Version) MapToLockRow1(global::System.Data.Common.DbDataReader row) => (Id: row.GetInt64(0), Version: row.GetInt64(1));
}
