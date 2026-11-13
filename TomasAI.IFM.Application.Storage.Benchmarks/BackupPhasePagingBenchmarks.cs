using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class BackupPhasePagingBenchmarks
{
    internal const string Table = "system_admin.database_recovery_phase";
    const string Index = "ix_database_recovery_phase_revision";
    internal const int History = 180_000;
    internal const int Deep = 150_000;
    internal const int Limit = 32;
    const string CurrentSql =
        """
        SELECT event_revision, observed_utc, phase, outcome, progress_percent
        FROM (
            SELECT event_revision, observed_utc, phase, outcome,
                   MAX(progress_percent) OVER (ORDER BY event_revision) AS progress_percent
            FROM system_admin.database_recovery_phase WHERE operation_id = $1
        ) AS phases
        WHERE event_revision > $2 ORDER BY event_revision LIMIT $3;
        """;
    const string SeededSql = SystemAdminDbSql.GetBackupPhases;
    internal static readonly Guid Operation = new("10000000-0000-0000-0000-000000000001");
    static readonly Guid Other = new("10000000-0000-0000-0000-000000000002");
    internal static readonly Guid Tied = new("10000000-0000-0000-0000-000000000003");
    internal static readonly DateTimeOffset Start = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    NpgsqlConnection db = null!;
    NpgsqlCommand current = null!;
    NpgsqlCommand seeded = null!;

    [Params(false, true)] public bool DeepPage { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        current = Command(db, CurrentSql, Operation, DeepPage ? Deep : 0, Limit);
        seeded = Command(db, SeededSql, Operation, DeepPage ? Deep : 0, Limit);
        await current.PrepareAsync().ConfigureAwait(false);
        await seeded.PrepareAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)] public Task<List<DatabaseBackupPhaseReadModel>> Current() => Read(current);
    [Benchmark] public Task<List<DatabaseBackupPhaseReadModel>> PriorMaxAndPage() => Read(seeded);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await current.DisposeAsync().ConfigureAwait(false);
        await seeded.DisposeAsync().ConfigureAwait(false);
        await db.DisposeAsync().ConfigureAwait(false);
    }

    internal static NpgsqlCommand Command(NpgsqlConnection db, string sql, Guid operation, long cursor, int limit)
    {
        var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, [operation, cursor, limit]);
        return command;
    }

    internal static async Task<List<DatabaseBackupPhaseReadModel>> Read(NpgsqlCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(reader);
        var rows = new List<DatabaseBackupPhaseReadModel>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            rows.Add(SystemAdminDbContext.MapToBackupPhase(record));
        return rows;
    }

    internal static async Task PrepareAsync(bool indexed, bool verifyApplied = false)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(SystemAdminDbContext.SystemAdminDbConnection,
            Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")!, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        var schema = new SystemAdminSchemaDb(settings, logger);
        await schema.CreateAsync(["system_admin", "database_recovery_phase"]).ConfigureAwait(false);
        if (verifyApplied)
        {
            await VerifyIndex(db).ConfigureAwait(false);
            await using var empty = Command(db, SeededSql, Operation, 0, Limit);
            if ((await Read(empty).ConfigureAwait(false)).Count != 0)
                throw new InvalidOperationException("Fresh phase history should be empty.");
            Console.WriteLine("Verified fresh schema and empty phase history.");
        }
        await Execute(db, $"DROP INDEX system_admin.{Index};").ConfigureAwait(false);
        await Seed(db).ConfigureAwait(false);
        if (indexed)
        {
            var fingerprint = verifyApplied ? await Fingerprint(db).ConfigureAwait(false) : null;
            await schema.CreateAsync(["database_recovery_phase"]).ConfigureAwait(false);
            if (verifyApplied)
            {
                var oid = await VerifyIndex(db).ConfigureAwait(false);
                await schema.CreateAsync(["database_recovery_phase"]).ConfigureAwait(false);
                if (oid != await VerifyIndex(db).ConfigureAwait(false) || fingerprint != await Fingerprint(db).ConfigureAwait(false))
                    throw new InvalidOperationException("Phase migration changed the stored rows or index identity.");
                Console.WriteLine($"Verified populated upgrade and reapplication, fingerprint {fingerprint}, index OID {oid}.");
            }
        }
        await Execute(db, $"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);
        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"))
                { Options = "-c plan_cache_mode=" + mode };
            var context = verifyApplied ? new SystemAdminDbContext(new DbConnectionSettings().Add(
                SystemAdminDbContext.SystemAdminDbConnection, builder.ConnectionString, "System.Data.Postgres"), logger) : null;
            await Execute(db, $"SET plan_cache_mode={mode}").ConfigureAwait(false);
            foreach (var (label, cursor, limit) in new[] { ("first", 0L, Limit), ("deep", (long)Deep, Limit),
                         ("terminal", History - 3L, Limit), ("past-end", (long)History, Limit), ("zero-limit", (long)Deep, 0) })
            {
                var page = await Verify(db, label, Operation, cursor, limit, context).ConfigureAwait(false);
                var revisions = Enumerable.Range(1, History).Skip((int)cursor).Take(limit);
                if (!page.Select(x => x.Revision).SequenceEqual(revisions.Select(n => (long)n)) ||
                    page.Any(row => row.ObservedUtc != Start.AddSeconds(row.Revision)
                        || row.ProgressPercent != ExpectedProgress(row.Revision)))
                    throw new InvalidOperationException($"Incorrect revision, time or running progress for {label}.");
                if (page.Count == Limit)
                    await Verify(db, label + "-replay", Operation, page[^1].Revision, limit, context).ConfigureAwait(false);
            }
            var peers = await Verify(db, "full-peer-groups", Tied, 0, Limit, context).ConfigureAwait(false);
            if (!peers.Select(x => (x.Revision, x.ProgressPercent)).SequenceEqual(
                new (long, int)[] { (1, 78), (1, 78), (2, 78), (4, 96), (4, 96), (6, 96) }))
                throw new InvalidOperationException("Peer maximum was not preserved.");
            var boundary = await Verify(db, "cursor-in-gap", Tied, 3, Limit, context).ConfigureAwait(false);
            if (boundary.Count != 3 || boundary.Any(row => row.ProgressPercent != 96))
                throw new InvalidOperationException("Cursor gap or peer progress changed.");
            await Verify(db, "cursor-after-peers", Tied, 4, Limit, context).ConfigureAwait(false);
            await Verify(db, "unknown-operation", Guid.Empty, 0, Limit, context).ConfigureAwait(false);
            // Ordering within a tied revision is unspecified. Both queries must expose its full max even when LIMIT cuts it.
            foreach (var sql in new[] { CurrentSql, SeededSql })
            {
                await using var cut = Command(db, sql, Tied, 0, 1);
                await cut.PrepareAsync().ConfigureAwait(false);
                var rows = await Read(cut).ConfigureAwait(false);
                if (rows.Count != 1 || rows[0].Revision != 1 || rows[0].ProgressPercent != 78)
                    throw new InvalidOperationException("Truncated peer group lost its maximum.");
            }
            if (context is not null)
            {
                var rows = await ReadProduction(context, Tied, 0, 1).ConfigureAwait(false);
                if (rows.Length != 1 || rows[0].Revision != 1 || rows[0].ProgressPercent != 78)
                    throw new InvalidOperationException("Production page lost the truncated peer group's maximum.");
            }
            Console.WriteLine("VERIFIED,limit cuts tied revision,full peer maximum retained");
            foreach (var (name, sql) in new[] { ("current", CurrentSql), ("seeded", SeededSql) })
            {
                await Execute(db, $"PREPARE phase_page(uuid,bigint,int) AS {sql}").ConfigureAwait(false);
                foreach (var cursor in new[] { 0, Deep })
                {
                    Console.WriteLine($"PLAN,{mode},{name},{cursor}");
                    await using var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS) EXECUTE phase_page('{Operation}',{cursor},{Limit})", db);
                    await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                    var lines = new List<string>();
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        lines.Add(reader.GetString(0));
                    Console.WriteLine(string.Join(Environment.NewLine, lines));
                    if (verifyApplied && name == "seeded")
                    {
                        if (!lines.Any(line => line.Contains($"Index Scan using {Index} ", StringComparison.Ordinal)) ||
                            !lines.Any(line => line.Contains("Index Cond:", StringComparison.Ordinal)
                                && line.Contains("event_revision >", StringComparison.Ordinal)) ||
                            lines.Any(line => line.Contains("external merge", StringComparison.Ordinal)))
                            throw new InvalidOperationException($"Expected a bounded revision seek in {mode} at {cursor}.");
                        Console.WriteLine($"VERIFIED-PLAN,{mode},{cursor},bounded revision seek");
                    }
                }
                await Execute(db, "DEALLOCATE phase_page").ConfigureAwait(false);
            }
        }
    }

    internal static async Task Seed(NpgsqlConnection db)
    {
        await Execute(db, $"""
            INSERT INTO {Table}(operation_id,phase,event_revision,outcome,progress_percent,observed_utc,last_event_id,last_source_event_id)
            SELECT op,1+(n%7),n,0,CASE WHEN op='{Other}' THEN 100
                WHEN n=25 THEN 72 WHEN n=150011 THEN 91 WHEN n=179980 THEN 100 ELSE n%31 END,
                timestamptz '2026-10-13 12:00Z'+n*interval '1 second',n,md5(n::text)::uuid
            FROM (VALUES ('{Operation}'::uuid,{History}),('{Other}'::uuid,4096)) operations(op,count)
            CROSS JOIN LATERAL generate_series(1,count) n;
            INSERT INTO {Table}(operation_id,phase,event_revision,outcome,progress_percent,observed_utc,last_event_id,last_source_event_id)
            SELECT '{Tied}',phase,rev,0,progress,timestamptz '2026-10-13 12:00Z'+rev*interval '1 second',
                rev,md5(rev::text)::uuid
            FROM (VALUES (1,1,10),(2,1,78),(1,2,20),(1,4,50),(2,4,96),(1,6,2)) p(phase,rev,progress);
            """).ConfigureAwait(false);
    }

    static int ExpectedProgress(long revision) => revision >= 179980 ? 100
        : revision >= 150011 ? 91 : revision >= 25 ? 72 : (int)revision;

    static async Task<List<DatabaseBackupPhaseReadModel>> Verify(NpgsqlConnection db, string label, Guid operation, long cursor,
        int limit, SystemAdminDbContext? context)
    {
        await using var before = Command(db, CurrentSql, operation, cursor, limit);
        await using var after = Command(db, SeededSql, operation, cursor, limit);
        await before.PrepareAsync().ConfigureAwait(false);
        await after.PrepareAsync().ConfigureAwait(false);
        var baseline = await Read(before).ConfigureAwait(false);
        var candidate = await Read(after).ConfigureAwait(false);
        var applied = context is null ? candidate.ToArray() : await ReadProduction(context, operation, cursor, limit).ConfigureAwait(false);
        if (!baseline.Select(x => x.Revision).SequenceEqual(candidate.Select(x => x.Revision))
            || !baseline.OrderBy(x => x.Revision).ThenBy(x => x.Phase).SequenceEqual(
                candidate.OrderBy(x => x.Revision).ThenBy(x => x.Phase))
            || !candidate.Select(x => x.Revision).SequenceEqual(applied.Select(x => x.Revision))
            || !candidate.OrderBy(x => x.Revision).ThenBy(x => x.Phase).SequenceEqual(
                applied.OrderBy(x => x.Revision).ThenBy(x => x.Phase))
            || candidate.Zip(candidate.Skip(1)).Any(pair => pair.First.ProgressPercent > pair.Second.ProgressPercent))
            throw new InvalidOperationException($"Page or progress changed for {label}.");
        Console.WriteLine($"ORDERED,{label},{cursor},{candidate.Count},{candidate.FirstOrDefault()?.ProgressPercent},{candidate.LastOrDefault()?.ProgressPercent}");
        return baseline;
    }

    static ValueTask<DatabaseBackupPhaseReadModel[]> ReadProduction(SystemAdminDbContext context, Guid operation, long cursor, int limit) =>
        context.GetBackupPhasesAsync(new GetDatabaseBackupLogQuery
        {
            OperationId = new(operation), AfterPhaseRevision = cursor, PageSize = limit
        }, default);

    static async Task<long> VerifyIndex(NpgsqlConnection db)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT indexrelid::bigint FROM pg_index
            WHERE indexrelid='system_admin.{Index}'::regclass AND indrelid='{Table}'::regclass
                AND indisvalid AND indisready AND indpred IS NULL
                AND pg_get_indexdef(indexrelid) LIKE '%USING btree (operation_id, event_revision) INCLUDE (progress_percent)'
            """, db);
        return (await command.ExecuteScalarAsync().ConfigureAwait(false)) is long oid
            ? oid : throw new InvalidOperationException("Missing valid covering revision index.");
    }

    static async Task<string> Fingerprint(NpgsqlConnection db)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT count(*) || ':' || sum(hashtextextended(row_to_json(p)::text,0)::numeric) FROM {Table} p
            """, db);
        return (string)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    internal static async Task Execute(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
