using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

public enum HandoffWorkload { Read, InsertReceipt, CompleteHandoff }

public class PendingBusinessHandoffBenchmarks
{
    const int History = 120_000;
    const int WriteRows = 16_384;
    const int WalRows = 8_192;
    const int LateStart = 119_041;
    const int BatchSize = 32;
    const string Receipt = "business_subscription_projection_receipt";
    const string Candidate = "business_subscription_receipt_pending_handoff";
    const string ReadSql = EventSourceActorDbSql.ReadPendingBusinessHandoffs;
    const string InsertSql = EventSourceActorDbSql.PostgresCommittedBusinessEventJournal_RejectAsync_2;
    const string CompleteSql = EventSourceActorDbSql.PostgresCommittedBusinessEventJournal_CompleteHandoffAsync_1;
    const string Workflow = "WorkflowStrategyStateUpdatedEvent";

    NpgsqlConnection db = null!;
    EventSourceSchemaDb schema = null!;
    int nextId;

    [Params(false, true)] public bool Indexed { get; set; }
    [ParamsAllValues] public HandoffWorkload Workload { get; set; }
    [Params(false, true)] public bool EarlyBacklog { get; set; }

    [GlobalSetup]
    public Task Setup() => Setup(verifyMigration: false);

    async Task Setup(bool verifyMigration)
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (connection.Host != "127.0.0.1" || connection.Port != 58231 || connection.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            connection.ConnectionString, "System.Data.Postgres");
        schema = new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance);
        await schema.CreateAllAsync().ConfigureAwait(false);
        db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection.ConnectionString);
        await db.OpenAsync().ConfigureAwait(false);
        if (verifyMigration)
        {
            await VerifyIndex().ConfigureAwait(false);
            if ((await Read().ConfigureAwait(false)).Length != 0)
                throw new InvalidOperationException("Fresh schema returned handoffs.");
            Console.WriteLine("Verified fresh schema and empty handoff read.");
        }
        await Execute($"""
            DROP INDEX IF EXISTS {Candidate};
            TRUNCATE TABLE event_log CASCADE;
            TRUNCATE TABLE event_name_id CASCADE;
            INSERT INTO event_name_id(eventnameid,eventname,eventtypename) VALUES
                (1,'{Workflow}','WorkflowV1'),(2,'{Workflow}','WorkflowV2'),(3,'OtherEvent','Other');
            INSERT INTO event_log(eventstreamid,eventnameid,eventversion,streamversion,eventpayload,commandid,eventtimestamp)
            SELECT 1,CASE n%4 WHEN 0 THEN 1 WHEN 1 THEN 2 ELSE 3 END,n,n,
                decode(lpad(to_hex(n),8,'0')||repeat('ab',256),'hex'),
                '3db31873-7f7a-4439-8a5e-654caac7594d','2026-11-12T16:00:00Z'
            FROM generate_series(1,{History + WriteRows}) n;
            """).ConfigureAwait(false);
        await ResetReceipts().ConfigureAwait(false);
        await Execute("VACUUM (ANALYZE) event_log").ConfigureAwait(false);
        await Execute("ANALYZE event_name_id").ConfigureAwait(false);
        if (verifyMigration)
        {
            await VerifyMigrationAndPlans().ConfigureAwait(false);
        }
        else if (Workload == HandoffWorkload.Read)
        {
            await VerifyReads().ConfigureAwait(false);
            await Explain().ConfigureAwait(false);
        }
        else
        {
            var start = (string)await Scalar("SELECT pg_current_wal_insert_lsn()::text").ConfigureAwait(false);
            for (var n = 0; n < WalRows / BatchSize; n++)
                await WriteBatch().ConfigureAwait(false);
            var wal = (decimal)await Scalar("SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn)", start)
                .ConfigureAwait(false);
            var completed = Workload == HandoffWorkload.CompleteHandoff ? "handoff_completed" : "NOT handoff_completed";
            var written = (long)await Scalar($"SELECT count(*) FROM {Receipt} WHERE event_id>{History} AND {completed}")
                .ConfigureAwait(false);
            if (written != WalRows)
                throw new InvalidOperationException($"Expected {WalRows} persisted writes, found {written}.");
            Console.WriteLine(FormattableString.Invariant($"WAL,{Indexed},{Workload},{written},{wal},{wal / written:F2}"));
            await ResetReceipts().ConfigureAwait(false);
        }
    }

    async Task ResetReceipts()
    {
        nextId = History;
        var firstPending = EarlyBacklog ? 1 : LateStart;
        var seededRows = Workload == HandoffWorkload.CompleteHandoff ? History + WriteRows : History;
        await Execute($"""
            DROP INDEX IF EXISTS {Candidate};
            TRUNCATE TABLE {Receipt};
            INSERT INTO {Receipt}(event_id,handoff_completed)
            SELECT n, n <= {History} AND NOT (n >= {firstPending} AND n < {firstPending + 960} AND n % 5 = 1)
            FROM generate_series(1,{seededRows}) n
            WHERE n > {History} OR n < {firstPending} OR n >= {firstPending + 960} OR n % 7 <> 0;
            """).ConfigureAwait(false);
        if (Indexed)
        {
            await schema.CreateAsync([Receipt]).ConfigureAwait(false);
            Console.WriteLine($"INDEX-BYTES,{Workload},{await Scalar($"SELECT pg_relation_size('{Candidate}')").ConfigureAwait(false)}");
        }
        await Execute($"VACUUM (ANALYZE) {Receipt}").ConfigureAwait(false);
        await Execute("CHECKPOINT").ConfigureAwait(false);
    }

    internal static async Task VerifyMigrationAsync(bool earlyBacklog)
    {
        var fixture = new PendingBusinessHandoffBenchmarks { EarlyBacklog = earlyBacklog, Workload = HandoffWorkload.Read };
        try
        {
            await fixture.Setup(verifyMigration: true).ConfigureAwait(false);
        }
        finally
        {
            if (fixture.db is not null)
                await fixture.Cleanup().ConfigureAwait(false);
        }
    }

    async Task VerifyMigrationAndPlans()
    {
        var before = await Read().ConfigureAwait(false);
        var fingerprint = await ReceiptFingerprint().ConfigureAwait(false);
        await Explain().ConfigureAwait(false);

        await schema.CreateAsync([Receipt]).ConfigureAwait(false);
        var indexId = await VerifyIndex().ConfigureAwait(false);
        await schema.CreateAsync([Receipt]).ConfigureAwait(false);
        if (indexId != await VerifyIndex().ConfigureAwait(false) ||
            fingerprint != await ReceiptFingerprint().ConfigureAwait(false))
            throw new InvalidOperationException("Schema upgrade or reapplication changed receipts or index identity.");
        Console.WriteLine($"Verified populated upgrade and reapplication with receipt fingerprint {fingerprint} and index OID {indexId}.");

        Indexed = true;
        var after = await Read().ConfigureAwait(false);
        if (!before.SequenceEqual(after))
            throw new InvalidOperationException("Migration changed the ordered handoff batch.");
        await VerifyReads().ConfigureAwait(false);
        var plan = await Explain().ConfigureAwait(false);
        if (!plan.Contains($"Index Only Scan using {Candidate}", StringComparison.Ordinal) ||
            !plan.Contains("Heap Fetches: 0", StringComparison.Ordinal) ||
            plan.Contains("Sort Key:", StringComparison.Ordinal))
            throw new InvalidOperationException("Expected a sort-free pending receipt index-only scan after vacuum.");
    }

    async Task<long> VerifyIndex()
    {
        var indexId = (long)await Scalar($"""
            SELECT COALESCE((
                SELECT indexrelid::bigint FROM pg_index
                WHERE indexrelid = to_regclass('{Candidate}') AND indrelid = '{Receipt}'::regclass
                    AND indisvalid AND indisready
                    AND pg_get_indexdef(indexrelid, 1, true) = 'event_id'
                    AND indnkeyatts = 1 AND indnatts = 1
                    AND pg_get_expr(indpred, indrelid) = '(NOT handoff_completed)'
            ), 0::bigint)
            """).ConfigureAwait(false);
        return indexId != 0 ? indexId : throw new InvalidOperationException("The expected valid partial index was missing.");
    }

    async Task<string> ReceiptFingerprint() => (string)await Scalar($"""
        SELECT count(*) || ':' || sum(hashtextextended(row_to_json(r)::text, 0)::numeric) FROM {Receipt} r
        """).ConfigureAwait(false);

    async Task<string> Explain()
    {
        Console.WriteLine($"PLAN,Indexed={Indexed},EarlyBacklog={EarlyBacklog}");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) " + ReadSql, db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        return plan;
    }

    async Task VerifyReads()
    {
        var start = EarlyBacklog ? 1 : LateStart;
        var expected = Enumerable.Range(start, 960)
            .Where(n => n % 5 == 1 && n % 7 != 0 && n % 4 is 0 or 1).Select(n => (long)n).ToArray();
        var first = await Read().ConfigureAwait(false);
        var retry = await Read().ConfigureAwait(false);
        if (!first.SequenceEqual(expected.Take(32)) || !retry.SequenceEqual(first))
            throw new InvalidOperationException("Unexpected first or retry handoff batch.");
        await using var tx = await db.BeginTransactionAsync().ConfigureAwait(false);
        foreach (var id in first)
            await ScalarAffected(CompleteSql, id).ConfigureAwait(false);
        var after = await Read().ConfigureAwait(false);
        if (!after.SequenceEqual(expected.Skip(32).Take(32)))
            throw new InvalidOperationException("Completing the first batch did not expose the next ordered batch.");
        await tx.RollbackAsync().ConfigureAwait(false);
        Console.WriteLine($"ORDERED,{Indexed},{EarlyBacklog},first={string.Join(',', first)},next={string.Join(',', after)}");
        // Remove rolled-back row versions before timing and capturing the read plan.
        await Execute($"VACUUM (ANALYZE) {Receipt}").ConfigureAwait(false);
    }

    async Task<long[]> Read()
    {
        await using var command = new NpgsqlCommand(ReadSql, db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var ids = new List<long>(32);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var id = reader.GetInt64(3);
            var type = id % 4 == 0 ? "WorkflowV1" : "WorkflowV2";
            var payload = reader.GetFieldValue<byte[]>(4);
            if (reader.GetInt64(0) != 1 || reader.GetString(1) != Workflow || reader.GetString(2) != type
                || System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(payload) != id
                || payload.Length != 260 || reader.GetGuid(5) != new Guid("3db31873-7f7a-4439-8a5e-654caac7594d")
                || reader.GetString(6) != "2026-11-12T16:00:00Z" || reader.GetInt64(7) != id)
                throw new InvalidOperationException($"Unexpected event payload or identity for {id}.");
            ids.Add(id);
        }
        return ids.ToArray();
    }

    async Task WriteBatch()
    {
        await using var batch = new NpgsqlBatch(db);
        var sql = Workload == HandoffWorkload.InsertReceipt ? InsertSql : CompleteSql;
        for (var n = 0; n < BatchSize; n++)
        {
            var command = new NpgsqlBatchCommand(sql);
            PostgresValueBinding.AddParameters(command, [(long)++nextId]);
            batch.BatchCommands.Add(command);
        }
        if (await batch.ExecuteNonQueryAsync().ConfigureAwait(false) != BatchSize)
            throw new InvalidOperationException("Receipt batch affected an unexpected number of rows.");
    }

    [Benchmark]
    public async Task Run()
    {
        if (Workload == HandoffWorkload.Read)
            await Read().ConfigureAwait(false);
        else
            await WriteBatch().ConfigureAwait(false);
    }

    [GlobalCleanup] public async Task Cleanup() => await db.DisposeAsync().ConfigureAwait(false);

    async Task Execute(string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    async Task ScalarAffected(string sql, long id)
    {
        await using var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, [id]);
        if (await command.ExecuteNonQueryAsync().ConfigureAwait(false) != 1)
            throw new InvalidOperationException($"Completion did not update receipt {id}.");
    }

    async Task<object> Scalar(string sql, params object?[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, parameters);
        return (await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }
}
