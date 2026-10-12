using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Receipt-based scan includes late commits; a global sequence cursor cannot safely provide that guarantee.</summary>
public sealed class PostgresCommittedBusinessEventJournal : ICommittedBusinessEventJournal
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS business_subscription_projection_receipt(
          event_id bigint PRIMARY KEY REFERENCES event_log(eventVersion) ON DELETE CASCADE,
          projected_at_utc timestamptz NOT NULL DEFAULT now());
        ALTER TABLE business_subscription_projection_receipt ADD COLUMN IF NOT EXISTS handoff_completed boolean NOT NULL DEFAULT false;
        CREATE TABLE IF NOT EXISTS business_subscription_projection_issue(
          event_id bigint PRIMARY KEY REFERENCES event_log(eventVersion) ON DELETE CASCADE,
          reason_code text NOT NULL,
          detail text NOT NULL,
          rejected_at_utc timestamptz NOT NULL DEFAULT now());
        """;
    const string Columns = "SELECT el.eventStreamId,en.eventName,en.eventTypeName,el.eventVersion,el.EventPayload,el.commandId,el.eventTimestamp::text,el.StreamVersion FROM event_log el JOIN event_name_id en ON en.eventNameId=el.eventNameId ";
    const string Pending = Columns + "WHERE en.eventName=ANY($1) AND NOT EXISTS(SELECT 1 FROM business_subscription_projection_receipt r WHERE r.event_id=el.eventVersion) ORDER BY el.eventVersion LIMIT 32;";
    const string Prior = Columns + "WHERE el.eventStreamId=$1 AND el.eventVersion<=$2 AND en.eventName=ANY($3) ORDER BY el.eventVersion DESC LIMIT 1;";
    const string Handoffs = Columns + "JOIN business_subscription_projection_receipt r ON r.event_id=el.eventVersion WHERE en.eventName='WorkflowStrategyStateUpdatedEvent' AND NOT r.handoff_completed ORDER BY el.eventVersion LIMIT 32;";
    readonly string connection;
    readonly string pendingSql;
    readonly string priorSql;
    readonly string handoffsSql;

    public PostgresCommittedBusinessEventJournal(
        IDbConnectionSettings settings,
        EventLogPersistenceOptions? eventLogPersistenceOptions = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var options = (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate();
        var layout = EventLogSqlLayout.ForProduction(options);
        connection = settings[EventSourceActorDbContext.EventSourceActorDbConnection].ConnectionString;
        pendingSql = layout.Resolve(Pending);
        priorSql = layout.Resolve(Prior);
        handoffsSql = layout.Resolve(Handoffs);
    }

    internal static string CreateTableFor(EventLogPersistenceOptions? options)
    {
        var validated = (options ?? new EventLogPersistenceOptions()).Validate();
        return EventLogSqlLayout.ForProduction(validated).Resolve(CreateTable);
    }

    public Task<IReadOnlyList<EventLogReadModel>> ReadPendingAsync(IReadOnlyList<string> eventNames, CancellationToken cancellationToken)
        => ReadAsync(pendingSql, [eventNames.ToArray()], cancellationToken);

    public Task<IReadOnlyList<EventLogReadModel>> ReadPendingHandoffsAsync(CancellationToken cancellationToken)
        => ReadAsync(handoffsSql, [], cancellationToken);

    public async Task CompleteHandoffAsync(long eventId, CancellationToken cancellationToken)
    {
        var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await using var dbDisposal = db.ConfigureAwait(false);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new NpgsqlCommand("UPDATE business_subscription_projection_receipt SET handoff_completed=true WHERE event_id=$1;", db) { CommandTimeout = 10 };
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.Add(new NpgsqlParameter { Value = eventId });
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidDataException("Committed handoff receipt is missing.");
    }

    public async Task<EventLogReadModel?> ReadPriorAsync(long streamId, long throughEventId, IReadOnlyList<string> eventNames, CancellationToken cancellationToken)
        => (await ReadAsync(priorSql, [streamId, throughEventId, eventNames.ToArray()], cancellationToken).ConfigureAwait(false)).SingleOrDefault();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    async Task<IReadOnlyList<EventLogReadModel>> ReadAsync(string sql, object[] arguments, CancellationToken cancellationToken)
    {
        var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await using var dbDisposal = db.ConfigureAwait(false);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, db) { CommandTimeout = 10 };
        await using var commandDisposal = command.ConfigureAwait(false);
        foreach (var arg in arguments) command.Parameters.Add(new NpgsqlParameter { Value = arg });
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var result = new List<EventLogReadModel>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if ((await reader.GetFieldValueAsync<byte[]>(4,cancellationToken).ConfigureAwait(false)).Length > 16 * 1024 * 1024) throw new InvalidDataException("Business source event exceeds its bound.");
            result.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3),
(await                reader.GetFieldValueAsync<byte[]>(4,cancellationToken).ConfigureAwait(false)), reader.GetGuid(5), reader.GetString(6), reader.GetInt64(7)));
        }
        return result;
    }

    public async Task RejectAsync(long eventId, string reasonCode, string detail, CancellationToken cancellationToken)
    {
        if (eventId <= 0 || string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 128 || string.IsNullOrWhiteSpace(detail) || detail.Length > 4096)
            throw new ArgumentException("A bounded committed-event rejection is required.",nameof(eventId));
        var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await using var dbDisposal = db.ConfigureAwait(false);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var transaction = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        using (var issue = new NpgsqlCommand("INSERT INTO business_subscription_projection_issue(event_id,reason_code,detail) VALUES($1,$2,$3) ON CONFLICT DO NOTHING;", db, transaction) { CommandTimeout = 10 })
        {
            issue.Parameters.Add(new NpgsqlParameter { Value = eventId }); issue.Parameters.Add(new NpgsqlParameter { Value = reasonCode }); issue.Parameters.Add(new NpgsqlParameter { Value = detail });
            await issue.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        using (var receipt = new NpgsqlCommand("INSERT INTO business_subscription_projection_receipt(event_id) VALUES($1) ON CONFLICT DO NOTHING;", db, transaction) { CommandTimeout = 10 })
        {
            receipt.Parameters.Add(new NpgsqlParameter { Value = eventId });
            await receipt.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task AcknowledgeAsync(long eventId, CancellationToken cancellationToken)
    {
        var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await using var dbDisposal = db.ConfigureAwait(false);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new NpgsqlCommand("INSERT INTO business_subscription_projection_receipt(event_id) VALUES($1) ON CONFLICT DO NOTHING;", db) { CommandTimeout = 10 };
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.Add(new NpgsqlParameter { Value = eventId });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
