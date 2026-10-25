using System.Diagnostics;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.Development.Verification;

/// <summary>Removes obsolete quote-mark recovery work while retaining source events and all financial transitions.</summary>
internal static class DiscardIronCondorMarketMarkRecovery
{
    public static async Task RunAsync(IConfiguration config, CancellationToken token)
    {
        if (Process.GetProcessesByName("TomasAI.IFM.Application.Api.Server").Any(p => p.Id != Environment.ProcessId))
            throw new InvalidOperationException("Stop the development API before discarding old recovery work.");
        var connection = config["Storage:PostgresConnection"] ?? throw new InvalidOperationException("Storage:PostgresConnection is required.");
        var identity = PostgresDatabaseIdentity.Parse(connection);
        if (identity.Database != "ifm_application_development" || identity.Port != 5432 || identity.Host is not ("LOCALHOST" or "127.0.0.1"))
            throw new InvalidOperationException("Recovery cleanup is restricted to the local development database.");
        var owner = new PostgresEventTransaction(ApplicationStorageConnections.CreatePostgres(connection));
        var removed = await owner.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));", ["IronCondorPositionEventProjector:discard-market-marks"], ct);
            long after = 0; var deleted = 0;
            while (true)
            {
                var page = await db.QueryAsync("""
                    SELECT p.EventId,n.EventTypeName,e.EventPayload,c.CommandName
                    FROM event_projector_state p JOIN event_log e ON e.EventVersion=p.EventId
                    JOIN event_name_id n ON n.EventNameId=e.EventNameId
                    LEFT JOIN command_log c ON c.CommandId=e.CommandId
                    WHERE p.ProjectorName='IronCondorPositionEventProjector'
                      AND p.Outcome IN ('Processing','Retrying') AND p.EventId>$1
                    ORDER BY p.EventId LIMIT 512;
                    """, [after], row => (Id: row.GetInt64(0), Type: row.GetString(1), Payload: row.GetFieldValue<byte[]>(2),
                        Command: row.IsDBNull(3) ? "" : row.GetString(3)), ct);
                if (page.Count == 0) break;
                after = page[^1].Id;
                var ids = page.Where(row => row.Command is "ChangeTradeLegDataCommand" or "UpdateIronCondorPositionLegMarketPriceCommand")
                    .Where(row => EventLogMessagePackCodec.Shared.Deserialize(row.Type, row.Id, row.Payload)
                        is IronCondorPositionChangedEvent { PositionSnapshot.Phase: StrategyPositionPhase.MarkToMarket })
                    .Select(row => row.Id).ToArray();
                if (ids.Length != 0)
                    deleted += await db.ExecuteAsync("""
                        DELETE FROM event_projector_state WHERE ProjectorName='IronCondorPositionEventProjector'
                        AND Outcome IN ('Processing','Retrying') AND EventId=ANY($1);
                        """, [ids], ct); // Only associated outbox rows cascade; the event log and history are retained.
            }
            return deleted;
        }, token);
        Console.WriteLine($"Discarded {removed} obsolete Iron Condor market-mark recovery records; source events/history retained.");
        var remaining = await owner.ExecuteOnceAsync((db, ct) => db.ScalarAsync("""
            SELECT count(*) FROM event_projector_state WHERE ProjectorName='IronCondorPositionEventProjector'
            AND Outcome IN ('Processing','Retrying');
            """, [], ct), token);
        if (Convert.ToInt64(remaining) != 0)
            throw new InvalidOperationException("Financial or unclassified recovery work remains; transport queues were preserved.");
        // No unfinished financial projection exists in this projector. Remove old transport copies too;
        // otherwise legacy payloads without the new observation flag could recreate deleted markers.
        using var logs = LoggerFactory.Create(_ => { });
        await using var manager = new NatsConnectionManager();
        await new NatsJetStreamStartupPurge(config["MarketDataHost:NatsUrl"] ?? "nats://localhost:4222", manager,
            logs.CreateLogger<NatsJetStreamStartupPurge>()).PurgeAsync(name => name is
                "IFM_IronCondorPositionEventProjector_PROCESS" or "IFM_IronCondorPositionEventProjector_REPLAY", token);
        Console.WriteLine("Cleared only the Iron Condor position process/replay transport queues.");
    }
}
