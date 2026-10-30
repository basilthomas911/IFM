using System.Collections.Concurrent;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb.Recovery;
/// <summary>Idempotent projection and bounded operational timeline reads. Source events remain authoritative.</summary>
public static class MarketDataRecoveryLog
{
    // Keys come only from the configured MarketDataServiceDb target, never from an RPC.
    // A failed initialization is removed so replay can retry after storage returns.
    static readonly ConcurrentDictionary<string,Lazy<Task>> Schemas=new(StringComparer.Ordinal);
    public static async Task EnsureSchemaAsync(string connectionString,CancellationToken token=default)
    {
        var schema=Schemas.GetOrAdd(connectionString,key=>new Lazy<Task>(async ()=> {
            try {
                await using var connection=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(key);
                await connection.OpenAsync(token).ConfigureAwait(false);
                await using var command=new NpgsqlCommand(MarketDataServiceSchemaSql.CreateSchema+MarketDataServiceSchemaSql.CreateRecoveryLog,connection) {CommandTimeout=120};
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            catch {Schemas.TryRemove(key,out _);throw;}
        }));
        await schema.Value.WaitAsync(token).ConfigureAwait(false);
    }

    public const string InsertSql = """
        INSERT INTO market_data_service.marketdata_recovery_log
          (record_id,operation_id,sequence,value_date,role,dataset,kind,occurred_at_utc,received_at_utc,payload_digest,payload,source_event_id)
        VALUES (@record,@operation,@sequence,@date,@role,@dataset,@kind,@occurred,@received,@digest,@payload,@event)
        ON CONFLICT (record_id) DO UPDATE SET record_id=EXCLUDED.record_id
        WHERE marketdata_recovery_log.payload_digest=EXCLUDED.payload_digest
          AND marketdata_recovery_log.payload=EXCLUDED.payload
        RETURNING record_id;
        """;
    /// <summary>A digest conflict returns no row and is rejected. Sequence conflicts are PostgreSQL uniqueness failures.</summary>
    public static async Task ProjectAsync(string connectionString, MarketDataFeedRecoveryFact source, CancellationToken token = default)
    {
        var fact = RecoveryRecord.Parse(source.Payload, source.Digest);
        await EnsureSchemaAsync(connectionString,token).ConfigureAwait(false);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(InsertSql, connection) { CommandTimeout = 120 };
        command.Parameters.AddWithValue("record",fact.RecordId); command.Parameters.AddWithValue("operation",fact.OperationId);
        command.Parameters.AddWithValue("sequence",fact.Sequence); command.Parameters.AddWithValue("date",fact.ValueDate);
        command.Parameters.AddWithValue("role",fact.Role); command.Parameters.AddWithValue("dataset",fact.Dataset);
        command.Parameters.AddWithValue("kind",fact.Kind); command.Parameters.AddWithValue("occurred",fact.OccurredAtUtc);
        command.Parameters.AddWithValue("received",source.ReceivedOn); command.Parameters.AddWithValue("digest",source.Digest);
        command.Parameters.AddWithValue("payload",source.Payload); command.Parameters.AddWithValue("event",source.EventId);
        if (await command.ExecuteScalarAsync(token).ConfigureAwait(false) is null)
            throw new InvalidDataException("Recovery projection RecordId/digest conflict.");
    }
    /// <summary>Returns at most 4096 copied JSON records, ordered per incident. Historical ValueDates remain queryable.</summary>
    public static async Task<IReadOnlyList<string>> QueryAsync(string connectionString, DateOnly valueDate,
        string? dataset = null, Guid? operationId = null, int limit = 256, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit,1); ArgumentOutOfRangeException.ThrowIfGreaterThan(limit,4096);
        await EnsureSchemaAsync(connectionString,token).ConfigureAwait(false);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            SELECT payload FROM market_data_service.marketdata_recovery_log
            WHERE value_date=@date AND (@dataset IS NULL OR dataset=@dataset) AND (@operation IS NULL OR operation_id=@operation)
            ORDER BY operation_id,sequence LIMIT @limit
            """, connection) { CommandTimeout=120 };
        command.Parameters.AddWithValue("date",valueDate);
        command.Parameters.Add(TomasAI.IFM.Framework.Storage.Postgres.PostgresValueBinding.Named("dataset",TomasAI.IFM.Shared.Storage.BindValues.Text(dataset)));
        command.Parameters.Add(TomasAI.IFM.Framework.Storage.Postgres.PostgresValueBinding.Named("operation",TomasAI.IFM.Shared.Storage.BindValues.Uuid(operationId)));
        command.Parameters.AddWithValue("limit",limit);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) rows.Add(reader.GetString(0));
        return rows;
    }
}
