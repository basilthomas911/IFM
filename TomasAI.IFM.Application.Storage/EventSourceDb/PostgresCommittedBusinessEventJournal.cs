using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Receipt-based scan includes late commits; a global sequence cursor cannot safely provide that guarantee.</summary>
public sealed class PostgresCommittedBusinessEventJournal(IDbConnectionSettings settings) : ICommittedBusinessEventJournal
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS business_subscription_projection_receipt(
          event_id bigint PRIMARY KEY REFERENCES event_log(eventVersion) ON DELETE CASCADE,
          projected_at_utc timestamptz NOT NULL DEFAULT now());
        ALTER TABLE business_subscription_projection_receipt ADD COLUMN IF NOT EXISTS handoff_completed boolean NOT NULL DEFAULT false;
        """;
    const string Columns = "SELECT el.eventStreamId,en.eventName,en.eventTypeName,el.eventVersion,el.EventPayload,el.commandId,el.eventTimestamp::text,el.StreamVersion FROM event_log el JOIN event_name_id en ON en.eventNameId=el.eventNameId ";
    const string Pending = Columns + "WHERE en.eventName=ANY($1) AND NOT EXISTS(SELECT 1 FROM business_subscription_projection_receipt r WHERE r.event_id=el.eventVersion) ORDER BY el.eventVersion LIMIT 32;";
    const string Prior = Columns + "WHERE el.eventStreamId=$1 AND el.eventVersion<=$2 AND en.eventName=ANY($3) ORDER BY el.eventVersion DESC LIMIT 1;";
    const string Handoffs = Columns + "JOIN business_subscription_projection_receipt r ON r.event_id=el.eventVersion WHERE en.eventName='WorkflowStrategyStateUpdatedEvent' AND NOT r.handoff_completed ORDER BY el.eventVersion LIMIT 32;";
    readonly string connection = settings[EventSourceActorDbContext.EventSourceActorDbConnection].ConnectionString;

    public Task<IReadOnlyList<EventLogReadModel>> ReadPendingAsync(IReadOnlyList<string> eventNames, CancellationToken cancellationToken)
        => ReadAsync(Pending, [eventNames.ToArray()], cancellationToken);

    public Task<IReadOnlyList<EventLogReadModel>> ReadPendingHandoffsAsync(CancellationToken cancellationToken)
        => ReadAsync(Handoffs, [], cancellationToken);

    public async Task CompleteHandoffAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("UPDATE business_subscription_projection_receipt SET handoff_completed=true WHERE event_id=$1;", db) { CommandTimeout = 10 };
        command.Parameters.Add(new NpgsqlParameter { Value = eventId });
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidDataException("Committed handoff receipt is missing.");
    }

    public async Task<EventLogReadModel?> ReadPriorAsync(long streamId, long throughEventId, IReadOnlyList<string> eventNames, CancellationToken cancellationToken)
        => (await ReadAsync(Prior, [streamId, throughEventId, eventNames.ToArray()], cancellationToken).ConfigureAwait(false)).SingleOrDefault();

    async Task<IReadOnlyList<EventLogReadModel>> ReadAsync(string sql, object[] arguments, CancellationToken cancellationToken)
    {
        await using var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, db) { CommandTimeout = 10 };
        foreach (var arg in arguments) command.Parameters.Add(new NpgsqlParameter { Value = arg });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<EventLogReadModel>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.GetFieldValue<byte[]>(4).Length > 16 * 1024 * 1024) throw new InvalidDataException("Business source event exceeds its bound.");
            result.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3),
                reader.GetFieldValue<byte[]>(4), reader.GetGuid(5), reader.GetString(6), reader.GetInt64(7)));
        }
        return result;
    }

    public async Task AcknowledgeAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("INSERT INTO business_subscription_projection_receipt(event_id) VALUES($1) ON CONFLICT DO NOTHING;", db) { CommandTimeout = 10 };
        command.Parameters.Add(new NpgsqlParameter { Value = eventId });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
