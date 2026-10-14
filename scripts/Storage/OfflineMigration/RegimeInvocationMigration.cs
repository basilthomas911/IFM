using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Storage.Tools;

/// <summary>Creates new invocation receipts from complete accepted results, never invoking workflow operators.</summary>
internal static class RegimeInvocationMigration
{
    public static async Task<object> RunAsync(string connection)
    {
        var identity = new NpgsqlConnectionStringBuilder(connection);
        if (identity.Host is not ("localhost" or "127.0.0.1") || !identity.Database.StartsWith("ifm_storage_migration_staging_", StringComparison.Ordinal))
            throw new InvalidOperationException("Invocation reconstruction requires an isolated local staging database.");
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres");
        var transactions = new PostgresEventTransaction(settings);
        long cursor = 0;
        long rebuilt = 0;
        while (true)
        {
            var page = await transactions.ExecuteOnceAsync((db, token) => db.QueryAsync("""
                SELECT n.eventtypename,l.eventversion,l.eventpayload
                FROM event_log l JOIN event_name_id n USING(eventnameid)
                WHERE l.eventversion>$1 AND n.eventtypename LIKE $2 ORDER BY l.eventversion LIMIT 128;
                """, [cursor, typeof(RegimeDiscoveryPipelineCompletedEvent).FullName + ",%"], reader =>
                    new EventStreamReadModel { EventTypeName = reader.GetString(0), EventVersion = reader.GetInt64(1), EventData = reader.GetFieldValue<byte[]>(2) }, token)).ConfigureAwait(false);
            if (page.Count == 0) break;
            foreach (var row in page)
            {
                var source = row.ToDomainEvent() as RegimeDiscoveryPipelineCompletedEvent
                    ?? throw new InvalidDataException("Persisted Regime Discovery source type differs.");
                await transactions.ExecuteOnceAsync(async (db, token) =>
                {
                    await TradeDbContextExtensions.RegimeProjectAsync(db, source, token).ConfigureAwait(false);
                    return true;
                }).ConfigureAwait(false);
                rebuilt++;
            }
            cursor = page[^1].EventVersion;
        }
        return new { rebuiltInvocations = rebuilt, sourceCursor = cursor, providersCalled = false, notificationsPublished = false };
    }
}
