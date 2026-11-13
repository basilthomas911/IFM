using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class ReceiptAntiJoinBenchmarks
{
    const int History = 96000;
    const string Workflow = "WorkflowStrategyStateUpdatedEvent";
    const string Business = "BusinessEvent";
    const string Projection = "risk-history-v1";
    const string OtherProjection = "other-projection";
    static readonly string[] BusinessNames = [Workflow, Business, Business];
    static readonly string[] Queries =
    [
        LegacySql("en.eventname=ANY($1)", "business_subscription_projection_receipt", ""),
        LegacySql("en.eventname=$1", "risk_history_projection_receipt", "AND r.projection_name=$2"),
        LegacySql("en.eventname='WorkflowStrategyStateUpdatedEvent'", "risk_history_projection_receipt", "AND r.projection_name=$1")
    ];
    static readonly string[] CandidateSql =
    [
        BuildSql("en.eventname=ANY($1)", "business_subscription_projection_receipt", ""),
        BuildSql("en.eventname=$1", "risk_history_projection_receipt", "WHERE projection_name=$2"),
        BuildSql("en.eventname='WorkflowStrategyStateUpdatedEvent'", "risk_history_projection_receipt", "WHERE projection_name=$1")
    ];
    static readonly string[] PerNameSql =
    [
        EventSourceActorDbSql.ReadPendingBusinessEvents,
        EventSourceActorDbSql.ReadPendingRiskJournalEventsByName,
        EventSourceActorDbSql.RiskHistoryJournal_PageAsync_1
    ];
    static readonly object?[][] Parameters = [[BusinessNames], [Workflow, Projection], [Projection]];
    static readonly Guid CommandId = new("3db31873-7f7a-4439-8a5e-654caac7594d");

    [Params(0, 1, 2)] public int Reader { get; set; }

    static string BuildSql(string nameFilter, string receipt, string receiptFilter) => $"""
        WITH pending AS MATERIALIZED (
            SELECT eventversion FROM (
                SELECT e.eventversion FROM event_name_id en
                JOIN event_log e ON e.eventnameid=en.eventnameid WHERE {nameFilter}
                EXCEPT
                SELECT event_id FROM {receipt} {receiptFilter}
            ) missing
            ORDER BY eventversion LIMIT 32
        )
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
               e.eventtimestamp::text,e.streamversion
        FROM pending p JOIN event_log e ON e.eventversion=p.eventversion
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        ORDER BY e.eventversion;
        """;

    static string LegacySql(string nameFilter, string receipt, string receiptFilter) => $"""
        SELECT e.eventstreamid,en.eventname,en.eventtypename,e.eventversion,e.eventpayload,e.commandid,
               e.eventtimestamp::text,e.streamversion
        FROM event_log e JOIN event_name_id en ON en.eventnameid=e.eventnameid
        WHERE {nameFilter}
          AND NOT EXISTS(SELECT 1 FROM {receipt} r WHERE r.event_id=e.eventversion {receiptFilter})
        ORDER BY e.eventversion LIMIT 32;
        """;

    static string ConnectionString()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        return settings.ConnectionString;
    }

    static NpgsqlConnection Connection() =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString());

    internal static async Task PrepareAsync(bool earlyPending = false, bool verifyApplied = false)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, ConnectionString(), "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync().ConfigureAwait(false);
        await Execute(db, """
            INSERT INTO event_name_id(eventnameid,eventname,eventtypename) VALUES
                (1,'WorkflowStrategyStateUpdatedEvent','WorkflowV1'),
                (2,'BusinessEvent','BusinessV1'),
                (3,'UnrelatedEvent','UnrelatedV1'),
                (4,'WorkflowStrategyStateUpdatedEvent','WorkflowV2');
            """).ConfigureAwait(false);
        await SeedEventsAsync(db, 1, 0, History - 1, "$2 + id + 1", "id % 10", null).ConfigureAwait(false);
        await Execute(db, $"""
            INSERT INTO business_subscription_projection_receipt(event_id)
            SELECT eventversion FROM event_log WHERE eventnameid<>3 AND eventversion<={History - 240};
            INSERT INTO risk_history_projection_receipt(projection_name,event_id)
            SELECT '{Projection}',eventversion FROM event_log
            WHERE eventnameid IN (1,4) AND eventversion<={History - 240};
            INSERT INTO risk_history_projection_receipt(projection_name,event_id)
            SELECT '{OtherProjection}',eventversion FROM event_log WHERE eventnameid IN (1,4);
            """).ConfigureAwait(false);
        foreach (var table in new[] { "event_log", "event_name_id", "business_subscription_projection_receipt", "risk_history_projection_receipt" })
            await Execute(db, $"VACUUM (ANALYZE) {table};").ConfigureAwait(false);
        await VerifyAsync(db, new PostgresCommittedBusinessEventJournal(settings),
            new PostgresEventTransaction(settings)).ConfigureAwait(false);
        if (earlyPending)
        {
            await Execute(db, $"""
                DELETE FROM business_subscription_projection_receipt WHERE event_id BETWEEN 2 AND 401;
                DELETE FROM risk_history_projection_receipt WHERE projection_name='{Projection}' AND event_id BETWEEN 2 AND 401;
                """).ConfigureAwait(false);
            await using var tx = await db.BeginTransactionAsync().ConfigureAwait(false);
            var pages = await CompareAllAsync(db, tx).ConfigureAwait(false);
            if (pages.Any(page => page.Length != 32 || page[0].Id != 2 || page[^1].Id > 401))
                throw new InvalidOperationException("The early backlog was not returned correctly.");
            await tx.CommitAsync().ConfigureAwait(false);
            Console.WriteLine("All three readers matched the 32 oldest pending events in the early-backlog fixture.");
        }
        for (var i = 0; i < Queries.Length; i++)
        {
            await Explain(db, i, candidate: false).ConfigureAwait(false);
            await Explain(db, i, candidate: true).ConfigureAwait(false);
            await Explain(db, $"reader {i}, production per-name", PerNameSql[i], Parameters[i], verifyApplied).ConfigureAwait(false);
        }
    }

    // Reserve a version by inserting on a separate transaction and expose it only at commit.
    static Task SeedEventsAsync(NpgsqlConnection db, long stream, long start, long end, string version, string bucket,
        NpgsqlTransaction? tx) => Execute(db, $"""
        INSERT INTO event_log(eventstreamid,eventnameid,eventversion,streamversion,eventpayload,commandid,eventtimestamp)
        SELECT $2, CASE WHEN ({bucket})=0 THEN 1 WHEN ({bucket})=1 THEN 4
                        WHEN ({bucket})=2 THEN 2 ELSE 3 END,
               {version},id+1,decode(repeat('ab',512),'hex'),$1,'2026-11-12T16:00:00Z'
        FROM generate_series($3::bigint,$4::bigint) id;
        """, [CommandId, stream, start, end], tx);

    static async Task VerifyAsync(NpgsqlConnection db, PostgresCommittedBusinessEventJournal business,
        PostgresEventTransaction transactions)
    {
        await using var late = Connection();
        await late.OpenAsync().ConfigureAwait(false);
        await using var tx = await late.BeginTransactionAsync().ConfigureAwait(false);
        await SeedEventsAsync(late, 2, 0, 0, "$2 - 1", "0", tx).ConfigureAwait(false);
        await using var check = await db.BeginTransactionAsync().ConfigureAwait(false);
        await Execute(db, "DELETE FROM business_subscription_projection_receipt WHERE event_id=2;", tx: check).ConfigureAwait(false);
        await Execute(db, "DELETE FROM risk_history_projection_receipt WHERE projection_name=$1 AND event_id=2;",
            [Projection], check).ConfigureAwait(false);

        var before = await CompareAllAsync(db, check).ConfigureAwait(false);
        await VerifyBusinessEntryAsync(business, 1, visible: false).ConfigureAwait(false);
        if (before.Any(page => page.Length != 32 || page[0].Id != 2 || page.Any(row => row.Id == 1)))
            throw new InvalidOperationException("The receipt gap or late transaction visibility differed.");
        var retry = await CompareAllAsync(db, check).ConfigureAwait(false);
        if (!before.Zip(retry).All(pair => pair.First.SequenceEqual(pair.Second)))
            throw new InvalidOperationException("An unacknowledged retry changed the page.");

        await tx.CommitAsync().ConfigureAwait(false);
        var arrived = await CompareAllAsync(db, check).ConfigureAwait(false);
        await VerifyBusinessEntryAsync(business, 1, visible: true).ConfigureAwait(false);
        if (arrived.Any(page => page[0].Id != 1 || page[1].Id != 2))
            throw new InvalidOperationException("The late committed lower event did not lead the next page.");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Execute(db, EventSourceActorDbSql.PostgresCommittedBusinessEventJournal_RejectAsync_2, [1L], check).ConfigureAwait(false);
            await Execute(db, EventSourceActorDbSql.RiskHistoryJournal_AcknowledgeAsync_1, [Projection, 1L], check).ConfigureAwait(false);
        }
        var acknowledged = await CompareAllAsync(db, check).ConfigureAwait(false);
        if (!before.Zip(acknowledged).All(pair => pair.First.SequenceEqual(pair.Second)))
            throw new InvalidOperationException("Acknowledging the late event changed another pending event.");
        await check.RollbackAsync().ConfigureAwait(false);
        await VerifyRiskEntryPointsAsync(db, transactions).ConfigureAwait(false);
        await Execute(db, "DELETE FROM event_log WHERE eventversion=1;").ConfigureAwait(false);
        Console.WriteLine("All three readers matched ordered IDs and payloads across receipt gaps, unacknowledged retries, late lower-ID commits and repeated acknowledgements. Receipts in the other risk projection did not suppress these events.");
    }

    static async Task VerifyRiskEntryPointsAsync(NpgsqlConnection db, PostgresEventTransaction transactions)
    {
        var journal = new RiskHistoryJournal(transactions);
        var expected = await ReadAsync(db, Queries[1], Parameters[1]).ConfigureAwait(false);
        var named = await transactions.ExecuteOnceAsync((tx, token) =>
            journal.ReadPendingPageAsync(tx, Workflow, Projection, token)).ConfigureAwait(false);
        if (!expected.SequenceEqual(named.Select(ToReceiptRow)))
            throw new InvalidOperationException("The named risk journal entry point changed ordered results.");

        // Replace just the workflow page's synthetic bytes so the production page can deserialize it.
        var payload = new EventLogMessagePackCodec().Serialize(new WorkflowStrategyStateUpdatedEvent
        {
            Id = CommandId, CommandId = CommandId, WorkflowRevision = 7
        });
        var ids = expected.Select(row => row.Id).ToArray();
        await Execute(db, "UPDATE event_log SET eventpayload=$1 WHERE eventversion=ANY($2);", [payload, ids]).ConfigureAwait(false);
        var type = typeof(WorkflowStrategyStateUpdatedEvent);
        await Execute(db, "UPDATE event_name_id SET eventtypename=$1 WHERE eventnameid=1;",
            [type.AssemblyQualifiedName!]).ConfigureAwait(false);
        // The fixture keeps a second event-name ID for the same logical event. Give its rows a resolvable type alias.
        await Execute(db, "UPDATE event_name_id SET eventtypename=$1 WHERE eventnameid=4;",
            [$"{type.FullName}, {type.Assembly.GetName().Name}"]).ConfigureAwait(false);
        var decoded = await journal.PageAsync(History, projectionName: Projection).ConfigureAwait(false);
        if (!ids.SequenceEqual(decoded.Select(row => row.EventId)) || decoded.Any(row => row.WorkflowRevision != 7))
            throw new InvalidOperationException("The workflow risk-history entry point changed decoded events or suppressed a lower ID.");
        await Execute(db, "UPDATE event_log SET eventpayload=decode(repeat('ab',512),'hex') WHERE eventversion=ANY($1);", [ids]).ConfigureAwait(false);
        await Execute(db, """
            UPDATE event_name_id SET eventtypename = CASE WHEN eventnameid=1 THEN 'WorkflowV1' ELSE 'WorkflowV2' END
            WHERE eventnameid IN (1,4);
            """).ConfigureAwait(false);
        Console.WriteLine("Production business journal, enlisted named risk reader and decoded workflow risk-history page verified.");
    }

    static async Task VerifyBusinessEntryAsync(PostgresCommittedBusinessEventJournal journal, long lateId, bool visible)
    {
        var rows = await journal.ReadPendingAsync(BusinessNames, default).ConfigureAwait(false);
        if (rows.Count != 32 || rows.Any(row => row.EventVersion == lateId) != visible
            || !rows.Select(row => row.EventVersion).SequenceEqual(rows.Select(row => row.EventVersion).Order()))
            throw new InvalidOperationException("The business journal entry point changed ordering or late-commit visibility.");
    }

    static async Task<ReceiptRow[][]> CompareAllAsync(NpgsqlConnection db, NpgsqlTransaction tx)
    {
        var result = new ReceiptRow[Queries.Length][];
        for (var i = 0; i < Queries.Length; i++)
        {
            var current = await ReadAsync(db, Queries[i], Parameters[i], tx).ConfigureAwait(false);
            var candidate = await ReadAsync(db, CandidateSql[i], Parameters[i], tx).ConfigureAwait(false);
            var perName = await ReadAsync(db, PerNameSql[i], Parameters[i], tx).ConfigureAwait(false);
            if (!current.SequenceEqual(candidate) || !current.SequenceEqual(perName))
                throw new InvalidOperationException($"Reader {i} changed ordered results.");
            result[i] = current;
        }
        return result;
    }

    [Benchmark(Baseline = true)] public Task<ReceiptRow[]> Current() => ReadAsync(Queries[Reader]);
    [Benchmark] public Task<ReceiptRow[]> Candidate() => ReadAsync(CandidateSql[Reader]);
    [Benchmark] public Task<ReceiptRow[]> PerName() => ReadAsync(PerNameSql[Reader]);

    async Task<ReceiptRow[]> ReadAsync(string sql)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        return await ReadAsync(db, sql, Parameters[Reader]).ConfigureAwait(false);
    }

    static async Task<ReceiptRow[]> ReadAsync(NpgsqlConnection db, string sql, object?[] parameters, NpgsqlTransaction? tx = null)
    {
        await using var command = Command(db, sql, parameters, tx);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var rows = new List<ReceiptRow>(32);
        while (await reader.ReadAsync().ConfigureAwait(false))
            rows.Add(ToReceiptRow(EventSourceActorDbContext.MapToJournalEvent(reader)));
        return rows.ToArray();
    }

    static Task Explain(NpgsqlConnection db, int reader, bool candidate) =>
        Explain(db, $"reader {reader}, candidate={candidate}", candidate ? CandidateSql[reader] : Queries[reader], Parameters[reader]);

    static ReceiptRow ToReceiptRow(EventLogReadModel row) => new(row.EventStreamId, row.EventName, row.EventTypeName,
        row.EventVersion, Convert.ToHexString(row.EventData), row.CommandId, row.EventTimestamp, row.StreamVersion);

    static async Task Explain(NpgsqlConnection db, string label, string sql, object?[] parameters, bool verifyApplied = false)
    {
        Console.WriteLine($"Plan for {label}:");
        await using var command = Command(db, "EXPLAIN (ANALYZE, BUFFERS) " + sql, parameters);
        await using var result = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await result.ReadAsync().ConfigureAwait(false))
            lines.Add(result.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        if (verifyApplied && (!plan.Contains("CTE pending", StringComparison.Ordinal)
            || !plan.Contains("Index Only Scan using ix_event_log_event_name_version", StringComparison.Ordinal)
            || !lines.Any(line => line.Contains("Index Scan using ux_event_log_event_version", StringComparison.Ordinal)
                && line.Contains("loops=32", StringComparison.Ordinal))))
            throw new InvalidOperationException($"The production plan did not restrict payload lookup to the final page for {label}.");
    }

    static async Task Execute(NpgsqlConnection db, string sql, object?[]? parameters = null, NpgsqlTransaction? tx = null)
    {
        await using var command = Command(db, sql, parameters ?? [], tx);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    static NpgsqlCommand Command(NpgsqlConnection db, string sql, object?[] parameters, NpgsqlTransaction? tx = null)
    {
        var command = new NpgsqlCommand(sql, db, tx);
        PostgresValueBinding.AddParameters(command, parameters);
        return command;
    }

    public sealed record ReceiptRow(long Stream, string Name, string Type, long Id, string Payload, Guid Command, string Timestamp, long Version);
}
