using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.Benchmarks.BackupPhasePagingBenchmarks;
using RecoveryFixture = TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class BackupPhaseProgressFirstBenchmarks
{
    const string ProgressIndex = "ix_bench_database_recovery_phase_progress";
    const string CurrentSql = SystemAdminDbSql.GetBackupPhases;
    // A short revision prefix can be exhausted cheaply, even when most higher
    // progress values belong to future revisions. The extra row detects overflow.
    static readonly string BoundedSql = System.Text.RegularExpressions.Regex.Replace(
        CurrentSql,
        """
        \(SELECT MAX\(progress_percent\) FROM system_admin\.database_recovery_phase\s+WHERE operation_id=\$1 AND event_revision<=\$2\)
        """,
        static _ => BoundedSeed);

    const string BoundedSeed =
        """
        (SELECT CASE WHEN COUNT(*) <= 64 THEN MAX(progress_percent)
                ELSE (SELECT progress_percent FROM system_admin.database_recovery_phase
                      WHERE operation_id=$1 AND event_revision<=$2
                      ORDER BY progress_percent DESC LIMIT 1) END
         FROM (SELECT progress_percent FROM system_admin.database_recovery_phase
               WHERE operation_id=$1 AND event_revision<=$2
               ORDER BY event_revision DESC LIMIT 65) AS short_prefix)
        """;

    static readonly Guid FuturePeaks = new("10000000-0000-0000-0000-000000000004");
    static readonly Guid Nonpositive = new("10000000-0000-0000-0000-000000000005");
    NpgsqlConnection db = null!;
    NpgsqlCommand command = null!;
    long writeRevision = History;

    [Params("First", "Early", "Deep", "FuturePeaks", "Append")]
    public string Workload { get; set; } = "";

    [Params("Max", "BoundedSeek")]
    public string SeedShape { get; set; } = "";

    [GlobalSetup]
    public async Task Setup()
    {
        db = RecoveryFixture.Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        if (Workload == "Append")
        {
            command = Insert(db);
            // The WAL batch has already populated the append tail.
            writeRevision += 8192;
        }
        else
        {
            var (operation, cursor) = ReadCase(Workload);
            command = Command(db, SeedShape == "Max" ? CurrentSql : BoundedSql, operation, cursor, Limit);
        }
        await command.PrepareAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> ExecuteWorkload()
    {
        if (Workload == "Append")
            return await Append(command, ++writeRevision).ConfigureAwait(false);

        return (await Read(command).ConfigureAwait(false)).Count;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await command.DisposeAsync().ConfigureAwait(false);
        await db.DisposeAsync().ConfigureAwait(false);
    }

    static (Guid Operation, long Cursor) ReadCase(string workload) => workload switch
    {
        "First" => (Operation, 0),
        "Early" => (Operation, 16),
        "Deep" => (Operation, Deep),
        "FuturePeaks" => (FuturePeaks, 4096),
        _ => throw new ArgumentOutOfRangeException(nameof(workload))
    };

    internal static async Task PrepareAsync(bool indexed)
    {
        if (BoundedSql == CurrentSql)
            throw new InvalidOperationException("The benchmark seed replacement did not match the production query.");
        await using var connection = RecoveryFixture.Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(SystemAdminDbContext.SystemAdminDbConnection,
            Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")!, "System.Data.Postgres");
        var schema = new SystemAdminSchemaDb(settings, NullLogger<DbProvider>.Instance);
        await schema.CreateAsync(["system_admin", "database_recovery_phase"]).ConfigureAwait(false);
        await Seed(connection).ConfigureAwait(false);
        await Execute(connection, $"""
            INSERT INTO {Table}(operation_id,phase,event_revision,outcome,progress_percent,observed_utc,last_event_id,last_source_event_id)
            SELECT '{FuturePeaks}',1,n,0,CASE WHEN n<=4096 THEN n%31 WHEN n<=4128 THEN 0 ELSE 100 END,
                timestamptz '2026-10-13 12:00Z'+n*interval '1 second',n,md5(n::text)::uuid
            FROM generate_series(1,40000) n;
            INSERT INTO {Table}(operation_id,phase,event_revision,outcome,progress_percent,observed_utc,last_event_id,last_source_event_id)
            SELECT '{Nonpositive}',1,rev,0,progress,timestamptz '2026-10-13 12:00Z',rev,md5(rev::text)::uuid
            FROM (VALUES (-3,96),(0,2),(1,5),(2,4)) p(rev,progress);
            """).ConfigureAwait(false);
        if (indexed)
        {
            await Execute(connection, $"""
                CREATE INDEX {ProgressIndex} ON {Table}(operation_id, progress_percent DESC, event_revision);
                """).ConfigureAwait(false);
            Console.WriteLine($"INDEX_BYTES,{await Scalar(connection, $"SELECT pg_relation_size('system_admin.{ProgressIndex}')")}");
        }
        await Execute(connection, $"VACUUM (ANALYZE) {Table}").ConfigureAwait(false);
        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            await Execute(connection, $"SET plan_cache_mode={mode}").ConfigureAwait(false);
            var variants = indexed ? new[] { ("max", CurrentSql), ("bounded", BoundedSql) } : [("max", CurrentSql)];
            foreach (var (name, sql) in variants)
            {
                await Execute(connection, $"PREPARE phase_seed(uuid,bigint,int) AS {sql}").ConfigureAwait(false);
                foreach (var label in new[] { "First", "Early", "Deep", "FuturePeaks" })
                {
                    var (operation, cursor) = ReadCase(label);
                    Console.WriteLine($"PLAN,{mode},{name},{label}");
                    await using var explain = new NpgsqlCommand(
                        $"EXPLAIN (ANALYZE, BUFFERS) EXECUTE phase_seed('{operation}',{cursor},{Limit})", connection);
                    await using var reader = await explain.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        Console.WriteLine(reader.GetString(0));
                }
                await Execute(connection, "DEALLOCATE phase_seed").ConfigureAwait(false);
                await Verify(connection, sql).ConfigureAwait(false);
                Console.WriteLine($"VERIFIED,{mode},{name},exact ordered pages and monotonic progress; cursor replay, ties, gaps, and empty prefix");
            }
        }
        await MeasureWal(connection).ConfigureAwait(false);
    }

    static async Task Verify(NpgsqlConnection db, string sql)
    {
        foreach (var cursor in new long[] { 0, 16, 64, 65, Deep, Deep + Limit, History - 3, History })
        {
            var page = await Page(db, sql, Operation, cursor, Limit).ConfigureAwait(false);
            var expected = Enumerable.Range((int)cursor + 1, Math.Min(Limit, History - (int)cursor))
                .Select(revision => ((long)revision, ExpectedProgress(revision)));
            VerifyRows(page, expected);
            if (page.Any(row => row.ObservedUtc != Start.AddSeconds(row.Revision) || (int)row.Phase != 1 + row.Revision % 7))
                throw new InvalidOperationException("Unexpected phase payload.");
            if (page.Count == Limit)
            {
                await using var legacy = Command(db, CurrentSql, Operation, cursor, Limit);
                var existing = await Read(legacy).ConfigureAwait(false);
                var next = await Page(db, sql, Operation, existing[^1].Revision, Limit).ConfigureAwait(false);
                VerifyRows(next, Enumerable.Range((int)cursor + Limit + 1, Limit)
                    .Select(revision => ((long)revision, ExpectedProgress(revision))));
                if (next[0].ProgressPercent < existing[^1].ProgressPercent)
                    throw new InvalidOperationException("Progress fell across an existing cursor.");
            }
        }
        VerifyRows(await Page(db, sql, FuturePeaks, 4096, Limit).ConfigureAwait(false),
            Enumerable.Range(4097, Limit).Select(revision => ((long)revision, 30)));
        VerifyRows(await Page(db, sql, FuturePeaks, 4090, 6).ConfigureAwait(false),
            Enumerable.Range(4091, 6).Select(revision => ((long)revision, 30)));
        VerifyRows(await Page(db, sql, Tied, 0, Limit).ConfigureAwait(false),
            [(1, 78), (1, 78), (2, 78), (4, 96), (4, 96), (6, 96)]);
        VerifyRows(await Page(db, sql, Tied, 0, 1).ConfigureAwait(false), [(1, 78)]);
        VerifyRows(await Page(db, sql, Tied, 3, Limit).ConfigureAwait(false), [(4, 96), (4, 96), (6, 96)]);
        VerifyRows(await Page(db, sql, Tied, 4, Limit).ConfigureAwait(false), [(6, 96)]);
        VerifyRows(await Page(db, sql, Nonpositive, 0, Limit).ConfigureAwait(false), [(1, 96), (2, 96)]);
        VerifyRows(await Page(db, sql, Guid.Empty, 0, Limit).ConfigureAwait(false), []);
        VerifyRows(await Page(db, sql, Operation, Deep, 0).ConfigureAwait(false), []);
    }

    static int ExpectedProgress(int revision) => revision >= 179980 ? 100
        : revision >= 150011 ? 91 : revision >= 25 ? 72 : revision;

    static async Task<List<DatabaseBackupPhaseReadModel>> Page(NpgsqlConnection db, string sql, Guid operation, long cursor, int limit)
    {
        await using var command = Command(db, sql, operation, cursor, limit);
        await command.PrepareAsync().ConfigureAwait(false);
        return await Read(command).ConfigureAwait(false);
    }

    static void VerifyRows(List<DatabaseBackupPhaseReadModel> page, IEnumerable<(long Revision, int Progress)> expected)
    {
        if (!page.Select(row => (row.Revision, row.ProgressPercent)).SequenceEqual(expected))
            throw new InvalidOperationException("Unexpected revision or running progress.");
    }

    static NpgsqlCommand Insert(NpgsqlConnection db)
    {
        var command = new NpgsqlCommand(SystemAdminDbSql.InsertPhase, db);
        command.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = Operation });
        command.Parameters.Add(new NpgsqlParameter<short> { TypedValue = 1 });
        command.Parameters.Add(new NpgsqlParameter<long>());
        command.Parameters.Add(new NpgsqlParameter<short> { TypedValue = 0 });
        command.Parameters.Add(new NpgsqlParameter<int>());
        command.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { TypedValue = Start });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter<long>());
        command.Parameters.Add(new NpgsqlParameter<Guid>());
        return command;
    }

    static Task<int> Append(NpgsqlCommand insert, long revision)
    {
        ((NpgsqlParameter<short>)insert.Parameters[1]).TypedValue = (short)(1 + revision % 7);
        ((NpgsqlParameter<long>)insert.Parameters[2]).TypedValue = revision;
        ((NpgsqlParameter<int>)insert.Parameters[4]).TypedValue = (int)(revision % 101);
        ((NpgsqlParameter<long>)insert.Parameters[7]).TypedValue = revision;
        ((NpgsqlParameter<Guid>)insert.Parameters[8]).TypedValue = Guid.NewGuid();
        return insert.ExecuteNonQueryAsync();
    }

    static async Task MeasureWal(NpgsqlConnection db)
    {
        const int count = 8192;
        await Execute(db, "CHECKPOINT").ConfigureAwait(false);
        await using var insert = Insert(db);
        await insert.PrepareAsync().ConfigureAwait(false);
        var start = await Scalar(db, "SELECT pg_current_wal_insert_lsn()::text").ConfigureAwait(false);
        for (var revision = History + 1L; revision <= History + count; revision++)
            if (await Append(insert, revision).ConfigureAwait(false) != 1)
                throw new InvalidOperationException("Fixture insert did not append a row.");
        var bytes = await Scalar(db,
            $"SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), '{start}'::pg_lsn)").ConfigureAwait(false);
        if (await Append(insert, History + count).ConfigureAwait(false) != 0)
            throw new InvalidOperationException("The existing insert retry should be ignored.");
        Console.WriteLine($"WAL,{count},{bytes},verified inserts and duplicate retry");
    }

    static async Task<object?> Scalar(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        return await command.ExecuteScalarAsync().ConfigureAwait(false);
    }
}
