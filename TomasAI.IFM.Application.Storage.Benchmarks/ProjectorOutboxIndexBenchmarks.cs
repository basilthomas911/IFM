using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class ProjectorOutboxIndexBenchmarks
{
    const string ClaimOrderIndex = "ix_event_projector_outbox_claim_order_bench";
    const string ClaimOrderPredicate = "status IN ('Pending', 'Retrying', 'Publishing')";
    static readonly string OrderedClaimSql = EventSourceActorDbSql.ClaimEventProjectorOutbox.Replace(
        "WHERE projectorname = $1", "WHERE projectorname = $1 AND " + ClaimOrderPredicate, StringComparison.Ordinal);
    internal static bool UseOrderedClaim { get; set; }
    const int QueueSize = 12000;
    const int Projectors = 16;
    const int BatchSize = 32;
    const string Target = "Projector-1";
    const string Checks = "Projector-check";
    const string Writes = "Projector-write";
    const string PendingIndex = "ix_event_projector_outbox_projector_pending";
    const string LeaseIndex = "ix_event_projector_outbox_projector_lease";
    static readonly DateTime Now = new(2026, 11, 12, 16, 0, 0, DateTimeKind.Utc);
    static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    static readonly Guid ClaimToken = new("10000000-0000-0000-0000-000000000001");
    static readonly Guid ExpiredToken = new("10000000-0000-0000-0000-000000000002");
    EventSourceActorDbContext context = null!;
    int writes;
    string initialWal = null!;

    static string ConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        return builder.ConnectionString;
    }

    static IDbConnectionSettings Settings() => new DbConnectionSettings().Add(
        EventSourceActorDbContext.EventSourceActorDbConnection, ConnectionString(), "System.Data.Postgres");

    static NpgsqlConnection Connection() =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString());

    static EventSourceActorDbContext CreateContext()
    {
        EventSourceActorDbContext? store = null;
        var factory = new DbContextFactory(new DbContextResolver(_ => store!));
        return store = new(Settings(), factory, NullLogger<DbProvider>.Instance);
    }

    internal static async Task PrepareAsync(bool withIndexes, bool verifyMigration = false, bool compareOrder = false)
    {
        await using var connection = Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        if (await ScalarAsync(connection, "SELECT to_regclass('event_projector_outbox') IS NULL AND current_setting('fsync') = 'on' AND current_setting('synchronous_commit') = 'on' AND current_setting('full_page_writes') = 'on';")
            .ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Use a fresh database with full WAL durability.");
        var schema = new EventSourceSchemaDb(Settings(), NullLogger<DbProvider>.Instance);
        await schema.CreateAllAsync().ConfigureAwait(false);
        if (verifyMigration)
            Console.WriteLine($"Fresh schema index OIDs: {await VerifyIndexesAsync(connection).ConfigureAwait(false)}.");

        // Restore the pre-index layout on this disposable fixture before loading the queue.
        await ExecuteAsync(connection, $"DROP INDEX {PendingIndex}, {LeaseIndex};").ConfigureAwait(false);
        await ExecuteAsync(connection, $"""
            INSERT INTO event_log(eventstreamid,eventnameid,eventversion,streamversion,eventpayload,commandid,eventtimestamp)
            SELECT 1,1,id,id,decode('01','hex'),$1,'2026-11-12T16:00:00Z'
            FROM generate_series(1,{QueueSize}) id;
            """, [ExpiredToken]).ConfigureAwait(false);
        await ExecuteAsync(connection, $"""
            INSERT INTO event_projector_state(eventid,actorname,projectorname,isreplay,attemptnumber,outcome,stage,
                errormessage,createdtimestamp,updatedtimestamp,eventstreamid,sourceeventname,streamversion)
            SELECT id,'Actor',projector,FALSE,0,'Processing','ApplyProjection','','','',1,'Benchmark',id
            FROM generate_series(1,{QueueSize}) id
            CROSS JOIN (
                SELECT 'Projector-' || p AS projector FROM generate_series(1,{Projectors}) p
                UNION ALL SELECT '{Writes}' UNION ALL SELECT '{Checks}'
            ) projectors
            WHERE projector <> '{Checks}' OR id <= 10;
            """).ConfigureAwait(false);
        await ExecuteAsync(connection, $"""
            INSERT INTO event_projector_outbox
                (projectorname,eventid,effectkind,messageid,eventtypename,eventpayload,status,attemptcount,
                 nextattemptatutc,createdatutc,dispatchtoken,dispatchleaseexpiresatutc)
            SELECT 'Projector-' || p,id,'ProcessingPublication',p || '-' || id,'Benchmark',decode(repeat('ab',128),'hex'),
                CASE WHEN bucket < 3 THEN 'Pending' WHEN bucket < 6 THEN 'Retrying'
                     WHEN bucket < 16 THEN 'Publishing' WHEN bucket < 26 THEN 'Retrying' ELSE 'Published' END,
                CASE WHEN bucket < 3 THEN 0 ELSE 1 END,
                CASE WHEN bucket >= 16 AND bucket < 26 THEN $2::timestamptz + interval '1 minute'
                     WHEN bucket >= 4 AND bucket < 6 THEN $2::timestamptz - interval '1 minute' END,
                $2::timestamptz - (({QueueSize}-id)/2) * interval '1 second',
                CASE WHEN bucket >= 6 AND bucket < 16 THEN $1 END,
                CASE WHEN bucket >= 6 AND bucket < 16 THEN $2 + (CASE WHEN bucket < 9 THEN -1 ELSE 1 END) * interval '1 minute' END
            FROM generate_series(1,{Projectors}) p
            CROSS JOIN generate_series(1,{QueueSize}) id
            CROSS JOIN LATERAL (SELECT id % 100 AS bucket) b;
            """, [ExpiredToken, Now]).ConfigureAwait(false);
        await ExecuteAsync(connection, "VACUUM (ANALYZE) event_projector_outbox;").ConfigureAwait(false);
        if (verifyMigration)
        {
            Console.WriteLine("Pre-migration claim:");
            await ExplainAsync(connection).ConfigureAwait(false);
        }
        var before = verifyMigration ? await ReadQueueFingerprintAsync(connection).ConfigureAwait(false) : null;
        if (withIndexes)
            await schema.CreateAsync(["event_projector_outbox_v2"]).ConfigureAwait(false);
        if (compareOrder && UseOrderedClaim)
            await ExecuteAsync(connection, $"""
                CREATE INDEX {ClaimOrderIndex} ON event_projector_outbox(projectorname,createdatutc,eventid,effectkind)
                WHERE {ClaimOrderPredicate};
                """).ConfigureAwait(false);
        if (verifyMigration)
        {
            var oids = await VerifyIndexesAsync(connection).ConfigureAwait(false);
            if (!Equals(before, await ReadQueueFingerprintAsync(connection).ConfigureAwait(false)))
                throw new InvalidOperationException("Applying the index migration changed the existing queue.");
            await schema.CreateAsync(["event_projector_outbox_v2"]).ConfigureAwait(false);
            if (oids != await VerifyIndexesAsync(connection).ConfigureAwait(false)
                || !Equals(before, await ReadQueueFingerprintAsync(connection).ConfigureAwait(false)))
                throw new InvalidOperationException("Reapplying the migration changed the index identities or existing queue.");
            Console.WriteLine($"Populated upgrade and reapplication passed. Index OIDs: {oids}. Queue rows and fingerprint: {before}.");
            Console.WriteLine("Post-migration claim:");
        }
        Console.WriteLine($"Candidate index bytes: {await ScalarAsync(connection, $"""
            SELECT COALESCE(pg_relation_size(to_regclass('{PendingIndex}')),0)
                 + COALESCE(pg_relation_size(to_regclass('{LeaseIndex}')),0);
            """).ConfigureAwait(false)}.");
        await ExplainAsync(connection, verifyMigration).ConfigureAwait(false);
        if (compareOrder)
        {
            Console.WriteLine($"Ordered claim index bytes: {await ScalarAsync(connection,
                $"SELECT COALESCE(pg_relation_size(to_regclass('{ClaimOrderIndex}')),0);").ConfigureAwait(false)}.");
            await ExplainAsync(connection, asOf: Now.AddMinutes(-2)).ConfigureAwait(false);
            await VerifyBatchAsync(connection).ConfigureAwait(false);
        }
        await VerifyClaimsAsync(connection, CreateContext()).ConfigureAwait(false);
    }

    static async Task<(long Pending, long Lease)> VerifyIndexesAsync(NpgsqlConnection connection)
    {
        async Task<long> Read(string name, string key, string predicate)
        {
            var oid = await ScalarAsync(connection, """
                SELECT i.indexrelid::bigint
                FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid
                JOIN pg_am am ON am.oid = c.relam
                WHERE i.indexrelid = to_regclass($1) AND i.indrelid = 'event_projector_outbox'::regclass
                    AND i.indisvalid AND i.indisready AND am.amname = 'btree' AND i.indnatts = 2
                    AND pg_get_indexdef(i.indexrelid,1,true) = 'projectorname'
                    AND pg_get_indexdef(i.indexrelid,2,true) = $2
                    AND pg_get_expr(i.indpred,i.indrelid) = $3;
                """, [name, key, predicate]).ConfigureAwait(false);
            return oid is long value ? value : throw new InvalidOperationException($"Index {name} is missing or has a different definition.");
        }
        return (await Read(PendingIndex, "nextattemptatutc",
                "((status)::text = ANY ((ARRAY['Pending'::character varying, 'Retrying'::character varying])::text[]))").ConfigureAwait(false),
            await Read(LeaseIndex, "dispatchleaseexpiresatutc", "((status)::text = 'Publishing'::text)").ConfigureAwait(false));
    }

    static Task<object?> ReadQueueFingerprintAsync(NpgsqlConnection connection) => ScalarAsync(connection, """
        SELECT count(*) || ':' || sum(hashtextextended(row_to_json(o)::text, 0)::numeric)
        FROM event_projector_outbox o;
        """);

    static async Task ExplainAsync(NpgsqlConnection connection, bool verifyIndexes = false, DateTime? asOf = null)
    {
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, WAL) " +
            (UseOrderedClaim ? OrderedClaimSql : EventSourceActorDbSql.ClaimEventProjectorOutbox), connection, transaction);
        var now = asOf ?? Now;
        PostgresValueBinding.AddParameters(command, [Target, ClaimToken, now + Lease, now, BatchSize]);
        await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
        {
            Console.WriteLine($"Claim plan at {now:O}:");
            var lines = new List<string>();
            while (await reader.ReadAsync().ConfigureAwait(false))
                lines.Add(reader.GetString(0));
            var plan = string.Join(Environment.NewLine, lines);
            Console.WriteLine(plan);
            if (verifyIndexes && (!plan.Contains($"Bitmap Index Scan on {PendingIndex}", StringComparison.Ordinal)
                || !plan.Contains($"Bitmap Index Scan on {LeaseIndex}", StringComparison.Ordinal)
                || !plan.Contains("Sort Key: event_projector_outbox.createdatutc, event_projector_outbox.eventid, event_projector_outbox.effectkind",
                    StringComparison.Ordinal)))
                throw new InvalidOperationException("The claim plan did not use both scoped indexes and the established order.");
        }
        await transaction.RollbackAsync().ConfigureAwait(false);
    }

    static async Task VerifyBatchAsync(NpgsqlConnection connection)
    {
        // Compute the expected first batch from the current eligibility and total order before claiming.
        await using var command = new NpgsqlCommand("""
            SELECT eventid FROM event_projector_outbox
            WHERE projectorname=$1 AND
                ((status IN ('Pending','Retrying') AND (nextattemptatutc IS NULL OR nextattemptatutc <= $2))
                  OR (status='Publishing' AND dispatchleaseexpiresatutc <= $2))
            ORDER BY createdatutc,eventid,effectkind LIMIT $3;
            """, connection);
        PostgresValueBinding.AddParameters(command, [Target, Now, BatchSize]);
        var expected = new List<long>();
        await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await reader.ReadAsync().ConfigureAwait(false))
                expected.Add(reader.GetInt64(0));
        var benchmark = new ProjectorOutboxIndexBenchmarks();
        benchmark.SetupClaims();
        var batch = await benchmark.ClaimBatch().ConfigureAwait(false);
        if (!expected.Order().SequenceEqual(batch.Select(row => row.EventId).Order()))
            throw new InvalidOperationException("The full batch did not match the established claim priority.");
        benchmark.RestoreClaims();
        Console.WriteLine($"Full-batch priority matched. Claimed IDs: {string.Join(',', expected)}.");
    }

    static async Task<IReadOnlyList<EventProjectorOutboxReadModel>> ClaimAsync(EventSourceActorDbContext store,
        string projector, Guid token, DateTime now, TimeSpan lease, int size, CancellationToken cancellationToken = default)
    {
        if (!UseOrderedClaim)
            return await store.ClaimEventProjectorOutboxAsync(projector, token, now, lease, size, cancellationToken).ConfigureAwait(false);
        return [.. await store.Use(nameof(OrderedClaimSql), OrderedClaimSql)
            .SetParameters(new ClaimEventProjectorOutbox(projector, token, now + lease, now, size))
            .ExecuteQueryAsync<EventProjectorOutboxReadModel>(EventSourceActorDbContext.MapToEventProjectorOutbox, cancellationToken)
            .ConfigureAwait(false)];
    }

    static async Task VerifyClaimsAsync(NpgsqlConnection connection, EventSourceActorDbContext store)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO event_projector_outbox
                (projectorname,eventid,effectkind,messageid,eventtypename,eventpayload,status,attemptcount,
                 nextattemptatutc,createdatutc,dispatchtoken,dispatchleaseexpiresatutc)
            SELECT '{Checks}',id,effect,id || '-' || effect,'Benchmark',decode('01','hex'),status,attempts,
                CASE WHEN id=6 THEN $1 + interval '5 minutes' WHEN id=2 THEN $1 END,
                $1 - age * interval '1 minute',
                CASE WHEN status='Publishing' THEN $2 END,
                CASE WHEN id=3 THEN $1 WHEN id=7 THEN $1 + interval '5 minutes' END
            FROM (VALUES
                (1,'Pending','ProcessingPublication',0,8),
                (2,'Retrying','ProcessingPublication',2,6),
                (2,'Pending','CompletedPublication',0,6),
                (3,'Publishing','ProcessingPublication',1,7),
                (4,'Pending','ProcessingPublication',0,5),
                (5,'Pending','ProcessingPublication',0,4),
                (6,'Retrying','ProcessingPublication',1,9),
                (7,'Publishing','ProcessingPublication',1,9),
                (8,'Published','ProcessingPublication',1,9),
                (9,'Pending','ProcessingPublication',0,3)
            ) AS queue(id,status,effect,attempts,age);
            """, [Now, ExpiredToken]).ConfigureAwait(false);

        var old = (await ClaimAsync(store, Checks, ClaimToken, Now, Lease, 1).ConfigureAwait(false)).Single();
        if (old.EventId != 1 || old.AttemptCount != 1)
            throw new InvalidOperationException("The earliest eligible row was not claimed first.");
        var nextToken = Guid.NewGuid();
        var second = await ClaimAsync(store, Checks, nextToken, Now, Lease, 2).ConfigureAwait(false);
        if (second.Count != 2 || !second.Any(row => row.EventId == 3 && row.AttemptCount == 2)
            || !second.Any(row => row.EventId == 2 && row.EffectKind == EventProjectorEffectKind.CompletedPublication))
            throw new InvalidOperationException("Claim priority, effect tie-break or expired-lease recovery differed.");
        if (second.Any(row => row.DispatchToken != nextToken || row.DispatchLeaseExpiresAtUtc != Now + Lease))
            throw new InvalidOperationException("The claimant did not acquire the expected lease.");

        await using (var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false))
        {
            await using var lockRow = new NpgsqlCommand($"SELECT 1 FROM event_projector_outbox WHERE projectorname='{Checks}' AND eventid=2 AND effectkind='ProcessingPublication' FOR UPDATE;", connection, transaction);
            await lockRow.ExecuteScalarAsync().ConfigureAwait(false);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tokens = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var workers = await Task.WhenAll(tokens.Select(token =>
                ClaimAsync(store, Checks, token, Now, Lease, 2, timeout.Token))).ConfigureAwait(false);
            var ids = workers.SelectMany(batch => batch).Select(row => row.EventId).Order().ToArray();
            if (!ids.SequenceEqual(new long[] { 4, 5, 9 }) || workers.Where((batch, i) =>
                batch.Any(row => row.DispatchToken != tokens[i] || row.ProjectorName != Checks)).Any())
                throw new InvalidOperationException("Concurrent claims overlapped, crossed scope or failed to skip the locked row.");
            await transaction.RollbackAsync().ConfigureAwait(false);
        }
        var unlocked = (await ClaimAsync(store, Checks, Guid.NewGuid(), Now, Lease, 8).ConfigureAwait(false)).Single();
        if (unlocked.EventId != 2 || unlocked.AttemptCount != 3
            || (await ClaimAsync(store, Checks, Guid.NewGuid(), Now, Lease, 8).ConfigureAwait(false)).Count != 0)
            throw new InvalidOperationException("The released lock or future eligibility fence was not respected.");
        var recoveredToken = Guid.NewGuid();
        var recovered = (await ClaimAsync(store, Checks, recoveredToken, Now + Lease, Lease, 1).ConfigureAwait(false)).Single();
        if (recovered.EventId != old.EventId || recovered.AttemptCount != old.AttemptCount + 1 || recovered.DispatchToken != recoveredToken
            || await store.MarkEventProjectorOutboxPublishedAsync(old, Now + Lease).ConfigureAwait(false)
            || !await store.MarkEventProjectorOutboxPublishedAsync(recovered, Now + Lease).ConfigureAwait(false))
            throw new InvalidOperationException("Lease recovery or stale-token fencing failed.");
        Console.WriteLine("Verified claim priority and effect tie-break, two concurrent workers with disjoint results, SKIP LOCKED, future retry/lease exclusion, reclaim at expiry, and stale-token rejection.");
    }

    [GlobalSetup(Target = nameof(ClaimBatch))]
    public void SetupClaims() => context = CreateContext();

    [IterationCleanup(Target = nameof(ClaimBatch))]
    public void RestoreClaims()
    {
        using var connection = Connection();
        connection.Open();
        using var command = new NpgsqlCommand("""
            UPDATE event_projector_outbox
            SET status = CASE WHEN eventid % 100 < 3 THEN 'Pending'
                              WHEN eventid % 100 < 6 THEN 'Retrying' ELSE 'Publishing' END,
                attemptcount = attemptcount - 1,
                nextattemptatutc = CASE WHEN eventid % 100 BETWEEN 4 AND 5 THEN $1 - interval '1 minute' END,
                dispatchtoken = CASE WHEN eventid % 100 BETWEEN 6 AND 8 THEN $2 END,
                dispatchleaseexpiresatutc = CASE WHEN eventid % 100 BETWEEN 6 AND 8 THEN $1 - interval '1 minute' END
            WHERE projectorname=$3 AND dispatchtoken=$4;
            """, connection);
        PostgresValueBinding.AddParameters(command, [Now, ExpiredToken, Target, ClaimToken]);
        command.ExecuteNonQuery();
    }

    [Benchmark]
    public async Task<IReadOnlyList<EventProjectorOutboxReadModel>> ClaimBatch()
    {
        var claimed = await ClaimAsync(context, Target, ClaimToken, Now, Lease, BatchSize).ConfigureAwait(false);
        return claimed.Count == BatchSize ? claimed : throw new InvalidOperationException("The benchmark exhausted its eligible batch.");
    }

    [GlobalSetup(Target = nameof(Enqueue))]
    public async Task SetupWrites()
    {
        await using var connection = Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, "CHECKPOINT;").ConfigureAwait(false);
        initialWal = (string)(await ScalarAsync(connection, "SELECT pg_current_wal_insert_lsn()::text;").ConfigureAwait(false))!;
    }

    [Benchmark]
    public async Task Enqueue()
    {
        var id = ++writes;
        await using var connection = Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, """
            INSERT INTO event_projector_outbox
                (projectorname,eventid,effectkind,messageid,eventtypename,eventpayload,status,attemptcount,createdatutc)
            VALUES ($1,$2,'ProcessingPublication',$3,'Benchmark',decode(repeat('ab',128),'hex'),'Pending',0,$4)
            ON CONFLICT (projectorname,eventid,effectkind) DO NOTHING;
            """, [Writes, (long)id, $"write-{id}", Now]).ConfigureAwait(false);
    }

    [GlobalCleanup(Target = nameof(Enqueue))]
    public async Task VerifyWrites()
    {
        await using var connection = Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        var count = await ScalarAsync(connection, $"SELECT count(*) FROM event_projector_outbox WHERE projectorname='{Writes}';").ConfigureAwait(false);
        var wal = (decimal)(await ScalarAsync(connection, "SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn);",
            [initialWal]).ConfigureAwait(false))!;
        if (!Equals(count, (long)writes) || writes == 0)
            throw new InvalidOperationException("The enqueue workload did not persist every row.");
        Console.WriteLine(FormattableString.Invariant($"Write verification: {writes} rows, {wal} WAL bytes, {wal / writes:F2} WAL bytes/row."));
    }

    static async Task ExecuteAsync(NpgsqlConnection connection, string sql, object?[]? values = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (values is not null)
            PostgresValueBinding.AddParameters(command, values);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, object?[]? values = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (values is not null)
            PostgresValueBinding.AddParameters(command, values);
        return await command.ExecuteScalarAsync().ConfigureAwait(false);
    }
}
